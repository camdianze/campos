namespace PharmaPOS.Domain.Entities;

/// <summary>
/// PRD의 Inventory 테이블에 대응하는 엔티티.
/// 읽기 최적화용 캐시 테이블 — Stock_Transaction으로부터 계산된 값이며,
/// 별도의 진실 공급원이 아니다 (엔지니어링 노트).
/// </summary>
public class Inventory
{
    public required string InventoryId { get; set; }

    public required string FacilityId { get; set; }

    public required string ProductId { get; set; }

    public required string BatchNumber { get; set; }

    public required long ExpiryDate { get; set; }

    /// <summary>
    /// 낱개 기준 총 재고. 박스/낱개 상품이라도 이 값이 재고의 진실이며,
    /// 아래 두 값은 그 총량을 "안 헐린 박스 / 헐어 놓은 낱개"로 나눠 놓은 것이다.
    /// 항상 CurrentQuantity = BoxQuantity × units_per_box + UnitQuantity 를 만족한다.
    /// </summary>
    public required int CurrentQuantity { get; set; }

    /// <summary>아직 뜯지 않은 박스 수. units_per_box가 1인 상품은 늘 0이다.</summary>
    public int BoxQuantity { get; set; }

    /// <summary>헐어 놓은 낱개 수. units_per_box가 1인 상품은 여기에 전량이 들어간다.</summary>
    public int UnitQuantity { get; set; }

    public required long UpdatedAt { get; set; }
}