using PharmaPOS.Application.Inventory;

namespace PharmaPOS.Application.Repositories;

/// <summary>
/// 환불(F-06 부속)에 필요한 데이터 접근.
/// </summary>
public interface IRefundRepository
{
    /// <summary>
    /// 한 판매 거래((판매 시각 + 판매자)로 묶인 줄 전체)와 각 줄의 기환불 수량을 읽는다.
    /// </summary>
    Task<IReadOnlyList<RefundableLine>> GetRefundableLinesAsync(
        string facilityId, long transactionTime, string soldByUserId);

    /// <summary>
    /// 환불 행을 남기고, 재고 복원이 필요한 줄은 재고를 되돌린다. 전부 한 트랜잭션이다.
    /// 저장 직전 기환불 수량을 다시 확인해, 다른 창에서 먼저 환불한 경우 false를 돌려준다.
    /// </summary>
    Task<bool> SaveRefundAsync(IReadOnlyList<RefundLineForSave> lines);
}
