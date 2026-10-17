namespace CamPOS.Application.Sync;

/// <summary>
/// "동기화가 제대로 돌고 있나"에 대한 한 번의 판단. 로그인할 때 한 번 읽어
/// 상단 바 배지를 띄울지 정한다 — 라이선스 만료 배지와 같은 자리, 같은 방식이다.
///
/// <b>자동 전송의 진짜 위험은 조용히 멈추는 것</b>이다. 인터넷이 끊기거나 토큰이
/// 해지되면 앱은 평소와 똑같이 동작하고 서버에만 아무것도 안 들어온다. 그리고
/// 그것을 알아차릴 사람은 약국이 아니라 이 앱을 만든 쪽인데, 그 사람은 그 PC
/// 앞에 없다. 배지는 약국이 연락할 수 있게 만드는 장치다.
///
/// 꺼 둔 것과 고장난 것을 <b>다른 문구로</b> 말하는 것이 이 타입의 핵심이다.
/// 꺼 둔 상태를 "실패"로 보고하면 엉뚱한 곳을 찾게 되고, 아무 말도 안 하면
/// 꺼 둔 사실을 잊는다.
/// </summary>
public sealed class SyncHealth
{
    /// <summary>이만큼 성공이 없으면 상단 바에 띄운다.</summary>
    public const int WarningDays = 3;

    private SyncHealth(bool configured, bool automatic, DateTimeOffset? lastSuccess, int pending)
    {
        IsConfigured = configured;
        IsAutomatic = automatic;
        LastSuccess = lastSuccess;
        PendingRows = pending;
    }

    public bool IsConfigured { get; }

    public bool IsAutomatic { get; }

    public DateTimeOffset? LastSuccess { get; }

    public int PendingRows { get; }

    public int? DaysSinceLastSuccess => LastSuccess is null
        ? null
        : (int)(DateTimeOffset.UtcNow - LastSuccess.Value).TotalDays;

    /// <summary>
    /// 서버를 안 쓰는 설치에서는 배지를 띄우지 않는다. 동기화를 설정한 적이 없다면
    /// 안 되고 있는 것이 정상이고, 그 약국에 보여 줄 문제가 아니다.
    /// </summary>
    public bool ShouldWarn => IsConfigured && (!IsAutomatic || IsStale);

    private bool IsStale =>
        PendingRows != 0 && (LastSuccess is null || DaysSinceLastSuccess >= WarningDays);

    /// <summary>상단 바에 적을 한 줄. 사용자가 보는 글자이므로 영어다.</summary>
    public string WarningText
    {
        get
        {
            if (!ShouldWarn)
            {
                return string.Empty;
            }

            if (!IsAutomatic)
            {
                return "Automatic sync is off";
            }

            return DaysSinceLastSuccess switch
            {
                null => "Never synced to the server",
                1 => "Not synced for a day",
                { } days => $"Not synced for {days} days",
            };
        }
    }

    public string WarningTooltip => !IsAutomatic
        ? "Data is not being sent to the server. Turn Auto back on in Admin Dashboard → Data Center, or press Synchronize there."
        : "The server has not received this computer's data recently. Check the internet connection, then press Synchronize in Admin Dashboard → Data Center.";

    public static SyncHealth Of(
        bool configured, bool automatic, DateTimeOffset? lastSuccess, int pendingRows) =>
        new(configured, automatic, lastSuccess, pendingRows);

    /// <summary>상태를 읽지 못했을 때. 읽기 실패로 약국에 경고를 띄우지는 않는다.</summary>
    public static SyncHealth Unknown() => new(false, true, null, 0);
}
