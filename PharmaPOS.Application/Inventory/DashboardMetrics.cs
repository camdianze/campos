namespace PharmaPOS.Application.Inventory;

/// <summary>
/// 관리자 대시보드(SCR-ADMIN-015)에 표시할 6개 지표.
/// </summary>
public class DashboardMetrics
{
    public required decimal DailySalesAmount { get; set; }
    public required int DailyTransactionCount { get; set; }
    public required int TotalActiveProducts { get; set; }
    public required int LowStockAlertCount { get; set; }
    public required int ExpiryAlertCount { get; set; }
    public required decimal TotalInventoryValue { get; set; }

    /// <summary>
    /// 카드에 적는 재고 자산가치. 반올림한다.
    ///
    /// 원본을 그대로 내보내면 256317.690433333처럼 나온다 — 낱개 원가가
    /// "박스 원가 ÷ 박스당 개수"라 나누어떨어지지 않고, 그 나머지가 상품 수만큼 쌓인다.
    /// 자릿수를 세어야 읽히는 숫자는 한눈에 보라고 만든 카드의 목적을 잃는다.
    /// 반올림은 보여줄 때만 하고 저장된 값은 건드리지 않는다.
    /// </summary>
    public string TotalInventoryValueDisplay =>
        TotalInventoryValue.ToString("N2", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>같은 이유로 매출 카드도 통화 자릿수로 맞춘다.</summary>
    public string DailySalesAmountDisplay =>
        DailySalesAmount.ToString("N2", System.Globalization.CultureInfo.InvariantCulture);
}