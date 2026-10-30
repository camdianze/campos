using CamPOS.Application.Repositories;
using CamPOS.Application.Usage;
using CamPOS.DataAccess.Database;

namespace CamPOS.DataAccess.Repositories;

/// <summary>
/// <see cref="IUsageEventRepository"/>의 SQLite 구현. 한 줄 넣는 것이 전부다.
///
/// 트랜잭션을 열지 않는 이유: 한 문장뿐이라 SQLite가 알아서 하나로 처리하고,
/// 이 쓰기는 판매가 끝난 뒤에 따라붙으므로 다른 쓰기를 기다리게 만들 이유가 없다.
/// 실패하면 그대로 던진다 — 삼키는 것은 UsageRecorder의 일이다.
/// </summary>
public class UsageEventRepository : IUsageEventRepository
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public UsageEventRepository(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task InsertAsync(UsageEvent usageEvent)
    {
        using var connection = _connectionFactory.CreateOpenConnection();

        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Usage_Event
                (event_id, occurred_at, role, event_name, outcome, app_version)
            VALUES
                ($eventId, $occurredAt, $role, $eventName, $outcome, $appVersion);
            """;
        command.Parameters.AddWithValue("$eventId", usageEvent.EventId);
        command.Parameters.AddWithValue("$occurredAt", usageEvent.OccurredAt);
        command.Parameters.AddWithValue("$role", (object?)usageEvent.Role ?? DBNull.Value);
        command.Parameters.AddWithValue("$eventName", usageEvent.EventName);
        command.Parameters.AddWithValue("$outcome", usageEvent.Outcome);
        command.Parameters.AddWithValue("$appVersion", (object?)usageEvent.AppVersion ?? DBNull.Value);

        await command.ExecuteNonQueryAsync();
    }
}
