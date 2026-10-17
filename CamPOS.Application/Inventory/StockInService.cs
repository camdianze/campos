using PharmaPOS.Application.Repositories;
using PharmaPOS.Domain.Entities;
using PharmaPOS.Domain.Enums;

namespace PharmaPOS.Application.Inventory;

/// <summary>
/// IStockInService의 구현체.
/// Screen SCR-STOCKIN-009, 4절 흐름을 그대로 코드로 옮긴 것이다.
/// </summary>
public class StockInService : IStockInService
{
    private readonly IProductRepository _productRepository;
    private readonly IStockInRepository _stockInRepository;

    public StockInService(IProductRepository productRepository, IStockInRepository stockInRepository)
    {
        _productRepository = productRepository;
        _stockInRepository = stockInRepository;
    }

    public async Task<StockInResult> SaveStockInAsync(
        string facilityId,
        string productId,
        string userId,
        string batchNumber,
        DateTime expiryDate,
        DateTime stockInDate,
        int quantity)
    {
        // 상품 선택 검증
        if (string.IsNullOrWhiteSpace(productId))
        {
            return StockInResult.Failure("Please select a product.");
        }

        var product = await _productRepository.GetByIdAsync(productId);

        if (product is null)
        {
            return StockInResult.Failure("Product not found.");
        }

        // 상품은 Active만 선택 가능 (Screen §4절 "상품 조회" 원칙)
        if (product.Status != EntityStatus.Active)
        {
            return StockInResult.Failure("This product is inactive.");
        }

        // 배치번호 검증
        if (string.IsNullOrWhiteSpace(batchNumber))
        {
            return StockInResult.Failure("Please enter the batch number.");
        }

        // 유통기한 검증: 입고일 기준으로 미래여야 한다 (PRD F-05 Validation)
        if (expiryDate <= stockInDate)
        {
            return StockInResult.Failure("Expiry date must be a future date.");
        }

        // 수량 검증 (정수 여부는 View 단에서 파싱 시점에 이미 걸러지지만,
        // 서비스 계층에서도 0 이하 여부를 다시 한번 확인한다)
        if (quantity <= 0)
        {
            return StockInResult.Failure("Quantity must be greater than zero.");
        }

        // 입고는 언제나 박스째 들어온다. 박스/낱개 상품이면 입력한 수량이 박스 개수이고,
        // 재고에는 박스 수와 낱개 환산량을 함께 올린다. 헐어 놓은 낱개는 늘지 않는다.
        var isBoxedProduct = product.IsBoxedProduct;
        var boxQuantity = isBoxedProduct ? quantity : 0;
        var unitQuantity = isBoxedProduct ? 0 : quantity;
        var totalUnits = BoxUnitMath.ToTotalUnits(boxQuantity, unitQuantity, product.UnitsPerBox);

        var transaction = new StockTransaction
        {
            TransactionId = Guid.NewGuid().ToString(),
            FacilityId = facilityId,
            ProductId = productId,
            UserId = userId,
            TransactionType = TransactionType.StockIn,
            BatchNumber = batchNumber,
            ExpiryDate = new DateTimeOffset(expiryDate).ToUnixTimeMilliseconds(),
            // 원장은 낱개 기준. 10박스 × 30개 입고는 300으로 남는다.
            Quantity = totalUnits,
            TransactionTime = new DateTimeOffset(stockInDate).ToUnixTimeMilliseconds()
        };

        try
        {
            await _stockInRepository.SaveStockInAsync(transaction, boxQuantity, unitQuantity);
        }
        catch (Exception)
        {
            return StockInResult.Failure("Stock-in could not be saved.");
        }

        return StockInResult.Success();
    }
}