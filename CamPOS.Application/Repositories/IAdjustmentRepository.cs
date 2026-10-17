using PharmaPOS.Domain.Entities;

namespace PharmaPOS.Application.Repositories;

/// <summary>
/// 재고 조정(Adjustment) 데이터 저장을 담당하는 인터페이스.
/// </summary>
public interface IAdjustmentRepository
{
    /// <summary>
    /// Stock_Transaction에 ADJUSTMENT 기록을 남기고, Inventory 수량을
    /// physicalCount로 갱신한다. expectedCurrentQuantity가 실제 DB 값과
    /// 다르면(다른 곳에서 먼저 재고가 바뀐 경우) 저장하지 않고 false를 반환한다.
    /// (Screen SCR-ADJ-010, 5절 "조정 중 재고 변경 발생")
    ///
    /// batchNumber는 저장할 배치번호다. 바꾸지 않았으면 원래 값이 그대로 들어온다.
    /// </summary>
    /// <returns>저장에 성공하면 true, 동시성 충돌로 실패하면 false.</returns>
    Task<bool> SaveAdjustmentAsync(
        StockTransaction transaction,
        string inventoryId,
        string batchNumber,
        int expectedCurrentQuantity,
        int physicalCount,
        int physicalBoxCount,
        int physicalUnitCount);

    /// <summary>
    /// 같은 상품에 이미 그 배치번호를 쓰는 다른 배치가 있는지.
    /// Inventory가 (facility, product, batch)로 유일해서, 겹치면 저장 자체가 실패한다.
    /// </summary>
    Task<bool> BatchNumberExistsAsync(
        string facilityId, string productId, string batchNumber, string excludeInventoryId);
}
