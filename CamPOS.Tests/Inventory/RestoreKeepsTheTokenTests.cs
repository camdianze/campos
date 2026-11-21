using Microsoft.Data.Sqlite;
using CamPOS.Application.Settings;
using CamPOS.DataAccess.Database;
using CamPOS.DataAccess.Repositories;

namespace CamPOS.Tests.Inventory;

/// <summary>
/// 복원이 <b>이 PC의 설정</b>을 지우지 않는지.
///
/// 실제로 일어난 일을 그대로 재현한다. 새 라이선스로 활성화해 동기화 토큰을 받은 PC에
/// 옛 백업을 복원했더니 토큰이 사라졌고, 화면은 "Never sent"에 수천 행이 밀린 채로
/// 남았다. 라이선스는 멀쩡했다 — <c>license.dat</c>는 폴더의 파일이라 복원이 건드리지
/// 않는다. 데이터베이스 안에 있던 것만 날아갔고, 그 차이가 이 테스트의 주제다.
///
/// 현장에서 이것이 비싼 이유는 조용하기 때문이다. 앱은 멀쩡히 열리고 장사도 되는데
/// 서버에만 아무것도 안 들어온다. 방문을 마치고 돌아온 뒤에야 알게 된다.
///
/// 진짜 SQLite 파일에 대고 돈다 — 확인하려는 것이 파일을 통째로 덮어쓰는 동작이라
/// 가짜 저장소로는 재현할 수 없다.
/// </summary>
public class RestoreKeepsTheTokenTests : IDisposable
{
    private readonly string _directory;
    private readonly string _livePath;
    private readonly SqliteConnectionFactory _live;

    public RestoreKeepsTheTokenTests()
    {
        _directory = Path.Combine(
            Path.GetTempPath(), "camdiansrx-restore-tests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_directory);

        _livePath = Path.Combine(_directory, "live.db");
        _live = new SqliteConnectionFactory(_livePath);
        new DatabaseInitializer(_live).Initialize();
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
            // 임시 폴더가 남는 것은 결과에 영향을 주지 않는다.
        }
    }

    private static void Set(SqliteConnectionFactory factory, string key, string value)
    {
        using var connection = factory.CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO App_Setting (setting_key, setting_value, updated_at)
            VALUES ($k, $v, 0)
            ON CONFLICT(setting_key) DO UPDATE SET setting_value = excluded.setting_value;
            """;
        command.Parameters.AddWithValue("$k", key);
        command.Parameters.AddWithValue("$v", value);
        command.ExecuteNonQuery();
    }

    private string? Get(string key)
    {
        using var connection = _live.CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT setting_value FROM App_Setting WHERE setting_key = $k;";
        command.Parameters.AddWithValue("$k", key);
        return command.ExecuteScalar() as string;
    }

    /// <summary>복원해 올 백업을 하나 만든다. 그 안의 상품 이름으로 들어온 것을 가린다.</summary>
    private string MakeBackup(string productName, Action<SqliteConnectionFactory>? extra = null)
    {
        var path = Path.Combine(_directory, Guid.NewGuid().ToString("N") + ".db");
        var factory = new SqliteConnectionFactory(path);
        new DatabaseInitializer(factory).Initialize();

        using (var connection = factory.CreateOpenConnection())
        {
            using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO Product_Master
                    (product_id, product_name, unit, cost_price, selling_price,
                     safety_stock_level, status, created_at)
                VALUES ('p-backup', $name, 'tablet', 1.0, 2.0, 10, 'Active', 0);
                """;
            command.Parameters.AddWithValue("$name", productName);
            command.ExecuteNonQuery();
        }

        extra?.Invoke(factory);

