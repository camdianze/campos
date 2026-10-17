using CamPOS.Application.Startup;
using CamPOS.DataAccess.Database;
using Microsoft.Data.Sqlite;
using System.Security.Cryptography;
using System.Text;

namespace CamPOS.Tests.Startup;

/// <summary>
/// 내부 이름이 바뀌면서 데이터 폴더가 <c>%APPDATA%\PharmaPOS</c>에서
/// <c>%APPDATA%\CamPOS</c>로 옮겨졌다. 이미 쓰고 있던 약국이 새 버전을 켰을 때
/// 판매 기록·라이선스·동기화 설정이 그대로 보여야 한다.
///
/// 여기서 틀리면 약국의 데이터가 사라진다. 그래서 테스트가 실제 파일과 실제
/// SQLite에 대고 돈다 — 복사가 되었는지는 흉내로 알 수 없다.
/// </summary>
public class AppDataMigrationTests : IDisposable
{
    private const string LegacyDb = "pharmapos.db";
    private const string CurrentDb = "campos.db";

    private readonly string _root;
    private readonly string _legacy;
    private readonly string _current;

    public AppDataMigrationTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "campos-migration-tests", Guid.NewGuid().ToString());
        _legacy = Path.Combine(_root, "PharmaPOS");
        _current = Path.Combine(_root, "CamPOS");
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
            _legacy, _current, LegacyDb, CurrentDb, verifyDatabase: _ => false);

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
            _legacy, _current, LegacyDb, CurrentDb, verifyDatabase: _ => false);

        Assert.Contains("previous version", result.Message, StringComparison.OrdinalIgnoreCase);
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

    private AppDataMigrationResult Run() =>
        AppDataMigration.Run(_legacy, _current, LegacyDb, CurrentDb, SqliteDatabaseCheck.CanOpen);

    private void SeedLegacy()
    {
        Directory.CreateDirectory(_legacy);

        using var connection = new SqliteConnection($"Data Source={Path.Combine(_legacy, LegacyDb)}");
        connection.Open();
        Execute(connection, "CREATE TABLE product (id INTEGER, name TEXT);");
        Execute(connection, "INSERT INTO product VALUES (1, 'IBUFEN');");
        SqliteConnection.ClearAllPools();
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
