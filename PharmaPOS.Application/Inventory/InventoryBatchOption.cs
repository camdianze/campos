namespace PharmaPOS.Application.Inventory;

/// <summary>
/// 재고 조정 화면(SCR-ADJ-010)에서 배치를 선택할 때 쓰는 요약 정보.
/// </summary>
public class InventoryBatchOption
{
    public required string InventoryId { get; set; }
    public required string BatchNumber { get; set; }
    public required long ExpiryDate { get; set; }
    public required int CurrentQuantity { get; set; }
}