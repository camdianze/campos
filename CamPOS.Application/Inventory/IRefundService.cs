namespace PharmaPOS.Application.Inventory;

/// <summary>
/// 판매 취소(환불)를 담당하는 인터페이스. 판매 내역 화면에서 시작한다.
/// </summary>
public interface IRefundService
{
    /// <summary>
    /// 선택한 판매 거래의 줄 목록과 줄별 환불 가능 수량을 읽는다.
    /// </summary>
    Task<IReadOnlyList<RefundableLine>> GetRefundableLinesAsync(
        string facilityId, long transactionTime, string soldByUserId);

    /// <summary>
    /// 요청한 줄들을 환불한다. 수량 검증을 거쳐 원장에 Refund 행을 남기고,
    /// returnToStock이면 원래 배치로 재고를 되돌린다.
    /// </summary>
    /// <param name="userId">환불을 수행하는 사용자(원 판매자와 다를 수 있다).</param>
    /// <param name="reason">메모. 선택 입력이며 비워 두면 저장하지 않는다.</param>
    /// <param name="returnToStock">되돌린 약을 다시 팔 수 있는지. 개봉·변질품이면 false.</param>
    Task<RefundResult> RefundAsync(
        string facilityId,
        string userId,
        long saleTransactionTime,
        string soldByUserId,
        IReadOnlyList<RefundLineRequest> lines,
        string? reason,
        bool returnToStock);
}
