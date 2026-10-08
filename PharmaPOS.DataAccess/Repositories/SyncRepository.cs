using Microsoft.Data.Sqlite;
using PharmaPOS.Application.Repositories;
using PharmaPOS.Application.Sync;
using PharmaPOS.DataAccess.Database;

namespace PharmaPOS.DataAccess.Repositories;

/// <summary>
/// ISyncRepository의 SQLite 구현체.
///
/// 조회가 SELECT * 를 쓰지 않고 컬럼을 하나하나 적는 이유는 서수 매핑 때문만이 아니다 —
/// 보내면 안 되는 컬럼이 테이블에 생겨도 이 목록에 적지 않는 한 묶음에 들어갈 수 없다.
/// 비밀번호 해시가 Users에 있는데도 안전한 것이 그 덕분이다.
/// </summary>
public class SyncRepository : ISyncRepository
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public SyncRepository(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<SyncProduct>> GetProductsAsync()
    {
        using var connection = _connectionFactory.CreateOpenConnection();

        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT product_id, barcode, internal_barcode, unit_barcode,
                   product_name, generic_name, strength, dosage_form, unit,
                   units_per_box, sells_loose, manufacturer, country_of_origin,
                   atc_code, is_combination, category, safety_stock_level, status,
                   cost_price, selling_price, unit_selling_price,
                   selling_price_khr, cost_price_khr, unit_price_khr,
                   created_at, updated_at
            FROM Product_Master
            ORDER BY product_id;
            """;

        using var reader = await command.ExecuteReaderAsync();
        var rows = new List<SyncProduct>();

        while (await reader.ReadAsync())
        {
            rows.Add(new SyncProduct
            {
                ProductId = reader.GetString(0),
                Barcode = Text(reader, 1),
                InternalBarcode = Text(reader, 2),
                UnitBarcode = Text(reader, 3),
                ProductName = reader.GetString(4),
                GenericName = Text(reader, 5),
                Strength = Text(reader, 6),
                DosageForm = Text(reader, 7),
                Unit = reader.GetString(8),
                UnitsPerBox = reader.GetInt32(9),
                SellsLoose = !reader.IsDBNull(10) && reader.GetInt32(10) != 0,
                Manufacturer = Text(reader, 11),
                CountryOfOrigin = Text(reader, 12),
                AtcCode = Text(reader, 13),
                IsCombination = reader.GetInt32(14) != 0,
                Category = Text(reader, 15),
                SafetyStockLevel = reader.GetInt32(16),
                Status = reader.GetString(17),
                CostPrice = Money(reader, 18) ?? 0m,
                SellingPrice = Money(reader, 19) ?? 0m,
                UnitSellingPrice = Money(reader, 20),
                SellingPriceKhr = Money(reader, 21),
                CostPriceKhr = Money(reader, 22),
                UnitSellingPriceKhr = Money(reader, 23),
                CreatedAt = reader.GetInt64(24),
                UpdatedAt = reader.IsDBNull(25) ? null : reader.GetInt64(25)
            });
        }

        return rows;
    }

    public async Task<IReadOnlyList<SyncInventory>> GetInventoryAsync(string facilityId)
    {
        using var connection = _connectionFactory.CreateOpenConnection();

        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT inventory_id, product_id, batch_number, expiry_date,
                   current_quantity, box_quantity, unit_quantity, updated_at
            FROM Inventory
            WHERE facility_id = $facilityId
            ORDER BY inventory_id;
            """;
        command.Parameters.AddWithValue("$facilityId", facilityId);

        using var reader = await command.ExecuteReaderAsync();
        var rows = new List<SyncInventory>();

        while (await reader.ReadAsync())
        {
            rows.Add(new SyncInventory
            {
                InventoryId = reader.GetString(0),
                ProductId = reader.GetString(1),
                BatchNumber = reader.GetString(2),
                ExpiryDate = reader.GetInt64(3),
                CurrentQuantity = reader.GetInt32(4),
                BoxQuantity = reader.GetInt32(5),
                UnitQuantity = reader.GetInt32(6),
                UpdatedAt = reader.GetInt64(7)
            });
        }

        return rows;
    }

    public async Task<IReadOnlyList<SyncUser>> GetUsersAsync(string facilityId)
    {
        using var connection = _connectionFactory.CreateOpenConnection();

        using var command = connection.CreateCommand();

        // password_hash, 보안 질문·답, 복구 이메일은 이 목록에 없다. 테이블에는 있지만
        // 여기 적히지 않는 한 묶음에 닿을 수 없다.
        command.CommandText = """
            SELECT user_id, username, role, status
            FROM Users
            WHERE facility_id = $facilityId
            ORDER BY user_id;
            """;
        command.Parameters.AddWithValue("$facilityId", facilityId);

        using var reader = await command.ExecuteReaderAsync();
        var rows = new List<SyncUser>();

        while (await reader.ReadAsync())
        {
            rows.Add(new SyncUser
            {
                UserId = reader.GetString(0),
                Username = reader.GetString(1),
                Role = reader.GetString(2),
                Status = reader.GetString(3)
            });
        }

        return rows;
    }

    public async Task<IReadOnlyList<(long RowId, SyncTransaction Row)>> GetTransactionsAfterAsync(
        string facilityId, long afterRowId, int limit)
    {
        using var connection = _connectionFactory.CreateOpenConnection();

        using var command = connection.CreateCommand();

        // rowid로 자르고 rowid로 정렬한다. transaction_time은 담아서 보내되
        // 고르는 기준으로는 쓰지 않는다 — 입고 날짜는 사용자가 고르는 값이라
        // 지난 날짜가 들어올 수 있고, 그런 행은 시각 기준에서 영영 빠진다.
        command.CommandText = """
            SELECT rowid, transaction_id, product_id, user_id, transaction_type,
                   batch_number, expiry_date, quantity,
                   selling_price_at_transaction, total_amount, payment_method,
                   reason, related_transaction_id, stock_before, stock_after,
                   transaction_time
            FROM Stock_Transaction
            WHERE facility_id = $facilityId
              AND rowid > $afterRowId
            ORDER BY rowid
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$facilityId", facilityId);
        command.Parameters.AddWithValue("$afterRowId", afterRowId);
        command.Parameters.AddWithValue("$limit", limit);

        using var reader = await command.ExecuteReaderAsync();
        var rows = new List<(long, SyncTransaction)>();

        while (await reader.ReadAsync())
        {
            rows.Add((reader.GetInt64(0), new SyncTransaction
            {
                TransactionId = reader.GetString(1),
                ProductId = reader.GetString(2),
                UserId = reader.GetString(3),
                TransactionType = reader.GetString(4),
                BatchNumber = reader.GetString(5),
                ExpiryDate = reader.GetInt64(6),
                Quantity = reader.GetInt32(7),
                SellingPriceAtTransaction = Money(reader, 8),
                TotalAmount = Money(reader, 9),
                PaymentMethod = Text(reader, 10),
                Reason = Text(reader, 11),
                RelatedTransactionId = Text(reader, 12),
                StockBefore = reader.IsDBNull(13) ? null : reader.GetInt32(13),
                StockAfter = reader.IsDBNull(14) ? null : reader.GetInt32(14),
                TransactionTime = reader.GetInt64(15)
            }));
        }

        return rows;
    }

    public async Task<IReadOnlyList<(long RowId, SyncCounsellingLog Row)>> GetCounsellingLogsAfterAsync(
        long afterRowId, int limit)
    {
        using var connection = _connectionFactory.CreateOpenConnection();

        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT rowid, log_id, transaction_id, product_id, atc_code,
                   aware_group, printed, skip_reason, locale, source_version, created_at
            FROM Counselling_Log
            WHERE rowid > $afterRowId
            ORDER BY rowid
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$afterRowId", afterRowId);
        command.Parameters.AddWithValue("$limit", limit);

        using var reader = await command.ExecuteReaderAsync();
        var rows = new List<(long, SyncCounsellingLog)>();

        while (await reader.ReadAsync())
        {
            rows.Add((reader.GetInt64(0), new SyncCounsellingLog
            {
                LogId = reader.GetString(1),
                TransactionId = reader.GetString(2),
                ProductId = reader.GetString(3),
                AtcCode = Text(reader, 4),
                AwareGroup = reader.GetString(5),
                Printed = reader.GetInt32(6) != 0,
                SkipReason = Text(reader, 7),
                Locale = reader.GetString(8),
                SourceVersion = Text(reader, 9),
                CreatedAt = reader.GetInt64(10)
            }));
        }

        return rows;
    }

    public async Task<long> GetPositionAsync(string stream)
    {
        using var connection = _connectionFactory.CreateOpenConnection();

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT position FROM Sync_State WHERE stream = $stream;";
        command.Parameters.AddWithValue("$stream", stream);

        var result = await command.ExecuteScalarAsync();

        // 보낸 적이 없으면 0 — rowid는 1부터이므로 처음부터 전부 보낸다.
        return result is null or DBNull ? 0L : Convert.ToInt64(result);
    }

    public async Task SavePositionAsync(string stream, long position)
    {
        using var connection = _connectionFactory.CreateOpenConnection();

        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Sync_State (stream, position, updated_at)
            VALUES ($stream, $position, $updatedAt)
            ON CONFLICT(stream) DO UPDATE SET
                position = $position,
                updated_at = $updatedAt;
            """;
        command.Parameters.AddWithValue("$stream", stream);
        command.Parameters.AddWithValue("$position", position);
        command.Parameters.AddWithValue("$updatedAt", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

        await command.ExecuteNonQueryAsync();
    }

    private static string? Text(SqliteDataReader reader, int i) =>
        reader.IsDBNull(i) ? null : reader.GetString(i);

    /// <summary>REAL로 저장된 금액. 이 앱의 다른 조회와 같은 방식으로 읽는다.</summary>
    private static decimal? Money(SqliteDataReader reader, int i) =>
        reader.IsDBNull(i) ? null : (decimal)reader.GetDouble(i);
}
