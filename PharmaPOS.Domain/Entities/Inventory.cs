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

    public required int CurrentQuantity { get; set; }

    public required long UpdatedAt { get; set; }
}