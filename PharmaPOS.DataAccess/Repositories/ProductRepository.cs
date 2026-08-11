using Microsoft.Data.Sqlite;
using PharmaPOS.Application.Repositories;
using PharmaPOS.DataAccess.Database;
using PharmaPOS.Domain.Entities;
using PharmaPOS.Domain.Enums;

namespace PharmaPOS.DataAccess.Repositories;

/// <summary>
/// IProductRepository의 SQLite 구현체.
/// </summary>
public class ProductRepository : IProductRepository
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public ProductRepository(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<Product>> SearchAsync(string searchTerm, EntityStatus? statusFilter)
    {
        using var connection = _connectionFactory.CreateOpenConnection();

        using var command = connection.CreateCommand();

        var whereClauses = new List<string>();

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            // LOWER()로 양쪽을 소문자로 맞춰서 비교 — 대소문자 구분 없이 검색되도록 명시적으로 처리한다.
            whereClauses.Add("""
                (LOWER(product_name) LIKE LOWER($search)
                 OR LOWER(generic_name) LIKE LOWER($search)
                 OR LOWER(barcode) LIKE LOWER($search)
                 OR LOWER(internal_barcode) LIKE LOWER($search))
                """);
            command.Parameters.AddWithValue("$search", $"%{searchTerm}%");
        }

        if (statusFilter is not null)
        {
            whereClauses.Add("status = $status");
            command.Parameters.AddWithValue("$status", statusFilter.Value.ToString());
        }

        var whereSql = whereClauses.Count > 0
            ? "WHERE " + string.Join(" AND ", whereClauses)
            : "";

        command.CommandText = $"""
            SELECT product_id, barcode, internal_barcode, product_name, generic_name,
                   strength, unit, manufacturer, country_of_origin, cost_price,
                   selling_price, safety_stock_level, status, created_at,
                   atc_code, is_combination, units_per_box, unit_selling_price, category
            FROM Product_Master
            {whereSql}
            ORDER BY product_name;
            """;

        using var reader = await command.ExecuteReaderAsync();

        var results = new List<Product>();
        while (await reader.ReadAsync())
        {
            results.Add(MapToProduct(reader));
        }

        return results;
    }

    public async Task<Product?> GetByIdAsync(string productId)
    {
        using var connection = _connectionFactory.CreateOpenConnection();

        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT product_id, barcode, internal_barcode, product_name, generic_name,
                   strength, unit, manufacturer, country_of_origin, cost_price,
                   selling_price, safety_stock_level, status, created_at,
                   atc_code, is_combination, units_per_box, unit_selling_price, category
            FROM Product_Master
            WHERE product_id = $productId;
            """;
        command.Parameters.AddWithValue("$productId", productId);

        using var reader = await command.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
        {
            return null;
        }

        return MapToProduct(reader);
    }

    public async Task<bool> BarcodeExistsAsync(string barcode, string? excludeProductId = null)
    {
        using var connection = _connectionFactory.CreateOpenConnection();

        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(*) FROM Product_Master
            WHERE barcode = $barcode
              AND ($excludeProductId IS NULL OR product_id != $excludeProductId);
            """;
        command.Parameters.AddWithValue("$barcode", barcode);
        command.Parameters.AddWithValue("$excludeProductId", (object?)excludeProductId ?? DBNull.Value);

        var count = (long)(await command.ExecuteScalarAsync())!;
        return count > 0;
    }

    public async Task<bool> InternalBarcodeExistsAsync(string internalBarcode, string? excludeProductId = null)
    {
        using var connection = _connectionFactory.CreateOpenConnection();

        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(*) FROM Product_Master
            WHERE internal_barcode = $internalBarcode
              AND ($excludeProductId IS NULL OR product_id != $excludeProductId);
            """;
        command.Parameters.AddWithValue("$internalBarcode", internalBarcode);
        command.Parameters.AddWithValue("$excludeProductId", (object?)excludeProductId ?? DBNull.Value);

        var count = (long)(await command.ExecuteScalarAsync())!;
        return count > 0;
    }

    public async Task InsertAsync(Product product)
    {
        using var connection = _connectionFactory.CreateOpenConnection();

        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Product_Master
                (product_id, barcode, internal_barcode, product_name, generic_name,
                 strength, unit, manufacturer, country_of_origin, cost_price,
                 selling_price, safety_stock_level, status, created_at,
                 atc_code, is_combination, units_per_box, unit_selling_price, category)
            VALUES
                ($productId, $barcode, $internalBarcode, $productName, $genericName,
                 $strength, $unit, $manufacturer, $countryOfOrigin, $costPrice,
                 $sellingPrice, $safetyStockLevel, $status, $createdAt,
                 $atcCode, $isCombination, $unitsPerBox, $unitSellingPrice, $category);
            """;
        AddProductParameters(command, product);
        await command.ExecuteNonQueryAsync();
    }

    public async Task UpdateAsync(Product product)
    {
        using var connection = _connectionFactory.CreateOpenConnection();

        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE Product_Master
            SET barcode = $barcode,
                internal_barcode = $internalBarcode,
                product_name = $productName,
                generic_name = $genericName,
                strength = $strength,
                unit = $unit,
                manufacturer = $manufacturer,
                country_of_origin = $countryOfOrigin,
                cost_price = $costPrice,
                selling_price = $sellingPrice,
                safety_stock_level = $safetyStockLevel,
                status = $status,
                atc_code = $atcCode,
                is_combination = $isCombination,
                units_per_box = $unitsPerBox,
                unit_selling_price = $unitSellingPrice,
                category = $category
            WHERE product_id = $productId;
            """;
        AddProductParameters(command, product);
        await command.ExecuteNonQueryAsync();
    }

    public async Task DeactivateAsync(string productId)
    {
        using var connection = _connectionFactory.CreateOpenConnection();

        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE Product_Master
            SET status = $status
            WHERE product_id = $productId;
            """;
        command.Parameters.AddWithValue("$status", EntityStatus.Inactive.ToString());
        command.Parameters.AddWithValue("$productId", productId);
        await command.ExecuteNonQueryAsync();
    }

    private static void AddProductParameters(SqliteCommand command, Product product)
    {
        command.Parameters.AddWithValue("$productId", product.ProductId);
        command.Parameters.AddWithValue("$barcode", (object?)product.Barcode ?? DBNull.Value);
        command.Parameters.AddWithValue("$internalBarcode", (object?)product.InternalBarcode ?? DBNull.Value);
        command.Parameters.AddWithValue("$productName", product.ProductName);
        command.Parameters.AddWithValue("$genericName", (object?)product.GenericName ?? DBNull.Value);
        command.Parameters.AddWithValue("$strength", (object?)product.Strength ?? DBNull.Value);
        command.Parameters.AddWithValue("$unit", product.Unit);
        command.Parameters.AddWithValue("$manufacturer", (object?)product.Manufacturer ?? DBNull.Value);
        command.Parameters.AddWithValue("$countryOfOrigin", (object?)product.CountryOfOrigin ?? DBNull.Value);
        command.Parameters.AddWithValue("$costPrice", product.CostPrice);
        command.Parameters.AddWithValue("$sellingPrice", product.SellingPrice);
        command.Parameters.AddWithValue("$safetyStockLevel", product.SafetyStockLevel);
        command.Parameters.AddWithValue("$status", product.Status.ToString());
        command.Parameters.AddWithValue("$createdAt", product.CreatedAt);
        command.Parameters.AddWithValue("$atcCode", (object?)product.AtcCode ?? DBNull.Value);
        command.Parameters.AddWithValue("$isCombination", product.IsCombination ? 1 : 0);
        command.Parameters.AddWithValue("$unitsPerBox", product.UnitsPerBox);
        command.Parameters.AddWithValue("$unitSellingPrice", (object?)product.UnitSellingPrice ?? DBNull.Value);
        command.Parameters.AddWithValue("$category", (object?)product.Category?.ToString() ?? DBNull.Value);
    }

    private static Product MapToProduct(SqliteDataReader reader)
    {
        return new Product
        {
            ProductId = reader.GetString(0),
            Barcode = reader.IsDBNull(1) ? null : reader.GetString(1),
            InternalBarcode = reader.IsDBNull(2) ? null : reader.GetString(2),
            ProductName = reader.GetString(3),
            GenericName = reader.IsDBNull(4) ? null : reader.GetString(4),
            Strength = reader.IsDBNull(5) ? null : reader.GetString(5),
            Unit = reader.GetString(6),
            Manufacturer = reader.IsDBNull(7) ? null : reader.GetString(7),
            CountryOfOrigin = reader.IsDBNull(8) ? null : reader.GetString(8),
            CostPrice = (decimal)reader.GetDouble(9),
            SellingPrice = (decimal)reader.GetDouble(10),
            SafetyStockLevel = reader.GetInt32(11),
            Status = Enum.Parse<EntityStatus>(reader.GetString(12)),
            CreatedAt = reader.GetInt64(13),
            AtcCode = reader.IsDBNull(14) ? null : reader.GetString(14),
            IsCombination = reader.GetInt32(15) != 0,
            UnitsPerBox = reader.GetInt32(16),
            UnitSellingPrice = reader.IsDBNull(17) ? null : (decimal)reader.GetDouble(17),
            // 알 수 없는 값(자유 입력이던 시절의 잔재 등)은 "정하지 않음"으로 읽는다.
            // 여기서 예외를 던지면 상품 목록 전체가 열리지 않는다.
            Category = reader.IsDBNull(18) || !Enum.TryParse<ProductCategory>(reader.GetString(18), out var category)
                ? null
                : category
        };
    }
}