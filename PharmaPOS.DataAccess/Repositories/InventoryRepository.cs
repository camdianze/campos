using Microsoft.Data.Sqlite;
using PharmaPOS.Application.Inventory;
using PharmaPOS.Application.Repositories;
using PharmaPOS.DataAccess.Database;

namespace PharmaPOS.DataAccess.Repositories;

/// <summary>
/// IInventoryRepository의 SQLite 구현체.
/// </summary>
public class InventoryRepository : IInventoryRepository
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public InventoryRepository(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<InventoryStatusItem>> GetInventoryStatusAsync(
        string searchTerm,
        ExpiryFilterOption expiryFilter,
        bool lowStockOnly,
        InventorySortOption sortBy)
    {
        using var connection = _connectionFactory.CreateOpenConnection();

        using var command = connection.CreateCommand();

        var whereClauses = new List<string> { "p.status = 'Active'" };

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            whereClauses.Add("""
                (LOWER(p.product_name) LIKE LOWER($search)
                 OR LOWER(p.generic_name) LIKE LOWER($search)
                 OR LOWER(p.barcode) LIKE LOWER($search)
                 OR LOWER(p.internal_barcode) LIKE LOWER($search))
                """);
            command.Parameters.AddWithValue("$search", $"%{searchTerm}%");
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        command.Parameters.AddWithValue("$now", now);

        switch (expiryFilter)
        {
            case ExpiryFilterOption.Expired:
                // expiry_date = 0은 "유효기간 모름"이다. 빼지 않으면 그 배치가 전부 만료로 잡힌다.
                // 나머지 필터는 BETWEEN $now …라 0이 저절로 빠진다.
                whereClauses.Add("i.expiry_date > 0 AND i.expiry_date < $now");
                break;
            case ExpiryFilterOption.Within7Days:
                whereClauses.Add("i.expiry_date BETWEEN $now AND $now + 7 * 86400000");
                break;
            case ExpiryFilterOption.Within30Days:
                whereClauses.Add("i.expiry_date BETWEEN $now AND $now + 30 * 86400000");
                break;
            case ExpiryFilterOption.Within90Days:
                whereClauses.Add("i.expiry_date BETWEEN $now AND $now + 90 * 86400000");
                break;
        }

        if (lowStockOnly)
        {
            whereClauses.Add("i.current_quantity < p.safety_stock_level");
        }

        var orderBySql = sortBy switch
        {
            InventorySortOption.Quantity => "i.current_quantity ASC",
            InventorySortOption.ExpiryDate => "i.expiry_date ASC",
            _ => "p.product_name ASC"
        };

        command.CommandText = $"""
            SELECT i.inventory_id, i.product_id, p.product_name, p.generic_name,
                   p.barcode, p.internal_barcode, i.batch_number, i.expiry_date,
                   i.current_quantity, p.selling_price, p.safety_stock_level, i.updated_at,
                   i.box_quantity, i.unit_quantity, p.units_per_box, p.unit_selling_price
            FROM Inventory i
            JOIN Product_Master p ON p.product_id = i.product_id
            WHERE {string.Join(" AND ", whereClauses)}
            ORDER BY {orderBySql};
            """;

        using var reader = await command.ExecuteReaderAsync();

        var results = new List<InventoryStatusItem>();
        while (await reader.ReadAsync())
        {
            results.Add(new InventoryStatusItem
            {
                InventoryId = reader.GetString(0),
                ProductId = reader.GetString(1),
                ProductName = reader.GetString(2),
                GenericName = reader.IsDBNull(3) ? null : reader.GetString(3),
                Barcode = reader.IsDBNull(4) ? null : reader.GetString(4),
                InternalBarcode = reader.IsDBNull(5) ? null : reader.GetString(5),
                BatchNumber = reader.GetString(6),
                ExpiryDate = reader.GetInt64(7),
                CurrentQuantity = reader.GetInt32(8),
                SellingPrice = (decimal)reader.GetDouble(9),
                SafetyStockLevel = reader.GetInt32(10),
                UpdatedAt = reader.GetInt64(11),
                BoxQuantity = reader.GetInt32(12),
                UnitQuantity = reader.GetInt32(13),
                UnitsPerBox = reader.GetInt32(14),
                UnitSellingPrice = reader.IsDBNull(15) ? null : (decimal)reader.GetDouble(15)
            });
        }

        return results;
    }

    public async Task<bool> DeleteEmptyBatchAsync(string inventoryId)
    {
        using var connection = _connectionFactory.CreateOpenConnection();

        using var command = connection.CreateCommand();

        // current_quantity = 0 조건을 WHERE에 함께 두는 게 핵심이다. 화면에서 0인 걸
        // 확인하고 누르는 사이에 입고가 들어오면, 이 조건 덕분에 조용히 아무것도 안 지운다.
        command.CommandText = """
            DELETE FROM Inventory
            WHERE inventory_id = $inventoryId
              AND current_quantity = 0;
            """;
        command.Parameters.AddWithValue("$inventoryId", inventoryId);

        var rowsAffected = await command.ExecuteNonQueryAsync();
        return rowsAffected > 0;
    }

    public async Task<IReadOnlyList<InventoryBatchOption>> GetBatchesForProductAsync(string productId, string facilityId)
    {
        using var connection = _connectionFactory.CreateOpenConnection();

        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT inventory_id, batch_number, expiry_date, current_quantity,
                   box_quantity, unit_quantity
            FROM Inventory
            WHERE product_id = $productId AND facility_id = $facilityId
            -- 선입선출이므로 유효기한이 이른 배치가 먼저다. 유효기간을 모르는 배치(0)는
            -- 맨 뒤로 보낸다 — 0을 그냥 정렬하면 "가장 급한 배치"로 올라와 먼저 팔린다.
            ORDER BY CASE WHEN expiry_date = 0 THEN 1 ELSE 0 END, expiry_date ASC;
            """;
        command.Parameters.AddWithValue("$productId", productId);
        command.Parameters.AddWithValue("$facilityId", facilityId);

        using var reader = await command.ExecuteReaderAsync();

        var results = new List<InventoryBatchOption>();
        while (await reader.ReadAsync())
        {
            results.Add(new InventoryBatchOption
            {
                InventoryId = reader.GetString(0),
                BatchNumber = reader.GetString(1),
                ExpiryDate = reader.GetInt64(2),
                CurrentQuantity = reader.GetInt32(3),
                BoxQuantity = reader.GetInt32(4),
                UnitQuantity = reader.GetInt32(5)
            });
        }

        return results;
    }
}