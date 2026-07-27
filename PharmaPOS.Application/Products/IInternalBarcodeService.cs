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
    /// 라벨을 출력한다.
    /// 주의: 실제 프린터 하드웨어 연동 전까지는 입력값 검증만 수행하고
    /// "출력 준비 완료" 상태를 반환하는 placeholder 구현이다.
    /// </summary>
    Task<LabelPrintResult> PrintLabelAsync(string productId, int labelQuantity, string? selectedPrinter);
}