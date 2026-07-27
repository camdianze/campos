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
}