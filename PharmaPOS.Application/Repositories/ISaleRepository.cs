using PharmaPOS.Application.Inventory;

namespace PharmaPOS.Application.Repositories;

/// <summary>
/// POS 판매(Sale Cart) 데이터 저장을 담당하는 인터페이스.
/// </summary>
public interface ISaleRepository
{
    /// <summary>
    /// 장바구니의 모든 라인을 하나의 트랜잭션으로 저장한다.
    /// 각 라인마다 저장 직전 현재 재고가 판매 수량 이상인지 재확인하고,
    /// 하나라도 부족하면 전체를 롤백한다.
    /// (Screen SCR-POS-005, 5절 "장바구니 상품 중 일부 재고 부족")
    /// </summary>
    /// <returns>전부 성공하면 true, 재고 부족으로 실패하면 false.</returns>
    Task<bool> SaveSaleAsync(IReadOnlyList<SaleLineForSave> lines);
}