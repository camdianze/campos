using PharmaPOS.Application.Repositories;
using PharmaPOS.Domain.Enums;

namespace PharmaPOS.Application.Products;

/// <summary>
/// IProductService의 구현체.
/// Screen SCR-PROD-012, 4절 흐름과 검증 순서를 그대로 코드로 옮긴 것이다.
/// </summary>
public class ProductService : IProductService
{
    private readonly IProductRepository _productRepository;
    private readonly IInternalBarcodeSequenceRepository _barcodeSequenceRepository;

    public ProductService(
        IProductRepository productRepository,
        IInternalBarcodeSequenceRepository barcodeSequenceRepository)
    {
        _productRepository = productRepository;
        _barcodeSequenceRepository = barcodeSequenceRepository;
    }

    public async Task<ProductSaveResult> SaveProductAsync(
        Domain.Entities.Product product,
        bool isNewProduct,
        bool acknowledgeLowerSellingPriceWarning = false)
    {
        // 필수값 검증 (Screen §4.3절 순서 그대로)
        if (string.IsNullOrWhiteSpace(product.ProductName))
        {
            return ProductSaveResult.Failure("Please enter the product name.");
        }

        // 제형은 언제나 필수다. 약이 아닌 것(붕대·채혈관·폐기물 봉투)은 Other를 고른다.
        if (product.DosageForm is null)
        {
            return ProductSaveResult.Failure("Please select the dosage form.");
        }

        // 성분명은 약에만 필수다. 항생제 판별이 성분명으로 되기 때문에 약에서 비면
        // 복약안내가 조용히 빠진다. 반면 붕대에는 성분명이라는 것이 없다 —
        // 그것까지 요구하면 상품의 1/4이 저장 자체를 못 하게 된다.
        if (product.DosageForm != DosageForm.Other && string.IsNullOrWhiteSpace(product.GenericName))
        {
            return ProductSaveResult.Failure(
                "Please enter the generic name. For items that are not medicines, choose dosage form 'Other'.");
        }

        if (string.IsNullOrWhiteSpace(product.Unit))
        {
            return ProductSaveResult.Failure("Please enter the unit.");
        }

        // 원가는 선택이다. 모르면 0으로 두고, 그때는 "원가보다 싸게 판다" 경고가 뜨지 않을 뿐이다.
        if (product.CostPrice < 0)
        {
            return ProductSaveResult.Failure("Cost price cannot be negative.");
        }

        if (product.SellingPrice <= 0)
        {
            return ProductSaveResult.Failure("Selling price must be greater than zero.");
        }

        if (product.SafetyStockLevel < 0)
        {
            return ProductSaveResult.Failure("Safety stock level cannot be negative.");
        }

        if (product.UnitsPerBox < 1)
        {
            return ProductSaveResult.Failure("Units per box must be at least 1.");
        }

        if (product.UnitSellingPrice is <= 0)
        {
            return ProductSaveResult.Failure("Loose unit price must be greater than zero.");
        }

        // 낱개가는 실제로 주고받는 돈이라 소수점 두 자리를 넘길 수 없다.
        // 여기서 막지 않으면 0.4533 같은 값이 그대로 저장되고, 영수증·판매 이력에는
        // 통화 단위로 낼 수 없는 금액이 찍힌다.
        if (product.UnitSellingPrice is { } looseUnitPrice
            && decimal.Round(looseUnitPrice, 2) != looseUnitPrice)
        {
            return ProductSaveResult.Failure("Loose unit price can have at most 2 decimal places.");
        }

        // 낱개가는 헐어서 파는 상품에만 의미가 있다. 낱개 판매를 끈 상품에 값만 남아 있으면
        // 화면에는 안 보이는데 나중에 다시 켜는 순간 옛 가격이 되살아난다.
        if (!product.IsBoxedProduct)
        {
            product.UnitSellingPrice = null;
        }

        // 경고 후 확인이 필요한 케이스: 아직 확인 안 받았으면 여기서 멈추고 확인을 요청한다.
        if (product.SellingPrice < product.CostPrice && !acknowledgeLowerSellingPriceWarning)
        {
            return ProductSaveResult.NeedsConfirmation(
                "Selling price is lower than cost price. Continue?");
        }

        // 바코드는 앞뒤 공백 없이 저장한다. 스캐너가 찍은 값과 글자 단위로 같아야
        // 판매 화면이 장바구니까지 한 번에 가는데, 붙여넣기나 시트에서 공백 하나가
        // 따라오면 검색은 되고 담기는 안 되는 상태가 된다 — 계산대에서 원인을 알 수 없다.
        product.Barcode = string.IsNullOrWhiteSpace(product.Barcode) ? null : product.Barcode.Trim();
        product.InternalBarcode = string.IsNullOrWhiteSpace(product.InternalBarcode)
            ? null
            : product.InternalBarcode.Trim();

        // barcode 중복 확인 (자기 자신은 제외)
        if (!string.IsNullOrWhiteSpace(product.Barcode))
        {
            var barcodeExists = await _productRepository.BarcodeExistsAsync(
                product.Barcode, excludeProductId: isNewProduct ? null : product.ProductId);

            if (barcodeExists)
            {
                return ProductSaveResult.Failure("This barcode is already registered.");
            }
        }

        // 내부 바코드 자동 생성: 제조사 바코드가 없고, 아직 내부 바코드도 없는 경우.
        // 박스/낱개 상품은 제조사 바코드가 있어도 만든다 — 그 바코드는 박스에 붙은 것이라
        // 헐어서 파는 낱개를 가리킬 수단이 따로 있어야 하고, 그게 InternalBarcode + "-EA"다.
        var needsInternalBarcode =
            string.IsNullOrWhiteSpace(product.Barcode) || product.IsBoxedProduct;

        if (needsInternalBarcode && string.IsNullOrWhiteSpace(product.InternalBarcode))
        {
            try
            {
                product.InternalBarcode = await _barcodeSequenceRepository.GetNextInternalBarcodeAsync();
            }
            catch (Exception)
            {
                return ProductSaveResult.Failure("Internal barcode could not be generated.");
            }
        }
        else if (!string.IsNullOrWhiteSpace(product.InternalBarcode))
        {
            // 이미 내부 바코드가 있는 상태에서 수정하는 경우, 중복 확인만 한다 (재생성 안 함).
            var internalBarcodeExists = await _productRepository.InternalBarcodeExistsAsync(
                product.InternalBarcode, excludeProductId: isNewProduct ? null : product.ProductId);

            if (internalBarcodeExists)
            {
                return ProductSaveResult.Failure("Internal barcode already exists.");
            }
        }

        try
        {
            if (isNewProduct)
            {
                product.ProductId = Guid.NewGuid().ToString();
                product.CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                product.Status = EntityStatus.Active;

                await _productRepository.InsertAsync(product);
            }
            else
            {
                await _productRepository.UpdateAsync(product);
            }
        }
        catch (Exception)
        {
            return ProductSaveResult.Failure("Product could not be saved.");
        }

        return ProductSaveResult.Success();
    }

    public async Task<ProductSaveResult> DeactivateProductAsync(string productId)
    {
        var product = await _productRepository.GetByIdAsync(productId);

        if (product is null)
        {
            return ProductSaveResult.Failure("Product could not be deactivated.");
        }

        if (product.Status == EntityStatus.Inactive)
        {
            return ProductSaveResult.Failure("This product is already inactive.");
        }

        try
        {
            await _productRepository.DeactivateAsync(productId);
        }
        catch (Exception)
        {
            return ProductSaveResult.Failure("Product could not be deactivated.");
        }

        return ProductSaveResult.Success();
    }
}