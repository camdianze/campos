using PharmaPOS.Application.Inventory;
using PharmaPOS.Application.Repositories;
using PharmaPOS.DataAccess.Database;

namespace PharmaPOS.DataAccess.Repositories;

/// <summary>
/// IAlertRepository의 SQLite 구현체.
/// </summary>
public class AlertRepository : IAlertRepository
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public AlertRepository(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<LowStockCandidate>> GetLowStockCandidatesAsync(string facilityId)
    {
        using var connection = _connectionFactory.CreateOpenConnection();

        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT p.product_id, p.product_name, SUM(i.current_quantity) AS total_qty, p.safety_stock_level
            FROM Product_Master p
            JOIN Inventory i ON i.product_id = p.product_id
            WHERE p.status = 'Active' AND i.facility_id = $facilityId
            GROUP BY p.product_id, p.product_name, p.safety_stock_level
            HAVING SUM(i.current_quantity) < p.safety_stock_level;
            """;
        command.Parameters.AddWithValue("$facilityId", facilityId);

        using var reader = await command.ExecuteReaderAsync();

        var results = new List<LowStockCandidate>();
        while (await reader.ReadAsync())
        {
            results.Add(new LowStockCandidate
            {
                ProductId = reader.GetString(0),
                ProductName = reader.GetString(1),
                TotalQuantity = reader.GetInt32(2),
                SafetyStockLevel = reader.GetInt32(3)
            });
        }

        return results;
    }

    public async Task<IReadOnlyList<ExpiryCandidate>> GetExpiryCandidatesAsync(string facilityId)
    {
        using var connection = _connectionFactory.CreateOpenConnection();

        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT p.product_id, p.product_name, i.batch_number, i.expiry_date, i.current_quantity
            FROM Inventory i
            JOIN Product_Master p ON p.product_id = i.product_id
            WHERE p.status = 'Active' AND i.facility_id = $facilityId
              AND i.expiry_date <= $ninetyDaysFromNow;
            """;
        command.Parameters.AddWithValue("$facilityId", facilityId);
        command.Parameters.AddWithValue("$ninetyDaysFromNow", now + 90L * 86400000L);

        using var reader = await command.ExecuteReaderAsync();

        var results = new List<ExpiryCandidate>();
        while (await reader.ReadAsync())
        {
            results.Add(new ExpiryCandidate
            {
                ProductId = reader.GetString(0),
                ProductName = reader.GetString(1),
                BatchNumber = reader.GetString(2),
                ExpiryDate = reader.GetInt64(3),
                Quantity = reader.GetInt32(4)
            });
        }

        return results;
    }
}