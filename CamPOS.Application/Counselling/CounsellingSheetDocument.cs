namespace PharmaPOS.Application.Counselling;

/// <summary>
/// 그려진 복약안내 용지. 프린터 어댑터는 이 줄들을 고정폭 글꼴로 그대로 찍는다.
///
/// 인쇄 대상을 문자열 목록으로 두는 이유는, 레이아웃 규칙(공란 유지, 분류 표시,
/// 현지어 폴백)을 프린터 없이 단위 테스트로 검증하기 위해서다.
/// </summary>
public class CounsellingSheetDocument
{
    public required IReadOnlyList<string> Lines { get; init; }

    /// <summary>QR 코드로 만들 주소. 없으면 null.</summary>
    public string? QrUrl { get; init; }

    /// <summary>어느 상품의 용지인지 (인쇄 작업 이름 표시용).</summary>
    public required string ProductName { get; init; }

    public string ToPlainText() => string.Join(Environment.NewLine, Lines);
}
