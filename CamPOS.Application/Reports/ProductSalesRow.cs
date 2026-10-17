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

    /// <summary>
    /// 같은 기간 상품 매출 전체. 한 줄만으로는 알 수 없는 값이라 ViewModel이 채운다
    /// (Rank와 같은 이유·같은 자리에서).
    /// </summary>
    public decimal TotalAmountInPeriod { get; set; }

    /// <summary>
    /// 이 상품이 기간 매출에서 차지하는 비중. 분모는 이 표에 실린 줄들의 합이라
    /// 표의 비중을 다 더하면 100%가 된다.
    /// </summary>
    public string AmountShare => PeriodChange.FormatShare(Amount, TotalAmountInPeriod);

    public string AmountChange => PeriodChange.Format(Amount, PreviousAmount);
    public string QuantityChange => PeriodChange.Format(Quantity, PreviousQuantity);

    public ChangeDirection AmountDirection => PeriodChange.DirectionOf(Amount, PreviousAmount);
    public ChangeDirection QuantityDirection => PeriodChange.DirectionOf(Quantity, PreviousQuantity);
}
