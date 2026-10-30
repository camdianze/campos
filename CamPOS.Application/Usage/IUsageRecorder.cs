using CamPOS.Domain.Enums;

namespace CamPOS.Application.Usage;

/// <summary>
/// 기능 사용 기록을 남기는 단 하나의 입구. ViewModel은 이것만 부른다.
///
/// <b>이 인터페이스는 절대 던지지 않는다.</b> 기록은 판매의 부산물이지 판매의 일부가
/// 아니므로, 디스크가 차서 기록을 못 남긴 것이 계산대에서 판매를 막는 일은 있을 수
/// 없다. 그래서 반환값도 없다 — 호출부가 확인할 수 있으면 언젠가 확인하게 되고,
/// 확인하면 분기가 생기고, 분기가 생기면 판매가 기록에 매달린다.
/// 그 규칙이 Application에 있는 이유도 같다. 테스트가 잡을 수 있는 자리여야 한다.
///
/// <c>_ = recorder.RecordAsync(...)</c>로 기다리지 않고 부르는 것도 안전하다.
/// 던지지 않으므로 관찰되지 않는 예외가 생길 수 없다.
/// </summary>
public interface IUsageRecorder
{
    /// <summary>
    /// 이벤트 한 줄을 남긴다. 이름은 <see cref="UsageEventNames"/>, 결과는
    /// <see cref="UsageOutcome"/>의 상수여야 하고, 그 밖의 값은 조용히 버려진다 —
    /// 목록에 없는 이름이 쌓이면 약국에게 한 약속이 거짓이 되기 때문이다.
    /// </summary>
    Task RecordAsync(string eventName, string outcome);

    /// <summary>
    /// 로그인한 사람의 역할을 기억한다. 이후의 모든 기록이 이 역할을 달고 나간다.
    ///
    /// 역할을 호출부마다 넘기지 않는 이유: 지금 role을 들고 있는 ViewModel은
    /// 하나뿐이라 나머지 일곱 군데에 생성자 인자를 더해야 하는데, 이 값은
    /// <b>쓰기 전용</b>이다 — 로그 한 칸에 적힐 뿐 앱이 무엇을 할지 정하는 데는
    /// 쓰이지 않는다. 그래서 CLAUDE.md의 "주변 세션을 두지 않는다"가 막으려는
    /// 것(권한 판단이 보이지 않는 상태에 매달리는 것)에 해당하지 않는다.
    /// <b>기록기 말고는 아무도 이 값을 읽지 않는다</b>는 것이 그 선이다.
    /// </summary>
    void StartSession(UserRole role);

    /// <summary>
    /// 역할을 잊는다. 로그아웃하면 부른다 — 부르지 않으면 다음 사람이 로그인하기
    /// 전까지의 기록이 떠난 사람의 역할을 달고 나간다.
    /// </summary>
    void EndSession();
}
