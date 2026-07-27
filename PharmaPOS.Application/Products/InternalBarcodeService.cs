using PharmaPOS.Application.Repositories;

namespace PharmaPOS.Application.Products;

/// <summary>
/// IInternalBarcodeService의 구현체.
/// Screen SCR-BARCODE-013, 4절 흐름을 그대로 코드로 옮긴 것이다.
/// </summary>
public class InternalBarcodeService : IInternalBarcodeService
{
    private readonly IProductRepository _productRepository;
    private readonly IInternalBarcodeSequenceRepository _barcodeSequenceRepository;

    public InternalBarcodeService(
        IProductRepository productRepository,
        IInternalBarcodeSequenceRepository barcodeSequenceRepository)
    {
        _productRepository = productRepository;
        _barcodeSequenceRepository = barcodeSequenceRepository;
    }

    public async Task<BarcodeGenerationResult> GenerateOrGetInternalBarcodeAsync(string productId)
    {
        var product = await _productRepository.GetByIdAsync(productId);

        if (product is null)
        {
            return BarcodeGenerationResult.Failure("Please select a product.");
        }

        // 이미 내부 바코드가 있으면 재생성하지 않고 그대로 반환한다 (Screen §4.3절).
        if (!string.IsNullOrWhiteSpace(product.InternalBarcode))
        {
            return BarcodeGenerationResult.Success(product.InternalBarcode);
        }

        string newInternalBarcode;

        try
        {
            newInternalBarcode = await _barcodeSequenceRepository.GetNextInternalBarcodeAsync();
        }
        catch (Exception)
        {
            return BarcodeGenerationResult.Failure("Internal barcode could not be generated.");
        }

        product.InternalBarcode = newInternalBarcode;

        try
        {
            await _productRepository.UpdateAsync(product);
        }
        catch (Exception)
        {
            return BarcodeGenerationResult.Failure("Internal barcode could not be saved.");
        }

        return BarcodeGenerationResult.Success(newInternalBarcode);
    }

    public async Task<LabelPrintResult> PrintLabelAsync(string productId, int labelQuantity, string? selectedPrinter)
    {
        var product = await _productRepository.GetByIdAsync(productId);

        if (product is null)
        {
            return LabelPrintResult.Failure("Please select a product.");
        }

        if (labelQuantity <= 0)
        {
            return LabelPrintResult.Failure("Label quantity must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(selectedPrinter))
        {
            return LabelPrintResult.Failure("Please select a printer.");
        }

        // TODO: 실제 라벨 프린터 하드웨어 연동 (기종/프로토콜 미정, M-5).
        // 현재는 입력값 검증까지만 수행하고 "출력 준비 완료" 상태를 반환한다.
        await Task.CompletedTask;

        return LabelPrintResult.Success(
            $"Ready to print {labelQuantity} label(s) for {product.InternalBarcode} on {selectedPrinter}. " +
            "(Actual printing not yet implemented.)");
    }
}