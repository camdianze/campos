namespace PharmaPOS.Application.Inventory;

/// <summary>
/// 재고 현황 화면(SCR-INV-008)에 표시할 조인된 데이터.
/// Inventory와 Product Master를 합친 화면 전용 조회 결과이며, 별도 테이블이 아니다.
/// </summary>
public class InventoryStatusItem
{
    public required string InventoryId { get; set; }
    public required string ProductId { get; set; }
    public required string ProductName { get; set; }
    public string? GenericName { get; set; }
    public string? Barcode { get; set; }
    public string? InternalBarcode { get; set; }
    public required string BatchNumber { get; set; }
    public required long ExpiryDate { get; set; }
    public required int CurrentQuantity { get; set; }
    public required decimal SellingPrice { get; set; }
    public required int SafetyStockLevel { get; set; }
    public required long UpdatedAt { get; set; }

    /// <summary>화면 표시 편의 계산 속성. Screen §4절 "저재고 판단" 규칙 그대로.</summary>
    public bool IsLowStock => CurrentQuantity < SafetyStockLevel;
}