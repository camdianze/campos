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

    /// <summary>아직 뜯지 않은 박스 수. UnitsPerBox가 1인 상품은 늘 0이다.</summary>
    public int BoxQuantity { get; set; }

    /// <summary>헐어 놓은 낱개 수. UnitsPerBox가 1인 상품은 여기에 전량이 들어간다.</summary>
    public int UnitQuantity { get; set; }

    /// <summary>상품 마스터의 박스당 낱개 수. 화면이 박스/낱개를 나눠 보여줄지 판단한다.</summary>
    public int UnitsPerBox { get; set; } = 1;

    /// <summary>상품 마스터의 판매가. 박스로 파는 상품이면 박스 하나의 가격이다.</summary>
    public required decimal SellingPrice { get; set; }

    /// <summary>따로 정해 둔 낱개 판매가. 없으면 박스가를 나눠 쓴다.</summary>
    public decimal? UnitSellingPrice { get; set; }

    public required int SafetyStockLevel { get; set; }
    public required long UpdatedAt { get; set; }

    /// <summary>박스/낱개를 나눠 보여줘야 하는 상품인지.</summary>
    public bool IsBoxedProduct => UnitsPerBox > 1;

    /// <summary>
    /// 화면에 보여줄 단가. 재고 수량이 낱개 기준이라 단가도 낱개로 맞춘다 —
    /// 박스가를 그대로 두면 "299개"와 "45,000"이 나란히 놓여 곱셈이 엉뚱해진다.
    /// </summary>
    public decimal DisplayUnitPrice =>
        UnitSellingPrice ?? (IsBoxedProduct ? SellingPrice / UnitsPerBox : SellingPrice);

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