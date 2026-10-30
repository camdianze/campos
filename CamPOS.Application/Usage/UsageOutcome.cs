namespace CamPOS.Application.Usage;

/// <summary>
/// 한 이벤트가 어떻게 끝났는지. 네 가지뿐이다.
///
/// <b>실패는 여기에 적지, 이름으로 적지 않는다.</b> save_failed·print_failed 같은
/// 이름을 따로 두면 같은 사실을 적는 방법이 두 가지가 되어 호출부마다 갈라지고,
/// 더 나쁜 것은 그 줄이 <i>무엇이</i> 실패했는지 말하지 않는다는 점이다 —
/// "기능별 오류율"은 기능 이름과 실패가 한 줄에 같이 있어야 나오는 숫자다.
/// receipt_printed/Failure는 둘 다 말하고, print_failed는 절반만 말한다.
/// </summary>
public static class UsageOutcome
{
    public const string Success = "success";

    public const string Failure = "failure";

    /// <summary>화면이나 목록을 열었다. 성공도 실패도 아니다.</summary>
    public const string Shown = "shown";

    /// <summary>
    /// 할 수 있었는데 사람이 하지 않기로 했다. 영수증 설정이 <c>ask</c>일 때
    /// 인쇄를 거절한 경우가 이것이다 — 실패가 아니므로 오류율에 섞이면 안 된다.
    /// </summary>
    public const string Skipped = "skipped";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        Success, Failure, Shown, Skipped
    };
}
