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
    /// </summary>
    Task<AdjustmentResult> SaveAdjustmentAsync(
        string facilityId,
        string productId,
        string userId,
        string inventoryId,
        string batchNumber,
        long expiryDate,
        int systemQuantity,
        int physicalCount,
        string reason,
        bool allowZeroDelta = false);
}