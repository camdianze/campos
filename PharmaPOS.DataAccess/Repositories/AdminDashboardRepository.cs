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
        command.CommandText = """
            SELECT COALESCE(SUM(total_amount), 0), COUNT(*)
            FROM Stock_Transaction
            WHERE facility_id = $facilityId
              AND transaction_type = 'StockOut'
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
            SELECT COALESCE(SUM(i.current_quantity * p.cost_price), 0)
            FROM Inventory i
            JOIN Product_Master p ON p.product_id = i.product_id
            WHERE i.facility_id = $facilityId;
            """;
        command.Parameters.AddWithValue("$facilityId", facilityId);

        var result = await command.ExecuteScalarAsync();
        return (decimal)System.Convert.ToDouble(result);
    }
}