using CamPOS.Application.Updates;

namespace CamPOS.Tests.Updates;

/// <summary>
/// 업데이트를 <b>언제</b> 하느냐의 규칙.
///
/// 이 기능은 약국에게 묻지도 알리지도 않고 일어난다. 그래서 틀렸을 때 나타나는
/// 모습이 "판매 도중에 프로그램이 재시작됨"이거나 "백업 없이 교체된 뒤 새 버전이
/// 데이터를 못 읽음"이다. 둘 다 계산대에서, 고객 앞에서 일어난다.
/// </summary>
public class UpdatePolicyTests
{
    // ── 확인할 때 ───────────────────────────────────────────────────────

    [Fact]
    public void AtStartupOnAnInstalledCopyItChecks()
    {
        Assert.True(UpdatePolicy.ShouldCheck(Startup()).ShouldCheck);
    }

    /// <summary>
    /// 쓰는 중에는 확인하지 않는다. 느린 회선에서 판매 중에 대역폭을 가져가고,
    /// 그렇게 얻는 것은 몇 시간 일찍 받는 것뿐이다.
    /// </summary>
    [Fact]
    public void WhileTheAppIsInUseItNeverChecks()
    {
        var context = Startup();
        context = With(context, atStartup: false);

        Assert.False(UpdatePolicy.ShouldCheck(context).ShouldCheck);
    }

    /// <summary>
    /// 개발 중이거나 압축만 풀어 쓰는 사본에는 교체할 대상이 없다. Velopack은
    /// 이때 예외를 던지는데, 그것이 개발자의 시작을 막아서는 안 된다.
    /// </summary>
    [Fact]
    public void ACopyThatWasNotInstalledIsLeftAlone()
    {
        var context = With(Startup(), installed: false);

        var decision = UpdatePolicy.ShouldCheck(context);

        Assert.False(decision.ShouldCheck);
        Assert.Contains("not installed", decision.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void WithNoUpdateSourceItDoesNotCheck()
    {
        Assert.False(UpdatePolicy.ShouldCheck(With(Startup(), configured: false)).ShouldCheck);
    }

    // ── 적용할 때 ───────────────────────────────────────────────────────

    [Fact]
    public void AtShutdownWithADownloadAndABackupItApplies()
    {
        Assert.True(UpdatePolicy.ShouldApply(Shutdown()).ShouldApply);
    }

    /// <summary>
    /// 교체는 꺼질 때만 한다. 쓰는 중이면 판매 도중에 재시작이 걸리고, 켤 때면
    /// 켜자마자 한 번 더 꺼졌다 켜진다.
    /// </summary>
    [Fact]
    public void ItNeverAppliesWhileTheAppIsOpen()
    {
        var context = With(Shutdown(), atShutdown: false);

        Assert.False(UpdatePolicy.ShouldApply(context).ShouldApply);
    }

    /// <summary>
    /// <b>이 테스트가 이 규칙의 이유다.</b> 백업이 안 되면 미룬다. 교체 자체는
    /// 데이터베이스를 건드리지 않지만, 새 버전이 스키마를 고치는 경우 돌아갈
    /// 곳이 있어야 한다. 하루 늦게 업데이트되는 것과 돌아갈 데가 없는 것은
    /// 견줄 수 없다.
    /// </summary>
    [Fact]
    public void WithoutABackupTheUpdateIsPostponedRatherThanApplied()
    {
        var context = With(Shutdown(), backedUp: false);

        var decision = UpdatePolicy.ShouldApply(context);

        Assert.False(decision.ShouldApply);
        Assert.True(decision.IsPostponed);
        Assert.Contains("backed up", decision.Reason, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 미룬 것과 할 일이 없는 것은 다르다. 미룬 것은 기록에 남아야 다음에 왜
    /// 안 됐는지 알 수 있고, 할 일이 없는 것은 날마다 적을 이유가 없다.
    /// </summary>
    [Fact]
    public void NothingDownloadedIsNotTheSameAsPostponed()
    {
        var decision = UpdatePolicy.ShouldApply(With(Shutdown(), downloaded: false));

        Assert.False(decision.ShouldApply);
        Assert.False(decision.IsPostponed);
    }

    /// <summary>받아 둔 것이 없으면 백업도 하지 않는다 — 백업은 교체 직전의 일이다.</summary>
    [Fact]
    public void WithNothingDownloadedTheBackupStateDoesNotMatter()
    {
        var context = With(Shutdown(), downloaded: false, backedUp: false);

        Assert.False(UpdatePolicy.ShouldApply(context).IsPostponed);
    }

    // ── 채널 ────────────────────────────────────────────────────────────

    /// <summary>
    /// 읽지 못한 값은 stable로 간다. test로 떨어지면 검증 전 버전이 약국에
    /// 가는 쪽으로 실패한다.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("nonsense")]
    [InlineData("STABLE")]
    public void AnythingThatIsNotTestMeansStable(string? stored)
    {
        Assert.Equal(UpdateChannels.Stable, UpdateChannels.Normalize(stored));
    }

    [Theory]
    [InlineData("test")]
    [InlineData("TEST")]
    [InlineData("  test  ")]
    public void TestIsRecognisedHoweverItIsWritten(string stored)
    {
        Assert.Equal(UpdateChannels.Test, UpdateChannels.Normalize(stored));
    }

    [Fact]
    public void TheDefaultChannelIsStable()
    {
        Assert.Equal(UpdateChannels.Stable, UpdateChannels.Default);
    }

    // ── 받침대 ──────────────────────────────────────────────────────────

    private static UpdateContext Startup() => new()
    {
        IsAtStartup = true,
        IsUpdateSourceConfigured = true,
        IsInstalled = true,
    };

    private static UpdateContext Shutdown() => new()
    {
        IsAtShutdown = true,
        IsUpdateSourceConfigured = true,
        IsInstalled = true,
        HasDownloadedUpdate = true,
        BackupSucceeded = true,
    };

    private static UpdateContext With(
        UpdateContext context,
        bool? atStartup = null,
        bool? atShutdown = null,
        bool? configured = null,
        bool? installed = null,
        bool? downloaded = null,
        bool? backedUp = null) => new()
    {
        IsAtStartup = atStartup ?? context.IsAtStartup,
        IsAtShutdown = atShutdown ?? context.IsAtShutdown,
        IsUpdateSourceConfigured = configured ?? context.IsUpdateSourceConfigured,
        IsInstalled = installed ?? context.IsInstalled,
        HasDownloadedUpdate = downloaded ?? context.HasDownloadedUpdate,
        BackupSucceeded = backedUp ?? context.BackupSucceeded,
    };
}
