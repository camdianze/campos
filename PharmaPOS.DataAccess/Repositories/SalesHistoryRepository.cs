using Microsoft.Data.Sqlite;
using PharmaPOS.Application.Inventory;
using PharmaPOS.Application.Repositories;
using PharmaPOS.DataAccess.Database;

namespace PharmaPOS.DataAccess.Repositories;

/// <summary>
/// ISalesHistoryRepository의 SQLite 구현체.
/// </summary>
public class SalesHistoryRepository : ISalesHistoryRepository
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public SalesHistoryRepository(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<SalesHistoryLineItem>> SearchAsync(
        string facilityId,
        long? dateFromUtc,
        long? dateToUtc,
        string searchTerm,
        string? paymentMethod)
    {
        using var connection = _connectionFactory.CreateOpenConnection();

        using var command = connection.CreateCommand();

        // 환불 행도 함께 보여 준다. 판매만 늘어놓으면 이미 취소된 판매가 그대로 남아
        // 목록 합계가 실제로 들어온 돈과 어긋나 보인다.
        var whereClauses = new List<string>
        {
            "st.facility_id = $facilityId",
            "st.transaction_type IN ('StockOut', 'Refund')"
        };
        command.Parameters.AddWithValue("$facilityId", facilityId);

        if (dateFromUtc is not null)
        {
            whereClauses.Add("st.transaction_time >= $dateFrom");
            command.Parameters.AddWithValue("$dateFrom", dateFromUtc.Value);
        }

        if (dateToUtc is not null)
        {
            whereClauses.Add("st.transaction_time <= $dateTo");
            command.Parameters.AddWithValue("$dateTo", dateToUtc.Value);
        }

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

        if (!string.IsNullOrWhiteSpace(paymentMethod))
        {
            whereClauses.Add("st.payment_method = $paymentMethod");
            command.Parameters.AddWithValue("$paymentMethod", paymentMethod);
        }

        command.CommandText = $"""
            SELECT st.transaction_id, st.product_id,
                   COALESCE(p.product_name, st.product_id) AS product_name,
                   st.batch_number, st.quantity, st.selling_price_at_transaction,
                   st.total_amount, st.payment_method, st.user_id,
                   COALESCE(u.username, st.user_id) AS username,
                   st.transaction_time, st.transaction_type,
                   COALESCE((SELECT -SUM(r.quantity)
                             FROM Stock_Transaction r
                             WHERE r.related_transaction_id = st.transaction_id
                               AND r.transaction_type = 'Refund'), 0) AS refunded_quantity
            FROM Stock_Transaction st
            LEFT JOIN Product_Master p ON p.product_id = st.product_id
            LEFT JOIN Users u ON u.user_id = st.user_id
            WHERE {string.Join(" AND ", whereClauses)}
            ORDER BY st.transaction_time DESC;
            """;

        using var reader = await command.ExecuteReaderAsync();

        var results = new List<SalesHistoryLineItem>();
        while (await reader.ReadAsync())
        {
            results.Add(MapToLineItem(reader));
        }

        return results;
    }

    public async Task<IReadOnlyList<SalesHistoryLineItem>> GetTransactionGroupAsync(
        string facilityId, long transactionTime, string userId)
    {
        using var connection = _connectionFactory.CreateOpenConnection();

        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT st.transaction_id, st.product_id,
                   COALESCE(p.product_name, st.product_id) AS product_name,
                   st.batch_number, st.quantity, st.selling_price_at_transaction,
                   st.total_amount, st.payment_method, st.user_id,
                   COALESCE(u.username, st.user_id) AS username,
                   st.transaction_time, st.transaction_type,
                   COALESCE((SELECT -SUM(r.quantity)
                             FROM Stock_Transaction r
                             WHERE r.related_transaction_id = st.transaction_id
                               AND r.transaction_type = 'Refund'), 0) AS refunded_quantity
            FROM Stock_Transaction st
            LEFT JOIN Product_Master p ON p.product_id = st.product_id
            LEFT JOIN Users u ON u.user_id = st.user_id
            WHERE st.facility_id = $facilityId
              AND st.transaction_type = 'StockOut'
              AND st.transaction_time = $transactionTime
              AND st.user_id = $userId
            ORDER BY p.product_name;
            """;
        command.Parameters.AddWithValue("$facilityId", facilityId);
        command.Parameters.AddWithValue("$transactionTime", transactionTime);
        command.Parameters.AddWithValue("$userId", userId);

        using var reader = await command.ExecuteReaderAsync();

        var results = new List<SalesHistoryLineItem>();
        while (await reader.ReadAsync())
        {
            results.Add(MapToLineItem(reader));
        }

        return results;
    }

    private static SalesHistoryLineItem MapToLineItem(SqliteDataReader reader)
    {
        return new SalesHistoryLineItem
        {
            TransactionId = reader.GetString(0),
            ProductId = reader.GetString(1),
            ProductName = reader.GetString(2),
            BatchNumber = reader.GetString(3),
            Quantity = reader.GetInt32(4),
            UnitPrice = (decimal)reader.GetDouble(5),
            LineTotal = (decimal)reader.GetDouble(6),
            PaymentMethod = reader.GetString(7),
            UserId = reader.GetString(8),
            Username = reader.GetString(9),
            TransactionTime = reader.GetInt64(10),
            TransactionType = reader.GetString(11),
            RefundedQuantity = reader.GetInt32(12)
        };
    }
}