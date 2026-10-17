using CamPOS.Domain.Entities;
using CamPOS.Application.Products;

namespace CamPOS.Application.Products;

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
    ///
    /// userId는 수정으로 박스당 개수가 바뀔 때 재고를 다시 세는 조정 원장 행에 적힌다.
    /// </summary>
    /// <summary>
    /// 쓰인 적이 없는 상품을 지운다. 이력이 있으면 거부하고 Inactive를 권한다 —
    /// 판매·입고·조정이 전부 product_id로 상품을 가리키므로, 이력이 있는 상품을
    /// 지우면 지난 기록에 이름 없는 줄이 남고 되돌릴 수 없다.
    /// </summary>
    Task<ProductSaveResult> DeleteProductAsync(string productId);

    Task<ProductSaveResult> SaveProductAsync(
        Product product,
        bool isNewProduct,
        string userId,
        bool acknowledgeLowerSellingPriceWarning = false);

    /// <summary>
    /// 상품을 비활성화한다. 이미 Inactive면 실패로 처리한다 (Screen 11, §4.3절).
    /// </summary>
    Task<ProductSaveResult> DeactivateProductAsync(string productId);
}