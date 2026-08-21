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

    public async Task<IReadOnlyDictionary<string, string>> GetManyAsync(IReadOnlyList<string> keys)
    {
        var found = new Dictionary<string, string>(StringComparer.Ordinal);

        if (keys.Count == 0)
        {
            return found;
        }

        using var connection = _connectionFactory.CreateOpenConnection();

        using var command = connection.CreateCommand();

        // IN 절을 문자열로 이어 붙이지 않고 자리표시자를 키 개수만큼 만든다.
        var placeholders = keys.Select((_, index) => "$k" + index).ToList();

        command.CommandText = $"""
            SELECT setting_key, setting_value
            FROM App_Setting
            WHERE setting_key IN ({string.Join(", ", placeholders)});
            """;

        for (var index = 0; index < keys.Count; index++)
        {
            command.Parameters.AddWithValue(placeholders[index], keys[index]);
        }

        using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            found[reader.GetString(0)] = reader.GetString(1);
        }

        return found;
    }

    public async Task SetAsync(string key, string value, string? valueType = null, string? updatedBy = null)
    {
        using var connection = _connectionFactory.CreateOpenConnection();

        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO App_Setting (setting_key, setting_value, value_type, updated_at, updated_by)
            VALUES ($key, $value, $valueType, $updatedAt, $updatedBy)
            ON CONFLICT(setting_key) DO UPDATE
                SET setting_value = excluded.setting_value,
                    value_type    = excluded.value_type,
                    updated_at    = excluded.updated_at,
                    updated_by    = excluded.updated_by;
            """;
        command.Parameters.AddWithValue("$key", key);
        command.Parameters.AddWithValue("$value", value);
        command.Parameters.AddWithValue("$valueType", (object?)valueType ?? DBNull.Value);
        command.Parameters.AddWithValue("$updatedAt", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        command.Parameters.AddWithValue("$updatedBy", (object?)updatedBy ?? DBNull.Value);

        await command.ExecuteNonQueryAsync();
    }
}
