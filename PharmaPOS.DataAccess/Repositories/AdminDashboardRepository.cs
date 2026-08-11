using PharmaPOS.Application.Repositories;
using PharmaPOS.DataAccess.Database;

namespace PharmaPOS.DataAccess.Repositories;

/// <summary>
/// IAdminDashboardRepository의 SQLite 구현체.
/// </summary>
public class AdminDashboardRepository : IAdminDashboardRepository
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public AdminDashboardRepository(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<(decimal totalAmount, int count)> GetDailySalesAsync(
        string facilityId, long todayStartUtc, long todayEndUtc)
    {
        using var connection = _connectionFactory.CreateOpenConnection();

        using var command = connection.CreateCommand();
        // 환불 행은 수량·금액이 음수라 그냥 함께 더하면 순매출이 된다.
        // 건수는 판매 행만 센다 — 환불은 판매를 되돌린 것이지 새 판매가 아니다.
        command.CommandText = """
            SELECT COALESCE(SUM(total_amount), 0),
                   COUNT(CASE WHEN transaction_type = 'StockOut' THEN 1 END)
            FROM Stock_Transaction
            WHERE facility_id = $facilityId
              AND transaction_type IN ('StockOut', 'Refund')
              AND transaction_time >= $todayStart
              AND transaction_time < $todayEnd;
            """;
        command.Parameters.AddWithValue("$facilityId", facilityId);
        command.Parameters.AddWithValue("$todayStart", todayStartUtc);
        command.Parameters.AddWithValue("$todayEnd", todayEndUtc);

        using var reader = await command.ExecuteReaderAsync();
        await reader.ReadAsync();

        var totalAmount = (decimal)reader.GetDouble(0);
        var count = reader.GetInt32(1);

        return (totalAmount, count);
    }

    public async Task<int> GetActiveProductCountAsync()
    {
        using var connection = _connectionFactory.CreateOpenConnection();

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM Product_Master WHERE status = 'Active';";

        var result = await command.ExecuteScalarAsync();
        return System.Convert.ToInt32(result);
    }

    public async Task<decimal> GetTotalInventoryValueAsync(string facilityId)
    {
        using var connection = _connectionFactory.CreateOpenConnection();

        using var command = connection.CreateCommand();
        command.CommandText = """
            -- 재고는 낱개로 세는데 cost_price는 박스 하나의 원가라, 박스당 개수로 나눠야
            -- 자산가치가 맞는다. units_per_box가 1인 상품은 나눠도 그대로다.
            SELECT COALESCE(SUM(i.current_quantity * p.cost_price / p.units_per_box), 0)
            FROM Inventory i
            JOIN Product_Master p ON p.product_id = i.product_id
            WHERE i.facility_id = $facilityId;
            """;
        command.Parameters.AddWithValue("$facilityId", facilityId);

        var result = await command.ExecuteScalarAsync();
        return (decimal)System.Convert.ToDouble(result);
    }
}