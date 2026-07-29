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

    /// <summary>
    /// 만료임박으로 보는 기간(일). 재고 조회의 Within90Days 필터, 알림 조회와 같은 값이다.
    /// 세 곳이 따로 놀면 화면에는 임박 표시가 없는데 알림은 뜨는 식이 되므로 여기로 모았다.
    /// </summary>
    public const int ExpiringSoonDays = 90;

    private const long MillisecondsPerDay = 86_400_000L;

    /// <summary>화면 표시 편의 계산 속성. Screen §4절 "저재고 판단" 규칙 그대로.</summary>
    public bool IsLowStock => CurrentQuantity < SafetyStockLevel;

    /// <summary>유통기한이 이미 지났는지. 만료일이 없으면(0) false.</summary>
    public bool IsExpired =>
        ExpiryDate > 0 && ExpiryDate <= DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    /// <summary>아직 안 지났지만 ExpiringSoonDays 안에 만료되는지. 이미 만료된 건 제외한다.</summary>
    public bool IsExpiringSoon
    {
        get
        {
            if (ExpiryDate <= 0) return false;

            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            return ExpiryDate > now
                && ExpiryDate - now <= ExpiringSoonDays * MillisecondsPerDay;
        }
    }
}