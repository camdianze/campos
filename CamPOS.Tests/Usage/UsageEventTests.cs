using Microsoft.Data.Sqlite;
using CamPOS.Application.Repositories;
using CamPOS.Application.Sync;
using CamPOS.Application.Usage;
using CamPOS.DataAccess.Database;
using CamPOS.DataAccess.Repositories;
using CamPOS.Domain.Enums;

namespace CamPOS.Tests.Usage;

/// <summary>
/// 기능 사용 기록.
///
/// 여기서 지키는 것은 기능이 동작하는지가 아니라 <b>약국에게 한 약속</b>이다.
/// docs/usage-events-consent.md가 "무엇이 기록되고 무엇은 기록되지 않는가"를 적고
/// 약국이 그것을 읽고 서명하는데, 칸이 하나 늘거나 이름이 하나 늘면 그 문장은
/// 조용히 거짓이 된다 — 늘리는 쪽은 한 줄이고 설명을 고치는 쪽은 사람이 기억해야
/// 하는 일이라, 기억에 맡기면 반드시 어긋난다. 기록은 쌓이고 나면 되돌릴 수 없다.
/// </summary>
public class UsageEventTests : IDisposable
{
    /// <summary>
    /// 표에 있어도 되는 칸의 전부. <b>이 목록을 고치는 것은 동의서를 고치는 일이다.</b>
    /// </summary>
    private static readonly string[] AllowedColumns =
    [
        "event_id", "occurred_at", "role", "event_name", "outcome", "app_version"
    ];

    private readonly string _directory;
    private readonly SqliteConnectionFactory _connectionFactory;
    private readonly UsageEventRepository _repository;

    public UsageEventTests()
    {
        _directory = Path.Combine(
            Path.GetTempPath(), "campos-usage-tests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_directory);

        _connectionFactory = new SqliteConnectionFactory(Path.Combine(_directory, "test.db"));
        new DatabaseInitializer(_connectionFactory).Initialize();

        _repository = new UsageEventRepository(_connectionFactory);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();

        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
            // 임시 폴더라 남아도 그만이다.
        }
    }

    private List<string> ReadColumns()
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA table_info(Usage_Event);";

        using var reader = command.ExecuteReader();
        var names = new List<string>();

        while (reader.Read())
        {
            names.Add(reader.GetString(1));
        }

