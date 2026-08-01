namespace PharmaPOS.Application.Counselling;

/// <summary>복약안내 용지 출력 기본 동작.</summary>
public enum CounsellingPrintMode
{
    /// <summary>
    /// 항생제가 매칭되면 묻지 않고 인쇄한다. 기본값이다.
    /// 기능의 목적이 오남용 예방이라 opt-out 구조가 맞다.
    /// </summary>
    Always,

    /// <summary>매번 약사에게 인쇄 여부를 묻는다.</summary>
    Ask,

    /// <summary>인쇄하지 않는다. 매칭과 로깅은 그대로 남는다.</summary>
    Never
}
