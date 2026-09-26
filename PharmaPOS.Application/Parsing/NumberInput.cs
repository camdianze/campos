using System.Globalization;

namespace PharmaPOS.Application.Parsing;

/// <summary>
/// 사람이 칸에 적어 넣은 숫자를 읽는다. <b>언제나 InvariantCulture</b>다.
///
/// 이 앱은 숫자를 전부 InvariantCulture로 <i>쓴다</i> — 상품 화면이 리엘을 달러로
/// 되돌릴 때, 영수증이 금액을 "0.00"으로 찍을 때, 임포트가 셀을 읽을 때 모두 그렇다.
/// 그런데 <c>decimal.TryParse(text, out value)</c>는 <b>현재 Windows 지역 설정</b>을
/// 쓴다. 쓰는 규칙과 읽는 규칙이 다르면, 그 둘이 만나는 자리에서 숫자가 조용히 바뀐다.
///
/// 한국·캄보디아 Windows는 소수점이 "."이라 둘이 우연히 같고, 그래서 이 어긋남은
/// 개발 PC에서 절대 드러나지 않는다. 소수점이 ","인 지역(프랑스·독일·인도네시아·
/// 베트남 등)으로 설정된 PC에서는 "."이 <i>천 단위 구분자</i>로 읽혀서
/// <c>19.5122</c>가 <c>195122</c>가 된다 — 오류도 경고도 없이, 가격만 만 배가 된다.
///
/// 어느 PC에서 열어도 같은 값이 나오는 쪽을 택한다. 대가는 지역 설정이 ","인 PC에서
/// "19,5"라고 칠 수 없다는 것인데, 약국 한 곳에 설치해 쓰는 프로그램에서 그것은
/// 기기마다 가격이 달라지는 것보다 훨씬 가벼운 문제다.
/// </summary>
public static class NumberInput
{
    /// <summary>금액·수량 등 소수를 읽는다. 천 단위 쉼표는 받아 준다.</summary>
    public static bool TryParseDecimal(string? text, out decimal value) =>
        decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out value);

    /// <summary>개수를 읽는다. 소수점이 붙어 있으면 개수가 아니므로 거절한다.</summary>
    public static bool TryParseInt(string? text, out int value) =>
        int.TryParse(text, NumberStyles.Integer | NumberStyles.AllowThousands,
            CultureInfo.InvariantCulture, out value);

    /// <summary>칸에 다시 적어 넣을 문자열. 읽는 규칙과 짝이 맞아야 한다.</summary>
    public static string ToText(decimal value) =>
        value.ToString(CultureInfo.InvariantCulture);
}