        return names;
    }

    private (string Name, string Outcome, string? Role, string? Version) ReadOnlyRow()
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT event_name, outcome, role, app_version FROM Usage_Event;";

        using var reader = command.ExecuteReader();
        Assert.True(reader.Read());

        var row = (reader.GetString(0), reader.GetString(1),
                   reader.IsDBNull(2) ? null : reader.GetString(2),
                   reader.IsDBNull(3) ? null : reader.GetString(3));

        Assert.False(reader.Read());

        return row;
    }

    private int CountRows()
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM Usage_Event;";

        return Convert.ToInt32(command.ExecuteScalar());
    }

    private UsageRecorder Recorder() => new(() => _repository, "1.28");

    // ── 약국에게 한 약속 ────────────────────────────────────────────────────

    /// <summary>
    /// <b>칸은 여섯 개뿐이다.</b> 이 테스트가 깨졌다면 고칠 곳은 테스트가 아니라
    /// 동의서다 — 칸을 하나 더하는 것은 약국이 서명한 문장을 바꾸는 일이고,
    /// 이미 쌓인 기록은 되돌릴 수 없다.
    /// </summary>
    [Fact]
    public void TheTableHasExactlyTheSixColumnsThePharmacyWasTold()
    {
        Assert.Equal(AllowedColumns.Order(), ReadColumns().Order());
    }

    /// <summary>
    /// 이름으로 찾는다. 환자·금액·상품·검색어를 담을 칸은 이름부터 없어야 한다.
    /// </summary>
    [Theory]
    [InlineData("product")]
    [InlineData("patient")]
    [InlineData("amount")]
    [InlineData("price")]
    [InlineData("username")]
    [InlineData("product_name")]
    [InlineData("text")]
    [InlineData("search")]
    [InlineData("user_id")]
    public void NothingIdentifyingCanBeStored(string forbidden)
    {
        Assert.DoesNotContain(
            ReadColumns(),
            column => column.Contains(forbidden, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// user_id는 특히 일부러 없다. 담으면 이미 올라가 있는 app_user와 이어져
    /// 직원 실명 단위 기록이 되는데, 성과 지표에 필요한 것은 role까지다.
    /// 속성이 없으면 실수로 채울 수도 없다 — SyncUser와 같은 방어다.
    /// </summary>
    [Fact]
    public void TheEventTypeHasNowhereToPutAPerson()
    {
        var names = typeof(UsageEvent)
            .GetProperties()
            .Select(property => property.Name)
            .ToList();

        Assert.Equal(
            new[] { "EventId", "OccurredAt", "Role", "EventName", "Outcome", "AppVersion" }.Order(),
            names.Order());
    }

    // ── 기록기 ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task ARecordedEventCarriesTheRoleAndVersion()
    {
        var recorder = Recorder();
        recorder.StartSession(UserRole.FacilityStaff);

        await recorder.RecordAsync(UsageEventNames.SaleCompleted, UsageOutcome.Success);

        Assert.Equal(("sale_completed", "success", "FacilityStaff", "1.28"), ReadOnlyRow());
    }

    /// <summary>
    /// 로그인 전에는 역할이 없고, 그것이 사실이다. "Unknown" 같은 글자를 넣으면
    /// 집계에서 진짜 역할처럼 한 칸을 차지한다.
    /// </summary>
    [Fact]
    public async Task BeforeAnyoneLogsInTheRoleIsEmpty()
    {
        await Recorder().RecordAsync(UsageEventNames.AppStarted, UsageOutcome.Success);

        Assert.Null(ReadOnlyRow().Role);
    }

    [Fact]
    public async Task LoggingOutForgetsTheRole()
    {
        var recorder = Recorder();
        recorder.StartSession(UserRole.Administrator);
        recorder.EndSession();

        await recorder.RecordAsync(UsageEventNames.AppStarted, UsageOutcome.Success);

        Assert.Null(ReadOnlyRow().Role);
    }

    /// <summary>
    /// 목록에 없는 이름은 적지 않는다. 호출부가 문자열을 지어낼 수 있으면 동의서에
    /// 적힌 목록은 지켜지는지 확인할 수 없는 약속이 된다.
    /// </summary>
    [Theory]
    [InlineData("something_else", UsageOutcome.Success)]
    [InlineData("", UsageOutcome.Success)]
    [InlineData(UsageEventNames.Sales, "maybe")]
    public async Task AnUnknownNameOrOutcomeIsNotWritten(string eventName, string outcome)
    {
        await Recorder().RecordAsync(eventName, outcome);

        Assert.Equal(0, CountRows());
    }

    /// <summary>
    /// <b>이 테스트가 이 기능에서 제일 중요하다.</b> 기록은 판매의 부산물이지
    /// 판매의 일부가 아니다 — 디스크가 차서 기록을 못 남긴 것이 계산대에서 판매를
    /// 막는 일은 있을 수 없다.
    /// </summary>
    [Fact]
    public async Task AStorageFailureNeverReachesTheCaller()
    {
        var recorder = new UsageRecorder(() => new ThrowingRepository(), "1.28");

        // 던지면 이 줄에서 테스트가 실패한다.
        await recorder.RecordAsync(UsageEventNames.SaleCompleted, UsageOutcome.Success);
    }

    [Fact]
    public async Task AMissingRepositoryIsAlsoSwallowed()
    {
        var recorder = new UsageRecorder(
            () => throw new InvalidOperationException("no repository"), "1.28");

        await recorder.RecordAsync(UsageEventNames.Login, UsageOutcome.Success);
    }

    // ── 목록 자체 ───────────────────────────────────────────────────────────

    /// <summary>
    /// 상수로 적어 둔 이름이 전부 All에 들어 있어야 한다. 빠지면 그 기능은 기록기가
    /// 걸러 버려 지표에서 통째로 사라지는데, 화면에도 서버에도 아무 표시가 없다.
    /// </summary>
    [Fact]
    public void EveryDeclaredNameIsInTheAllowedSet()
    {
        var declared = typeof(UsageEventNames)
            .GetFields()
            .Where(field => field.IsLiteral && field.FieldType == typeof(string))
            .Select(field => (string)field.GetRawConstantValue()!)
            .ToList();

        Assert.NotEmpty(declared);
        Assert.All(declared, name => Assert.Contains(name, UsageEventNames.All));
        Assert.Equal(declared.Count, UsageEventNames.All.Count);
    }

    /// <summary>
    /// 이름은 영문 snake_case로 고정이다. 서버 쿼리와 동의서가 이 글자 그대로를
    /// 적으므로, 대문자나 공백이 섞이면 양쪽이 조용히 어긋난다.
    /// </summary>
    [Fact]
    public void NamesAreLowerCaseSnakeCase()
    {
        Assert.All(UsageEventNames.All, name =>
            Assert.Matches("^[a-z][a-z0-9_]*$", name));
    }

    // ── 전송 ────────────────────────────────────────────────────────────────

    /// <summary>
    /// 적힌 줄이 실제로 동기화에 실린다. 기록만 되고 올라가지 않으면 PC 안에서만
    /// 쌓이는데, 그 상태는 화면에도 서버에도 표시가 없다.
    /// </summary>
    [Fact]
    public async Task ARecordedEventIsPickedUpBySync()
    {
        var recorder = Recorder();
        recorder.StartSession(UserRole.Administrator);
        await recorder.RecordAsync(UsageEventNames.Reports, UsageOutcome.Shown);

        var sync = new SyncRepository(_connectionFactory);
        var rows = await sync.GetUsageEventsAfterAsync(afterRowId: 0, limit: 500);

        var row = Assert.Single(rows).Row;

        Assert.Equal("reports", row.EventName);
        Assert.Equal("shown", row.Outcome);
        Assert.Equal("Administrator", row.Role);
        Assert.Equal("1.28", row.AppVersion);
    }

    /// <summary>
    /// 워터마크는 rowid다. 서버가 받았다고 위치를 적으면 그 줄은 다시 가지 않는다.
    /// </summary>
    [Fact]
    public async Task AfterSavingThePositionTheRowIsNotSentAgain()
    {
        await Recorder().RecordAsync(UsageEventNames.Login, UsageOutcome.Success);

        var sync = new SyncRepository(_connectionFactory);
        var first = await sync.GetUsageEventsAfterAsync(0, 500);

        await sync.SavePositionAsync(SyncStreams.UsageEvents, first[^1].RowId);

        var again = await sync.GetUsageEventsAfterAsync(
            await sync.GetPositionAsync(SyncStreams.UsageEvents), 500);

        Assert.Empty(again);
    }

    private sealed class ThrowingRepository : IUsageEventRepository
    {
        public Task InsertAsync(UsageEvent usageEvent) =>
            throw new SqliteException("database or disk is full", 13);
    }
}
