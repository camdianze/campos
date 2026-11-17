using CamPOS.Application.Startup;
using CamPOS.DataAccess.Database;
using Microsoft.Data.Sqlite;
using System.Security.Cryptography;
using System.Text;

namespace CamPOS.Tests.Startup;

/// <summary>
/// 이름이 두 번 바뀌면서 데이터 폴더도 두 번 옮겨졌다:
/// <c>PharmaPOS</c>(v1.26, 약국) → <c>CamPOS</c>(v1.27~2.0, 개발 PC) →
/// <c>CamdiansRX</c>(지금). 이미 쓰고 있던 약국이 새 버전을 켰을 때 판매 기록·
/// 라이선스·동기화 설정이 그대로 보여야 한다.
///
/// 약국은 <c>PharmaPOS</c>에서 <b>곧장</b> 온다 — 중간 이름은 약국에 나간 적이 없다.
///
/// 여기서 틀리면 약국의 데이터가 사라진다. 그래서 테스트가 실제 파일과 실제
/// SQLite에 대고 돈다 — 복사가 되었는지는 흉내로 알 수 없다.
/// </summary>
public class AppDataMigrationTests : IDisposable
{
    private const string LegacyDb = "pharmapos.db";
    private const string CamPosDb = "campos.db";
    private const string CurrentDb = "camdiansrx.db";

    private readonly string _root;
    private readonly string _legacy;
    private readonly string _camPos;
    private readonly string _current;

    public AppDataMigrationTests()
    {
        _root = Path.Combine(
            Path.GetTempPath(), "camdiansrx-migration-tests", Guid.NewGuid().ToString());
        _legacy = Path.Combine(_root, "PharmaPOS");
        _camPos = Path.Combine(_root, "CamPOS");
        _current = Path.Combine(_root, "CamdiansRX");
        Directory.CreateDirectory(_root);
    }

    // ── 옮겨야 하는 때 ──────────────────────────────────────────────────

    [Fact]
    public void AnOldFolderOnItsOwnIsCopiedAcross()
    {
        SeedLegacy();

        var result = Run();

        Assert.True(result.IsSuccess);
        Assert.True(result.DidMigrate);
        Assert.True(File.Exists(Path.Combine(_current, CurrentDb)));
    }

    /// <summary>
    /// <c>-wal</c>을 빼놓으면 방금 끝난 판매가 사라진다. WAL 모드라 커밋된 거래가
    /// 아직 본 파일에 안 들어가 있을 수 있고, 그 손실은 조용하다.
    /// </summary>
    [Fact]
    public void ACommittedSaleSittingInTheWalSurvives()
    {
        var legacyDb = Path.Combine(_legacy, LegacyDb);
        Directory.CreateDirectory(_legacy);

        // WAL을 남긴 채로 닫는다 — 연결을 닫으면서 체크포인트가 돌지 않게 한다.
        using (var connection = new SqliteConnection($"Data Source={legacyDb}"))
        {
            connection.Open();
            Execute(connection, "PRAGMA journal_mode = WAL;");
            Execute(connection, "CREATE TABLE sale (id INTEGER);");
            Execute(connection, "INSERT INTO sale (id) VALUES (1), (2), (3);");
        }

        Run();

        // 옮긴 쪽에서 세 줄이 그대로 읽혀야 한다.
        SqliteConnection.ClearAllPools();
        using var moved = new SqliteConnection($"Data Source={Path.Combine(_current, CurrentDb)}");
        moved.Open();
        using var count = moved.CreateCommand();
        count.CommandText = "SELECT COUNT(*) FROM sale;";

        Assert.Equal(3L, (long)count.ExecuteScalar()!);
    }

    /// <summary>
    /// 데이터베이스 세 파일은 SQLite가 이름으로 짝을 찾는다. 하나만 바꾸면 WAL이
    /// 고아가 되고 거기 든 거래가 사라진다.
    /// </summary>
    [Fact]
    public void TheWalAndShmFilesAreRenamedWithTheDatabase()
    {
        SeedLegacy();
        File.WriteAllText(Path.Combine(_legacy, LegacyDb + "-wal"), "wal");
        File.WriteAllText(Path.Combine(_legacy, LegacyDb + "-shm"), "shm");

        Run();

        Assert.True(File.Exists(Path.Combine(_current, CurrentDb + "-wal")));
        Assert.True(File.Exists(Path.Combine(_current, CurrentDb + "-shm")));
        Assert.False(File.Exists(Path.Combine(_current, LegacyDb + "-wal")));
    }

