namespace PharmaPOS.Application.Inventory;

/// <summary>
/// F-08 재고 조정 로직을 담당하는 인터페이스. (Screen SCR-ADJ-010)
/// </summary>
public interface IAdjustmentService
{
    /// <summary>
    /// Screen SCR-ADJ-010, 4절의 검증/저장 흐름을 수행한다.
    /// allowZeroDelta가 false인 상태에서 delta가 0이면, 저장하지 않고
    /// NeedsConfirmation 결과를 반환한다 (호출부는 사용자 확인 후
    /// allowZeroDelta=true로 다시 호출한다).
    ///
    /// 실사 수량은 박스와 낱개를 따로 받는다 — 실사는 선반에서 "안 뜯은 통 몇 개,
    /// 헐어 놓은 알 몇 개"로 세지, 총 알 수를 암산해서 세지 않기 때문이다.
    /// 박스/낱개 구분이 없는 상품(unitsPerBox = 1)은 physicalBoxCount를 0으로 두고
    /// physicalUnitCount에 전량을 넣으면 종전과 같이 동작한다.
    /// </summary>
    Task<AdjustmentResult> SaveAdjustmentAsync(
        string facilityId,
        string productId,
        string userId,
        string inventoryId,
        string batchNumber,
        long expiryDate,
        int systemQuantity,
        int physicalBoxCount,
        int physicalUnitCount,
        int unitsPerBox,
        string reason,
        bool allowZeroDelta = false);
}