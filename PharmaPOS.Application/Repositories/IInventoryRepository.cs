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

    /// <summary>
    /// 다 팔려서 빈 배치 행을 목록에서 치운다. 유통기한마다 배치번호를 새로 따는 운영이라
    /// 소진된 배치가 계속 쌓이면 목록이 읽기 어려워지기 때문이다.
    ///
    /// 수량이 0인 행만 지운다. 재고가 남은 배치를 지우면 Stock_Transaction에 아무 기록도
    /// 남기지 않은 채 재고가 증발하므로, 그건 반드시 재고 조정(Adjustment)을 거쳐야 한다.
    /// 조건은 DELETE 문 안에 두어, 확인하는 사이에 입고가 들어와도 지워지지 않게 했다.
    ///
    /// 판매 이력은 Stock_Transaction이 따로 들고 있고 Inventory를 참조하지 않으므로,
    /// 이 행을 지워도 과거 기록은 그대로 남는다.
    /// </summary>
    /// <returns>지웠으면 true, 수량이 0이 아니거나 이미 없어서 못 지웠으면 false.</returns>
    Task<bool> DeleteEmptyBatchAsync(string inventoryId);
}