using PharmaPOS.Application.Inventory;

namespace PharmaPOS.Application.Repositories;

/// <summary>
/// 판매 내역 조회를 담당하는 인터페이스. (Screen SCR-SALES-007)
/// </summary>
public interface ISalesHistoryRepository
{
    /// <summary>
    /// 조건에 맞는 STOCK_OUT 라인을 조회한다. dateFrom/dateTo가 null이면 해당 조건은 무시된다.
    /// </summary>
    Task<IReadOnlyList<SalesHistoryLineItem>> SearchAsync(
        string facilityId,
        long? dateFromUtc,
        long? dateToUtc,
        string searchTerm,
        string? paymentMethod);

    /// <summary>
    /// 같은 결제 순간(transactionTime)에 발생한 모든 라인을 조회한다.
    /// 하나의 POS 판매(Confirm Sale)에서 여러 상품을 담으면 상품마다 별도 행이 생기지만,
    /// 같은 순간에 커밋되므로 이 값으로 하나의 "거래"를 재구성한다.
    /// (현재 검색 필터와 무관하게 해당 거래의 전체 라인을 가져와야 하므로 별도 메서드로 둔다.)
    /// </summary>
    Task<IReadOnlyList<SalesHistoryLineItem>> GetTransactionGroupAsync(
        string facilityId, long transactionTime, string userId);
}