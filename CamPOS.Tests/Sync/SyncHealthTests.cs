using PharmaPOS.Application.Sync;

namespace PharmaPOS.Tests.Sync;

/// <summary>
/// 상단 바 배지를 띄울지 정하는 판단.
///
/// 자동 전송의 위험은 실패가 아니라 <b>조용한</b> 실패다. 인터넷이 끊기거나
/// 토큰이 해지되면 앱은 평소와 똑같이 동작하고 서버에만 아무것도 안 들어온다.
/// 배지는 약국이 그것을 알고 연락할 수 있게 만드는 유일한 자리라서, 언제
/// 뜨고 언제 안 뜨는지가 규칙이다.
/// </summary>
public class SyncHealthTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    // ── 띄우지 않아야 하는 때 ───────────────────────────────────────────

    /// <summary>
    /// 서버를 안 쓰는 설치에서는 동기화가 안 되는 것이 정상이다. 그 약국에
    /// 보여 줄 문제가 아니다.
    /// </summary>
    [Fact]
    public void AComputerThatWasNeverConnectedShowsNothing()
    {
        var health = SyncHealth.Of(configured: false, automatic: true, lastSuccess: null, pendingRows: 500);

        Assert.False(health.ShouldWarn);
        Assert.Equal(string.Empty, health.WarningText);
    }

    [Fact]
    public void NothingIsShownWhileSyncIsKeepingUp()
    {
        var health = SyncHealth.Of(true, true, Now.AddMinutes(-20), pendingRows: 0);

        Assert.False(health.ShouldWarn);
    }

    /// <summary>
    /// 보낼 것이 없으면 며칠이 지나도 문제가 아니다. 판매가 없던 연휴에 배지가
    /// 뜨면, 그 배지는 다음에 진짜 문제가 생겼을 때 무시된다.
    /// </summary>
    [Fact]
    public void AQuietWeekWithNothingToSendIsNotAProblem()
    {
        var health = SyncHealth.Of(true, true, Now.AddDays(-10), pendingRows: 0);

        Assert.False(health.ShouldWarn);
    }

    // ── 띄워야 하는 때 ──────────────────────────────────────────────────

    /// <summary>
    /// 꺼 둔 것과 고장난 것은 <b>다른 문구</b>여야 한다. 꺼 둔 상태를 "실패"로
    /// 보고하면 회선과 토큰을 뒤지게 되고, 아무 말도 안 하면 꺼 둔 사실을 잊는다.
    /// </summary>
    [Fact]
    public void SwitchedOffSaysSoRatherThanLookingLikeAFault()
    {
        var health = SyncHealth.Of(true, automatic: false, lastSuccess: Now, pendingRows: 0);

        Assert.True(health.ShouldWarn);
        Assert.Equal("Automatic sync is off", health.WarningText);
        Assert.Contains("Turn Auto back on", health.WarningTooltip, StringComparison.Ordinal);
    }

    /// <summary>꺼 둔 상태는 방금 보냈어도 말한다 — 끈 사실 자체가 알려야 할 것이다.</summary>
    [Fact]
    public void SwitchedOffIsShownEvenRightAfterAManualSend()
    {
        Assert.True(SyncHealth.Of(true, false, Now, 0).ShouldWarn);
    }

    [Theory]
    [InlineData(3, "Not synced for 3 days")]
    [InlineData(9, "Not synced for 9 days")]
    public void ABacklogThatHasNotMovedForDaysIsShown(int days, string expected)
    {
        var health = SyncHealth.Of(true, true, Now.AddDays(-days).AddMinutes(-1), pendingRows: 7);

        Assert.True(health.ShouldWarn);
        Assert.Equal(expected, health.WarningText);
    }

    [Fact]
    public void TwoDaysIsNotYetWorthInterrupting()
    {
        Assert.False(SyncHealth.Of(true, true, Now.AddDays(-2), pendingRows: 7).ShouldWarn);
    }

    /// <summary>설정은 했는데 한 번도 성공한 적이 없는 상태.</summary>
    [Fact]
    public void ConnectedButNeverSyncedIsShown()
    {
        var health = SyncHealth.Of(true, true, lastSuccess: null, pendingRows: 7);

        Assert.True(health.ShouldWarn);
        Assert.Equal("Never synced to the server", health.WarningText);
    }

    /// <summary>상태를 못 읽은 것으로 약국에 경고를 띄우지는 않는다.</summary>
    [Fact]
    public void AnUnreadableStateShowsNothing()
    {
        Assert.False(SyncHealth.Unknown().ShouldWarn);
    }

    [Fact]
    public void WarningTextIsEmptyWheneverNothingIsShown()
    {
        var quiet = SyncHealth.Of(true, true, Now, 0);

        Assert.False(quiet.ShouldWarn);
        Assert.Equal(string.Empty, quiet.WarningText);
    }
}
