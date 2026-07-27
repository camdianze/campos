using PharmaPOS.Domain.Entities;
using PharmaPOS.Application.Products;

namespace PharmaPOS.Application.Products;

/// <summary>
/// F-03 상품 등록/수정 로직을 담당하는 인터페이스.
/// </summary>
public interface IProductService
{
    /// <summary>
    /// 신규 상품을 등록하거나(product.ProductId가 비어있으면 신규로 간주하지 않고,
    /// 호출하는 쪽에서 신규/수정을 이미 구분해서 넘긴다) 기존 상품을 수정한다.
    /// isNewProduct로 신규/수정을 명시적으로 구분한다.
    /// acknowledgeLowerSellingPriceWarning이 false인 상태에서 판매가가 매입가보다
    /// 낮으면, 저장하지 않고 NeedsConfirmation 결과를 반환한다.
    /// </summary>
    Task<ProductSaveResult> SaveProductAsync(
        Product product,
        bool isNewProduct,
        bool acknowledgeLowerSellingPriceWarning = false);

    /// <summary>
    /// 상품을 비활성화한다. 이미 Inactive면 실패로 처리한다 (Screen 11, §4.3절).
    /// </summary>
    Task<ProductSaveResult> DeactivateProductAsync(string productId);
}