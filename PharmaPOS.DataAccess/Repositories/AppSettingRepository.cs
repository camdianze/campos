using PharmaPOS.Application.Repositories;
using PharmaPOS.DataAccess.Database;

namespace PharmaPOS.DataAccess.Repositories;

/// <summary>
/// IAppSettingRepository의 SQLite 구현체.
/// </summary>
public class AppSettingRepository : IAppSettingRepository
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public AppSettingRepository(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<string?> GetAsync(string key)
    {
        using var connection = _connectionFactory.CreateOpenConnection();

        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT setting_value FROM App_Setting WHERE setting_key = $key;
            """;
        command.Parameters.AddWithValue("$key", key);

        var value = await command.ExecuteScalarAsync();
        return value as string;
    }

    public async Task SetAsync(string key, string value)
    {
        using var connection = _connectionFactory.CreateOpenConnection();

        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO App_Setting (setting_key, setting_value, updated_at)
            VALUES ($key, $value, $updatedAt)
            ON CONFLICT(setting_key) DO UPDATE
                SET setting_value = excluded.setting_value,
                    updated_at    = excluded.updated_at;
            """;
        command.Parameters.AddWithValue("$key", key);
        command.Parameters.AddWithValue("$value", value);
        command.Parameters.AddWithValue("$updatedAt", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

        await command.ExecuteNonQueryAsync();
    }
}
