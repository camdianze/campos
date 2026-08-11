namespace PharmaPOS.Application.Reports;

/// <summary>
/// 상품별 판매 순위 한 줄. 현재 기간과 직전 기간을 나란히 담는다.
/// </summary>
public class ProductSalesRow
{
    public required string ProductId { get; init; }
    public required string ProductName { get; init; }
    public string? GenericName { get; init; }
    public string? Strength { get; init; }

    /// <summary>낱개 기준 판매 수량.</summary>
    public int Quantity { get; init; }
    public decimal Amount { get; init; }

    public int PreviousQuantity { get; init; }
    public decimal PreviousAmount { get; init; }

    /// <summary>화면에서 고른 정렬 기준의 순위. ViewModel이 늘어놓으면서 채운다.</summary>
    public int Rank { get; set; }

    public string AmountChange => PeriodChange.Format(Amount, PreviousAmount);
    public string QuantityChange => PeriodChange.Format(Quantity, PreviousQuantity);
}
