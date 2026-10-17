namespace PharmaPOS.Application.Counselling;

/// <summary>
/// 복약안내 용지 출력 기본 동작.
///
/// 끄는 선택지(never)는 없앴다. 항생제를 팔았다는 안내는 인쇄 설정과 무관하게 뜨는데,
/// 끌 수 있게 두면 그 안내까지 함께 사라져 기능의 목적이 없어진다.
/// 남은 두 값은 "종이가 자동으로 나가는가"만 가른다.
///
/// 저장된 값이 never였던 DB는 이 값을 읽지 못해 기본값(always)으로 돌아온다.
/// 꺼 두었던 약국은 다시 켜지는 셈인데, 그것이 이 변경의 목적이다.
/// </summary>
public enum CounsellingPrintMode
{
    /// <summary>
    /// 항생제가 매칭되면 묻지 않고 인쇄한다. 기본값이다.
    /// 기능의 목적이 오남용 예방이라 opt-out 구조가 맞다.
    /// </summary>
    Always,

    /// <summary>매번 약사에게 인쇄 여부를 묻는다.</summary>
    Ask
}
