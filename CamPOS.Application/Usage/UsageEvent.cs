namespace CamPOS.Application.Usage;

/// <summary>
/// 저장되는 한 줄. <b>여섯 칸이 전부이고, 늘리는 것은 동의서를 고치는 일이다.</b>
///
/// Domain이 아니라 Application에 두는 이유는 SyncPayload와 같다 — 이것은 약국의
/// 개념(상품·재고·거래)이 아니라 우리가 제품을 관찰하는 방식이다.
///
/// 여기 <b>없는</b> 것이 이 형식의 요점이다: 환자, 입력한 글자, 금액, 상품 이름,
/// 화면, 키 입력, 그리고 <b>누가 눌렀는지</b>. 속성이 없으면 실수로 채울 수도 없다 —
/// SyncUser가 네 칸뿐인 것과 같은 방어다.
/// </summary>
public sealed class UsageEvent
{
    public required string EventId { get; init; }

    /// <summary>PC 시계 기준 epoch 밀리초.</summary>
    public required long OccurredAt { get; init; }

    /// <summary>
    /// 그 시점의 역할. 로그인 전(<c>app_started</c>)에는 null이다.
    ///
    /// 지금의 역할을 나중에 app_user에서 읽어 오면 안 되는 이유: 직원이 승진하면
    /// 그 값이 덮어써지고, 과거 기록 전체의 역할이 조용히 바뀐다.
    /// </summary>
    public string? Role { get; init; }

    public required string EventName { get; init; }

    public required string Outcome { get; init; }

    /// <summary>"1.28"처럼 숫자만. 버전별로 기능 사용이 어떻게 달라졌는지 본다.</summary>
    public string? AppVersion { get; init; }
}
