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
    private readonly ILabelPrintingService _labelPrintingService;

    public InternalBarcodeService(
        IProductRepository productRepository,
        IInternalBarcodeSequenceRepository barcodeSequenceRepository,
        ILabelPrintingService labelPrintingService)
    {
        _productRepository = productRepository;
        _barcodeSequenceRepository = barcodeSequenceRepository;
        _labelPrintingService = labelPrintingService;
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

    /// <summary>
    /// 라벨을 기본 프린터로 인쇄한다. 영수증·복약안내와 같은 경로다 — 프린터를 고르게 하지 않는다.
    ///
    /// 이 라벨은 제품마다 붙이는 스티커가 아니라 <b>계산대에 붙여 두는 카드</b>를 상정한다.
    /// 바코드가 없는 상품(낱개, 인쇄 불량 등)을 손님이 가져오면 카드를 찍어 등록하는 방식이라
    /// 상품 종류마다 한 장이면 된다. 그래서 기본이 1장이고, 제품마다 붙이고 싶으면 수량을 올린다.
    ///
    /// 소분 판매 상품은 <b>박스용과 낱개용 두 종류</b>를 함께 뽑는다. 헐어서 파는 낱개에는
    /// 박스 바코드를 쓸 수 없고(그걸 찍으면 박스 하나가 팔린다), 그렇다고 라벨을 따로
    /// 뽑으러 다시 들어오게 하면 한쪽을 빠뜨린다.
    /// </summary>
    public async Task<LabelPrintResult> PrintLabelAsync(string productId, int labelQuantity)
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

        // 유통사 바코드가 있으면 그것을 카드에 쓴다. 제품에 붙은 것과 같은 값이라
        // 카드를 찍든 제품을 찍든 같은 상품으로 잡히고, 판매 이력이 두 값으로 갈라지지 않는다.
        // 내부 바코드는 유통사 바코드가 없을 때의 대체품이다.
        var wholeProductCode = string.IsNullOrWhiteSpace(product.Barcode)
            ? product.InternalBarcode
            : product.Barcode;

        if (string.IsNullOrWhiteSpace(wholeProductCode))
        {
            return LabelPrintResult.Failure(
                "This product has no barcode. Generate the internal barcode first.");
        }

        if (!Code128Encoder.CanEncode(wholeProductCode))
        {
            return LabelPrintResult.Failure(
                "This barcode has characters that cannot be printed as a Code 128 label.");
        }

        var labels = new List<BarcodeLabel>();

        for (var i = 0; i < labelQuantity; i++)
        {
            labels.Add(new BarcodeLabel(wholeProductCode, product.ProductName));

            // 낱개 바코드는 소분 판매 상품에만 있다 (내부 바코드 + "-EA").
            // 유통사 바코드가 있어도 이것만은 내부 바코드를 쓴다 — 유통사 바코드는 박스에 붙은 것이라
            // 낱개를 가리키지 못한다.
            if (product.UnitBarcode is { } unitBarcode)
            {
                labels.Add(new BarcodeLabel(
                    unitBarcode, product.ProductName, Caption: $"LOOSE — 1 {product.Unit}"));
            }
        }

        var printed = await _labelPrintingService.PrintLabelsAsync(labels);

        if (!printed)
        {
            return LabelPrintResult.Failure("The labels could not be printed. Check the printer.");
        }

        return LabelPrintResult.Success(
            product.UnitBarcode is null
                ? $"Printed {labelQuantity} label(s) for {wholeProductCode}."
                : $"Printed {labelQuantity} label(s) for {wholeProductCode} "
                  + $"and {labelQuantity} loose-unit label(s).");
    }
}