using PharmaPOS.Domain.Enums;

namespace PharmaPOS.Application.Inventory;

/// <summary>
/// F-06 POS 판매 확정 로직을 담당하는 인터페이스. (Screen SCR-POS-005)
/// </summary>
public interface ISaleService
{
    /// <summary>
    /// Screen SCR-POS-005, 4절의 "판매 확정" 검증/저장 흐름을 수행한다.
    /// acknowledgeLowerSellingPriceWarning이 false인 상태에서 장바구니에
    /// Cost Price보다 낮은 Selling Price 항목이 있으면, 저장하지 않고
    /// NeedsConfirmation 결과를 반환한다.
    /// </summary>
    Task<SaleResult> ConfirmSaleAsync(
        string facilityId,
        string userId,
        IReadOnlyList<SaleLineItem> cartItems,
        PaymentMethod? paymentMethod,
        decimal? cashTendered,
        string? notes,
        bool acknowledgeLowerSellingPriceWarning = false);
}