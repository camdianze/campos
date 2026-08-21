namespace PharmaPOS.Application.Products;

/// <summary>
/// F-03 내부 바코드 생성/출력 로직을 담당하는 인터페이스. (Screen SCR-BARCODE-013)
/// </summary>
public interface IInternalBarcodeService
{
    /// <summary>
    /// 상품에 내부 바코드가 이미 있으면 그 값을 반환하고,
    /// 없으면 새로 생성해서 저장한 뒤 반환한다.
    /// </summary>
    Task<BarcodeGenerationResult> GenerateOrGetInternalBarcodeAsync(string productId);

    /// <summary>
    /// 라벨을 기본 프린터로 출력한다. 영수증·복약안내와 같이 프린터를 고르게 하지 않는다.
    /// 소분 판매 상품이면 박스용과 낱개용을 각각 labelQuantity장씩 함께 뽑는다.
    /// </summary>
    Task<LabelPrintResult> PrintLabelAsync(string productId, int labelQuantity);
}