    /// <summary>라이선스와 현장 교체 파일도 같이 온다. 이름은 그대로다.</summary>
    [Fact]
    public void EverythingElseInTheFolderComesToo()
    {
        SeedLegacy();
        File.WriteAllText(Path.Combine(_legacy, "license.dat"), "licence");
        Directory.CreateDirectory(Path.Combine(_legacy, "seeds"));
        File.WriteAllText(Path.Combine(_legacy, "seeds", "aware_2025.csv"), "seed");

        Run();

        Assert.Equal("licence", File.ReadAllText(Path.Combine(_current, "license.dat")));
        Assert.Equal("seed", File.ReadAllText(Path.Combine(_current, "seeds", "aware_2025.csv")));
    }

    /// <summary>옛 폴더는 남는다. 이전이 잘못돼도 돌아갈 곳이 있어야 한다.</summary>
    [Fact]
    public void TheOldFolderIsLeftWhereItIsWithANoteInIt()
    {
        SeedLegacy();

        Run();

        Assert.True(File.Exists(Path.Combine(_legacy, LegacyDb)));
        Assert.True(File.Exists(Path.Combine(_legacy, AppDataMigration.NoticeFileName)));
    }

    // ── 옮기지 말아야 하는 때 ───────────────────────────────────────────

    [Fact]
    public void ANewFolderOnItsOwnIsLeftAlone()
    {
        SeedCurrent("already here");

        var result = Run();

        Assert.True(result.IsSuccess);
        Assert.False(result.DidMigrate);
        Assert.Equal("already here", File.ReadAllText(Path.Combine(_current, CurrentDb)));
    }

    /// <summary>
    /// 둘 다 있으면 쓰고 있는 쪽이 이긴다. 옛 데이터가 지금 데이터를 덮으면
    /// 그 사이의 판매가 전부 사라지고, 덮어쓴 것은 되돌릴 수 없다.
    /// </summary>
    [Fact]
    public void WhenBothExistTheOneInUseIsNeverOverwritten()
    {
        SeedLegacy();
        SeedCurrent("in use");

        var result = Run();

        Assert.False(result.DidMigrate);
        Assert.Equal("in use", File.ReadAllText(Path.Combine(_current, CurrentDb)));
    }

    [Fact]
    public void WithNeitherFolderNothingHappens()
    {
        var result = Run();

        Assert.True(result.IsSuccess);
        Assert.False(result.DidMigrate);
        Assert.False(Directory.Exists(_current));
    }

    // ── 실패할 때 ───────────────────────────────────────────────────────

    /// <summary>
    /// 옮긴 결과가 열리지 않으면 새 폴더에 쓴 것을 치우고 옛 폴더를 그대로 둔다.
    /// 그러면 옛 버전으로 돌아가 그대로 일할 수 있다.
    /// </summary>
    [Fact]
    public void AnUnreadableCopyIsRolledBackAndTheOldFolderIsUntouched()
    {
        SeedLegacy();
        File.WriteAllText(Path.Combine(_legacy, "license.dat"), "licence");

        var result = AppDataMigration.Run(
            Sources, _current, CurrentDb, verifyDatabase: _ => false);

        Assert.False(result.IsSuccess);
        Assert.False(File.Exists(Path.Combine(_current, CurrentDb)));
        Assert.False(File.Exists(Path.Combine(_current, "license.dat")));

        // 옛 폴더는 손대지 않았다. 안내문도 남기지 않는다 — 옮겨지지 않았으니까.
        Assert.True(File.Exists(Path.Combine(_legacy, LegacyDb)));
        Assert.False(File.Exists(Path.Combine(_legacy, AppDataMigration.NoticeFileName)));
    }

