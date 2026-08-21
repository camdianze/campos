namespace PharmaPOS.Application.Products;

/// <summary>
/// 라벨 한 장에 들어갈 내용. 막대를 어떻게 그릴지는 여기서 정하지 않는다.
/// </summary>
/// <param name="Code">바코드로 찍을 값. 사람이 읽는 글자로도 함께 나간다.</param>
/// <param name="ProductName">상품명.</param>
/// <param name="Caption">
/// 낱개용 라벨처럼 구분이 필요할 때 붙이는 한 줄. 없으면 null.
/// 박스용과 낱개용이 같은 상품명을 달고 나오므로, 이게 없으면 선반에서 구분되지 않는다.
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
