namespace PharmaPOS.Application.Inventory;

/// <summary>
/// 알림 화면(SCR-ALERT-014)에 표시할 통합 알림 항목.
/// LowStock은 상품 단위(배치 합산), Expiry는 배치 단위이므로
/// BatchNumber/ExpiryDate는 LowStock에서는 null이다.
/// </summary>
public class AlertItem
{
    public required AlertType AlertType { get; set; }
    public required AlertPriority Priority { get; set; }
    public required string ProductId { get; set; }
    public required string ProductName { get; set; }
    public required int Quantity { get; set; }

    /// <summary>LowStock 알림에서만 사용.</summary>
    public int? SafetyStockLevel { get; set; }

    /// <summary>Expiry 알림에서만 사용.</summary>
    public string? BatchNumber { get; set; }

    /// <summary>Expiry 알림에서만 사용.</summary>
    public long? ExpiryDate { get; set; }
}