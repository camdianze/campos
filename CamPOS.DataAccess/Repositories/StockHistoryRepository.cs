using Microsoft.Data.Sqlite;
using PharmaPOS.Application.Inventory;
using PharmaPOS.Application.Repositories;
using PharmaPOS.DataAccess.Database;

namespace PharmaPOS.DataAccess.Repositories;

/// <summary>IStockHistoryRepository의 SQLite 구현체.</summary>
public class StockHistoryRepository : IStockHistoryRepository
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public StockHistoryRepository(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<StockHistoryLineItem>> SearchAsync(
        string facilityId,
        long? dateFromUtc,
        long? dateToUtc,
        string searchTerm,
        IReadOnlyList<string> transactionTypes)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        using var command = connection.CreateCommand();

        var whereClauses = new List<string> { "st.facility_id = $facilityId" };
        command.Parameters.AddWithValue("$facilityId", facilityId);

        // 종류 목록이 비어 있으면(All) 조건을 걸지 않는다. 종류 값은 열거형에서 온
        // 고정 문자열이지만, 그래도 SQL에 이어 붙이지 않고 파라미터로 넘긴다.
        if (transactionTypes.Count > 0)
        {
            var placeholders = new List<string>();
            for (var i = 0; i < transactionTypes.Count; i++)
            {
                placeholders.Add($"$type{i}");
                command.Parameters.AddWithValue($"$type{i}", transactionTypes[i]);
            }

            whereClauses.Add($"st.transaction_type IN ({string.Join(", ", placeholders)})");
        }

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
                 OR LOWER(p.internal_barcode) LIKE LOWER($search)
                 OR LOWER(st.batch_number) LIKE LOWER($search))
                """);
            command.Parameters.AddWithValue("$search", $"%{searchTerm}%");
        }

        command.CommandText = $"""
            SELECT st.transaction_id, st.product_id,
                   COALESCE(p.product_name, st.product_id) AS product_name,
                   st.batch_number, st.expiry_date, st.quantity, st.transaction_type,
                   st.reason, st.payment_method,
                   st.stock_before, st.stock_after,
                   COALESCE(u.username, st.user_id) AS username,
                   st.transaction_time
            FROM Stock_Transaction st
            LEFT JOIN Product_Master p ON p.product_id = st.product_id
            LEFT JOIN Users u ON u.user_id = st.user_id
            WHERE {string.Join(" AND ", whereClauses)}
            ORDER BY st.transaction_time DESC;
            """;

        using var reader = await command.ExecuteReaderAsync();

        var results = new List<StockHistoryLineItem>();
        while (await reader.ReadAsync())
        {
            results.Add(new StockHistoryLineItem
            {
                TransactionId = reader.GetString(0),
                ProductId = reader.GetString(1),
                ProductName = reader.GetString(2),
                BatchNumber = reader.GetString(3),
                ExpiryDate = reader.GetInt64(4),
                Quantity = reader.GetInt32(5),
                TransactionType = reader.GetString(6),
                Reason = reader.IsDBNull(7) ? null : reader.GetString(7),
                PaymentMethod = reader.IsDBNull(8) ? null : reader.GetString(8),
                // 이 컬럼이 생기기 전의 거래는 NULL이다. 0으로 바꾸면 재고가 0이었다는 뜻이 된다.
                StockBefore = reader.IsDBNull(9) ? null : reader.GetInt64(9),
                StockAfter = reader.IsDBNull(10) ? null : reader.GetInt64(10),
                Username = reader.GetString(11),
                TransactionTime = reader.GetInt64(12)
            });
        }

        return results;
    }
}
