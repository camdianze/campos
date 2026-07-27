using PharmaPOS.Application.Inventory;

namespace PharmaPOS.Application.Repositories;

/// <summary>
/// 재고 현황 조회를 담당하는 인터페이스. (Screen SCR-INV-008, SCR-ADJ-010)
/// </summary>
public interface IInventoryRepository
{
    /// <summary>
    /// 검색어, 유통기한 필터, 저재고 필터, 정렬 기준에 따라 재고 현황을 조회한다.
    /// 비활성 상품은 기본적으로 제외한다 (Screen §4절).
    /// </summary>
    Task<IReadOnlyList<InventoryStatusItem>> GetInventoryStatusAsync(
        string searchTerm,
        ExpiryFilterOption expiryFilter,
        bool lowStockOnly,
        InventorySortOption sortBy);

    /// <summary>
    /// 지정된 상품의 현재 배치 목록을 조회한다.
    /// 재고 조정 화면(SCR-ADJ-010)의 "Batch Number Selection"에서 사용한다.
    /// </summary>
    Task<IReadOnlyList<InventoryBatchOption>> GetBatchesForProductAsync(string productId, string facilityId);
}