        SqliteConnection.ClearAllPools();
        return path;
    }

    private string? RestoredProductName()
    {
        using var connection = _live.CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT product_name FROM Product_Master LIMIT 1;";
        return command.ExecuteScalar() as string;
    }

    // ── 본론 ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// 복원해도 토큰이 남는다. 이것 하나가 안 되면 현장에서 설치가 끝난 것처럼
    /// 보이면서 서버에는 영영 닿지 않는다.
    /// </summary>
    [Fact]
    public async Task TheDeviceTokenSurvivesARestore()
    {
        Set(_live, AppSettingKeys.SyncToken, "token-for-this-computer");

        await new BackupRepository(_live, _livePath)
            .RestoreDatabaseAsync(MakeBackup("From the backup"));

        Assert.Equal("token-for-this-computer", Get(AppSettingKeys.SyncToken));
        Assert.Equal("From the backup", RestoredProductName());
    }

    /// <summary>
    /// 백업 쪽에도 토큰이 있으면 <b>이 PC의 것</b>이 남아야 한다. 백업을 뜬 PC의
    /// 토큰을 쓰면 이 컴퓨터가 남의 약국 행세를 하게 되고, 그렇게 올라간 자료는
    /// 되돌릴 수 없다.
    /// </summary>
    [Fact]
    public async Task ThisComputersTokenBeatsTheOneInTheBackup()
    {
        Set(_live, AppSettingKeys.SyncToken, "mine");

        var backup = MakeBackup(
            "From the backup",
            f => Set(f, AppSettingKeys.SyncToken, "the-other-pharmacy"));

        await new BackupRepository(_live, _livePath).RestoreDatabaseAsync(backup);

        Assert.Equal("mine", Get(AppSettingKeys.SyncToken));
    }

    /// <summary>
    /// 목록에 있는 키는 모두 살아남는다. 토큰만 지키면 주소나 채널이 조용히
    /// 기본값으로 돌아가고, 그쪽은 티가 더 안 난다.
    /// </summary>
    [Fact]
    public async Task EveryMachineBoundSettingSurvives()
    {
        foreach (var key in MachineBoundSettings.Keys)
        {
            Set(_live, key, "kept:" + key);
        }

        await new BackupRepository(_live, _livePath)
            .RestoreDatabaseAsync(MakeBackup("From the backup"));

        foreach (var key in MachineBoundSettings.Keys)
        {
            Assert.Equal("kept:" + key, Get(key));
        }
    }

    /// <summary>
    /// 약국의 영업 설정은 <b>데이터와 함께</b> 와야 한다. 영수증에 찍히는 상호는
    /// 그 데이터의 약국 것이지 이 컴퓨터의 것이 아니다 — 지키는 목록이 넓어지면
    /// 복원이 "데이터만 바뀌고 설정은 안 바뀌는" 반쪽짜리가 된다.
    /// </summary>
    [Fact]
    public async Task ThePharmacysOwnSettingsComeFromTheBackup()
    {
        Set(_live, AppSettingKeys.ShopNameEn, "This computer's shop");

        var backup = MakeBackup(
            "From the backup",
            f => Set(f, AppSettingKeys.ShopNameEn, "The backup's shop"));

        await new BackupRepository(_live, _livePath).RestoreDatabaseAsync(backup);

        Assert.Equal("The backup's shop", Get(AppSettingKeys.ShopNameEn));
    }

    /// <summary>
    /// 저장된 적 없는 키는 되돌려 쓰지 않는다. 빈 문자열을 써 넣으면 설정 하나하나가
    /// 구분하는 "없음"과 "비우기로 했음"이 섞인다 — 영수증 주소를 지운 것과 한 번도
    /// 적지 않은 것이 같아지는 식이다.
    /// </summary>
    [Fact]
    public async Task AKeyThatWasNeverSetIsNotWrittenBack()
    {
        await new BackupRepository(_live, _livePath)
            .RestoreDatabaseAsync(MakeBackup("From the backup"));

        Assert.Null(Get(AppSettingKeys.SyncToken));
    }

    /// <summary>
    /// 워터마크는 데이터의 것이라 백업을 따라온다. 이 PC의 것을 남기면 복원해 온
    /// 행들이 "이미 보낸 것"으로 취급되어 영영 올라가지 않는다. 되감겨 다시 보내는
    /// 쪽은 안전하다 — 서버가 (site_id, 원본 ID)로 덮어쓴다.
    /// </summary>
    [Fact]
    public async Task TheWatermarkFollowsTheData()
    {
        using (var connection = _live.CreateOpenConnection())
        {
            using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO Sync_State (stream, position, updated_at)
                VALUES ('transactions', 9999, 0);
                """;
            command.ExecuteNonQuery();
        }

        await new BackupRepository(_live, _livePath)
            .RestoreDatabaseAsync(MakeBackup("From the backup"));

        using var check = _live.CreateOpenConnection();
        using var read = check.CreateCommand();
        read.CommandText = "SELECT COUNT(*) FROM Sync_State WHERE stream = 'transactions';";

        Assert.Equal(0L, (long)read.ExecuteScalar()!);
    }
}
