namespace PharmaPOS.Application.Products;

/// <summary>
/// 라벨 한 장에 들어갈 내용. 막대를 어떻게 그릴지는 여기서 정하지 않는다.
/// </summary>
/// <param name="Code">바코드로 찍을 값. 사람이 읽는 글자로도 함께 나간다.</param>
/// <param name="ProductName">상품명.</param>
/// <param name="Caption">
/// 이 라벨이 무엇인지 말하는 한 줄("BOX OF 30", "LOOSE — 1 Tablet"). 구분할 상대가
/// 없으면 null이다 — 소분하지 않는 상품은 라벨이 한 장뿐이라 붙일 이유가 없다.
///
/// 소분 상품은 <b>두 장 모두</b> 붙인다. 한쪽에만 붙이면 "없음"이 곧 박스라는 뜻이
/// 되는데, 없는 것은 눈에 띄지 않는다 — 계산대에서 카드 두 장을 집어 든 사람은
/// 한쪽에 아무것도 안 적혀 있다는 사실을 알아차리지 못한다. 인쇄가 흐려 그 줄이
/// 안 나오면 낱개 카드가 그대로 박스 카드가 되기도 한다.
/// </param>
public sealed record BarcodeLabel(string Code, string ProductName, string? Caption = null);

/// <summary>
/// 바코드 라벨 인쇄. 구현체는 WPF 쪽에 있다 — 막대를 그리는 일은 화면 기술에 속한다.
/// </summary>
public interface ILabelPrintingService
{
    /// <summary>
    /// 라벨을 차례로 인쇄한다. 한 장이 한 페이지다.
    /// 프린터가 없거나 드라이버가 실패해도 <b>예외를 던지지 않고</b> false를 돌려준다.
    /// </summary>
    Task<bool> PrintLabelsAsync(IReadOnlyList<BarcodeLabel> labels);
}
