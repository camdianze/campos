using PharmaPOS.Domain.Enums;

namespace PharmaPOS.Domain.Entities;

/// <summary>
/// PRD의 Product Master 테이블에 대응하는 엔티티.
/// F-03 Product Master Management에서 사용한다.
/// </summary>
public class Product
{
    public required string ProductId { get; set; }

    /// <summary>
    /// 제조사 바코드. 없을 수 있다 (nullable) — 이 경우 InternalBarcode를 사용한다.
    /// </summary>
    public string? Barcode { get; set; }

    /// <summary>
    /// 자동 생성되는 내부 바코드. 형식: INT-XXXXXXXX.
    /// 제조사 바코드가 없는 상품에 한해 생성된다.
    /// </summary>
    public string? InternalBarcode { get; set; }

    public required string ProductName { get; set; }

    public string? GenericName { get; set; }

    public string? Strength { get; set; }

    public required string Unit { get; set; }

    public string? Manufacturer { get; set; }

    public string? CountryOfOrigin { get; set; }

    public required decimal CostPrice { get; set; }

    public required decimal SellingPrice { get; set; }

    public required int SafetyStockLevel { get; set; }

    public required EntityStatus Status { get; set; }

    public required long CreatedAt { get; set; }

    /// <summary>
    /// WHO ATC 코드. 항생제 복약안내(AMR) 판별에 쓴다.
    /// 값이 있으면 성분명 매칭보다 우선한다 — 표기 흔들림이 없기 때문이다.
    /// 항생제가 아닌 상품은 비워 둔다.
    /// </summary>
    public string? AtcCode { get; set; }

    /// <summary>
    /// 복합제 여부. 성분은 여럿이어도 AWaRe 분류는 하나이므로,
    /// 조합 자체의 ATC 코드를 우선 사용한다는 점을 표시해 둔다.
    /// </summary>
    public bool IsCombination { get; set; }
}