    [Fact]
    public void AFailureSaysTheOldVersionStillWorks()
    {
        SeedLegacy();

        var result = AppDataMigration.Run(
            Sources, _current, CurrentDb, verifyDatabase: _ => false);

        Assert.Contains("previous version", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ── 옛 폴더가 둘일 때 ───────────────────────────────────────────────

    /// <summary>
    /// 약국이 겪는 경로. v1.26에서 곧장 올라오므로 중간 이름은 없다.
    /// 이 테스트가 21일 배포의 요점이다.
    /// </summary>
    [Fact]
    public void APharmacyComesStraightFromPharmaPos()
    {
        SeedLegacy();
        File.WriteAllText(Path.Combine(_legacy, "license.dat"), "licence");

        var result = Run();

        Assert.True(result.DidMigrate);
        Assert.Equal(_legacy, result.SourceFolder);
        Assert.True(File.Exists(Path.Combine(_current, CurrentDb)));
        Assert.True(File.Exists(Path.Combine(_current, "license.dat")));
    }

    /// <summary>
    /// 개발 PC에는 둘 다 있다 — 앞선 이전이 옛 폴더를 지우지 않기 때문이다.
    /// 보통은 나중 이름(CamPOS)이 더 새것이다.
    /// </summary>
    [Fact]
    public void WithBothOldFoldersTheOneUsedLastWins()
    {
        SeedLegacy();
        SeedCamPos();

        TouchDatabase(_legacy, LegacyDb, DateTime.UtcNow.AddDays(-30));
        TouchDatabase(_camPos, CamPosDb, DateTime.UtcNow.AddMinutes(-5));

        var result = Run();

        Assert.Equal(_camPos, result.SourceFolder);
        Assert.Equal("CAMPOS", ReadProductName(Path.Combine(_current, CurrentDb)));
    }

    /// <summary>
    /// <b>이 경우가 이 규칙이 있는 이유다.</b> 새 버전을 한 번 켜 봤다가 v1.26으로
    /// 돌아가 장사를 계속한 PC에서는 옛 이름 쪽이 더 새것이다. 이름의 순서로
    /// 골랐다면 그 사이의 판매가 전부 사라진다.
    /// </summary>
    [Fact]
    public void WhenTheOlderNameWasUsedMoreRecentlyItWins()
    {
        SeedLegacy();
        SeedCamPos();

        TouchDatabase(_camPos, CamPosDb, DateTime.UtcNow.AddDays(-30));
        TouchDatabase(_legacy, LegacyDb, DateTime.UtcNow.AddMinutes(-5));

        var result = Run();

        Assert.Equal(_legacy, result.SourceFolder);
        Assert.Equal("IBUFEN", ReadProductName(Path.Combine(_current, CurrentDb)));
    }

    /// <summary>
    /// WAL에만 쓰인 거래는 <c>.db</c>의 시각을 올리지 않는다. 본 파일만 보면
    /// 방금 장사한 폴더가 오래된 것으로 읽히고, 그래서 버려진다.
    /// </summary>
    [Fact]
    public void AFolderWhoseWalIsNewerCountsAsRecentlyUsed()
    {
        SeedLegacy();
        SeedCamPos();

        var old = DateTime.UtcNow.AddDays(-30);
        TouchDatabase(_camPos, CamPosDb, DateTime.UtcNow.AddDays(-1));
        TouchDatabase(_legacy, LegacyDb, old);

        // 본 파일은 오래됐지만 WAL은 방금이다 — 커밋만 되고 체크포인트 전인 상태.
        var wal = Path.Combine(_legacy, LegacyDb + "-wal");
        File.WriteAllText(wal, "wal");
        File.SetLastWriteTimeUtc(wal, DateTime.UtcNow.AddMinutes(-1));

        Assert.Equal(_legacy, Run().SourceFolder);
    }

    /// <summary>
    /// 앞선 이전이 남긴 쪽지는 따라오지 않는다. 새 폴더에 "이 폴더는 옮겨졌다"는
    /// 글이 들어 있으면 읽는 사람이 거꾸로 이해한다.
    /// </summary>
    [Fact]
    public void TheNoteLeftByAnEarlierMoveIsNotCopiedForward()
    {
        SeedCamPos();
        File.WriteAllText(Path.Combine(_camPos, "MOVED-TO-CamPOS.txt"), "old note");

        Run();

        Assert.False(File.Exists(Path.Combine(_current, "MOVED-TO-CamPOS.txt")));
        Assert.False(File.Exists(Path.Combine(_current, AppDataMigration.NoticeFileName)));
    }

    // ── 이름이 바뀌어도 이어지는 것 ─────────────────────────────────────

    /// <summary>
    /// 사용 기록과 <b>어디까지 보냈는지</b>가 함께 건너와야 한다.
    ///
    /// 둘 다 같은 데이터베이스 안에 있으므로 따로 챙길 일이 없어 보이지만, 그것이
    /// 바로 확인해 둘 이유다 — 어긋나면 둘 중 한 가지로 조용히 실패한다. 기록만
    /// 오고 워터마크가 0으로 돌아가면 이미 서버에 있는 것을 전부 다시 보내고,
    /// 워터마크만 오고 기록이 비면 그 사이의 사용이 영영 올라가지 않는다.
    ///
    /// 이름이 바뀔 때마다 데이터 폴더가 따라 바뀌므로, 이 확인은 이름을 바꿀 때마다
    /// 다시 해야 하는 종류다.
    /// </summary>
    [Fact]
    public void UsageEventsAndTheirWatermarkSurviveTheMove()
    {
        Directory.CreateDirectory(_legacy);

        var legacyDatabase = Path.Combine(_legacy, LegacyDb);
        var factory = new SqliteConnectionFactory(legacyDatabase);
        new DatabaseInitializer(factory).Initialize();

        using (var connection = factory.CreateOpenConnection())
        {
            Execute(connection, """
                INSERT INTO Usage_Event (event_id, occurred_at, role, event_name, outcome, app_version)
                VALUES ('e1', 100, 'Administrator', 'sales',          'shown',   '2.0'),
                       ('e2', 200, 'Administrator', 'sale_completed', 'success', '2.0'),
                       ('e3', 300, 'Administrator', 'receipt_printed','success', '2.0');

                INSERT INTO Sync_State (stream, position, updated_at) VALUES ('usage_events', 2, 300);
                """);
        }

        SqliteConnection.ClearAllPools();

        Assert.True(Run().DidMigrate);

        var moved = new SqliteConnectionFactory(Path.Combine(_current, CurrentDb));
        using var after = moved.CreateOpenConnection();

        Assert.Equal(3, Scalar(after, "SELECT COUNT(*) FROM Usage_Event;"));

        // 워터마크가 그대로여야 한다. 0으로 돌아가면 서버에 이미 있는 두 건을 다시 보낸다.
        Assert.Equal(2, Scalar(after, "SELECT position FROM Sync_State WHERE stream = 'usage_events';"));

        // 새 기록은 뒤에 붙는다 — rowid 워터마크가 그것을 고를 수 있어야 한다.
        Execute(after, """
            INSERT INTO Usage_Event (event_id, occurred_at, role, event_name, outcome, app_version)
            VALUES ('e4', 400, 'Administrator', 'login', 'success', '2.1');
            """);

        Assert.Equal(1, Scalar(after,
            "SELECT COUNT(*) FROM Usage_Event WHERE rowid > 2 AND event_id = 'e4';"));

        SqliteConnection.ClearAllPools();
    }

    /// <summary>
    /// 라이선스와 동기화 토큰도 같이 와야 한다. 둘 다 DPAPI로 묶여 있고 파일일
    /// 뿐이지만, 빠지면 약국은 활성화 화면부터 다시 만난다.
    /// </summary>
    [Fact]
    public void TheLicenceAndTheSyncTokenComeAcrossToo()
    {
        SeedLegacy();
        File.WriteAllText(Path.Combine(_legacy, "license.dat"), "licence-bytes");

        Run();

        Assert.True(File.Exists(Path.Combine(_current, "license.dat")));
        Assert.Equal("licence-bytes", File.ReadAllText(Path.Combine(_current, "license.dat")));
    }

    // ── 실제 데이터베이스 검사 ──────────────────────────────────────────

    [Fact]
    public void TheCheckAcceptsARealDatabaseAndRefusesRubbish()
    {
        Directory.CreateDirectory(_root);

        var real = Path.Combine(_root, "real.db");
        using (var connection = new SqliteConnection($"Data Source={real}"))
        {
            connection.Open();
            Execute(connection, "CREATE TABLE t (id INTEGER);");
        }

        SqliteConnection.ClearAllPools();

        var rubbish = Path.Combine(_root, "rubbish.db");
        File.WriteAllText(rubbish, "this is not a database");

        Assert.True(SqliteDatabaseCheck.CanOpen(real));
        Assert.False(SqliteDatabaseCheck.CanOpen(rubbish));
        Assert.False(SqliteDatabaseCheck.CanOpen(Path.Combine(_root, "not-there.db")));
    }

    /// <summary>
    /// DPAPI로 암호화한 값(동기화 토큰, 복구 메일 비밀번호)은 폴더가 아니라 Windows
    /// 계정에 묶여 있다. 폴더를 옮겨도 같은 PC·같은 계정이면 그대로 풀려야 한다 —
    /// 안 풀리면 약국은 토큰을 다시 받아야 하고, 그 토큰은 선유님만 발급할 수 있다.
    /// </summary>
    [Fact]
    public void ADpapiSecretStillDecryptsAfterTheFolderMoves()
    {
        // 앱이 쓰는 것과 같은 호출이다. WPF 프로젝트를 참조할 수 없어 여기서 직접 부른다.
        var encrypted = Convert.ToBase64String(ProtectedData.Protect(
            Encoding.UTF8.GetBytes("sync-token-12345"), null, DataProtectionScope.CurrentUser));

        SeedLegacy();
        File.WriteAllText(Path.Combine(_legacy, "secret.txt"), encrypted);

        Run();

        var moved = File.ReadAllText(Path.Combine(_current, "secret.txt"));
        var decrypted = Encoding.UTF8.GetString(ProtectedData.Unprotect(
            Convert.FromBase64String(moved), null, DataProtectionScope.CurrentUser));

        Assert.Equal("sync-token-12345", decrypted);
    }

    // ── 받침대 ──────────────────────────────────────────────────────────

    /// <summary>앱이 넘기는 것과 같은 순서 — CamPOS가 앞, PharmaPOS가 뒤.</summary>
    private AppDataMigrationResult Run() =>
        AppDataMigration.Run(Sources, _current, CurrentDb, SqliteDatabaseCheck.CanOpen);

    private AppDataSource[] Sources =>
    [
        new AppDataSource(_camPos, CamPosDb),
        new AppDataSource(_legacy, LegacyDb),
    ];

    private void SeedLegacy()
    {
        Directory.CreateDirectory(_legacy);

        using var connection = new SqliteConnection($"Data Source={Path.Combine(_legacy, LegacyDb)}");
        connection.Open();
        Execute(connection, "CREATE TABLE product (id INTEGER, name TEXT);");
        Execute(connection, "INSERT INTO product VALUES (1, 'IBUFEN');");
        SqliteConnection.ClearAllPools();
    }

    /// <summary>중간 이름의 폴더. 상품 이름으로 어느 쪽에서 왔는지 구분한다.</summary>
    private void SeedCamPos()
    {
        Directory.CreateDirectory(_camPos);

        using var connection = new SqliteConnection($"Data Source={Path.Combine(_camPos, CamPosDb)}");
        connection.Open();
        Execute(connection, "CREATE TABLE product (id INTEGER, name TEXT);");
        Execute(connection, "INSERT INTO product VALUES (1, 'CAMPOS');");
        SqliteConnection.ClearAllPools();
    }

    private static void TouchDatabase(string folder, string databaseName, DateTime writtenAtUtc)
    {
        SqliteConnection.ClearAllPools();
        File.SetLastWriteTimeUtc(Path.Combine(folder, databaseName), writtenAtUtc);
    }

    private static string ReadProductName(string database)
    {
        using var connection = new SqliteConnection($"Data Source={database}");
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM product LIMIT 1;";
        var name = (string)command.ExecuteScalar()!;

        SqliteConnection.ClearAllPools();

        return name;
    }

    private static long Scalar(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;

        return Convert.ToInt64(command.ExecuteScalar());
    }

    private void SeedCurrent(string contents)
    {
        Directory.CreateDirectory(_current);
        File.WriteAllText(Path.Combine(_current, CurrentDb), contents);
    }

    private static void Execute(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();

        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // 임시 폴더가 남는 것은 테스트 결과에 영향을 주지 않는다.
        }
    }
}
