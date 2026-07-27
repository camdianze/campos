using PharmaPOS.Domain.Enums;

namespace PharmaPOS.Application.Inventory;

/// <summary>
/// F-06 부속 기능인 판매 내역 조회를 담당하는 인터페이스. (Screen SCR-SALES-007)
/// </summary>
public interface ISalesHistoryService
{
    /// <summary>
    /// Screen §4절 검증(시작일 &gt; 종료일 차단)을 거쳐 판매 내역을 조회한다.
    /// </summary>
    Task<SalesHistoryQueryResult> SearchAsync(
        string facilityId,
        DateTime? dateFrom,
        DateTime? dateTo,
        string searchTerm,
        PaymentMethod? paymentMethod);

    /// <summary>
    /// 선택된 라인과 같은 순간에 발생한 모든 라인(하나의 판매 거래 전체)을 조회한다.
    /// View Detail, Reprint Receipt에서 사용한다.
    /// </summary>
    Task<IReadOnlyList<SalesHistoryLineItem>> GetTransactionGroupAsync(
        string facilityId, SalesHistoryLineItem selectedLine);
}