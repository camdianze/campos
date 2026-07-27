using PharmaPOS.Application.Repositories;

namespace PharmaPOS.Application.Inventory;

/// <summary>
/// IAdminDashboardService의 구현체.
/// Screen SCR-ADMIN-015, 4절의 6개 지표를 조합한다.
/// Low Stock/Expiry Alert 개수는 F-09의 IAlertRepository를 재사용한다.
/// </summary>
public class AdminDashboardService : IAdminDashboardService
{
    private readonly IAdminDashboardRepository _dashboardRepository;
    private readonly IAlertRepository _alertRepository;

    public AdminDashboardService(IAdminDashboardRepository dashboardRepository, IAlertRepository alertRepository)
    {
        _dashboardRepository = dashboardRepository;
        _alertRepository = alertRepository;
    }

    public async Task<DashboardMetrics> GetDashboardMetricsAsync(string facilityId)
    {
        // "오늘"은 로컬 시간대 자정을 기준으로 계산한다.
        var localNow = DateTime.Now;
        var localTodayStart = new DateTime(localNow.Year, localNow.Month, localNow.Day, 0, 0, 0, DateTimeKind.Local);
        var localTodayEnd = localTodayStart.AddDays(1);

        var todayStartUtc = new DateTimeOffset(localTodayStart).ToUnixTimeMilliseconds();
        var todayEndUtc = new DateTimeOffset(localTodayEnd).ToUnixTimeMilliseconds();

        var (dailySalesAmount, dailyTransactionCount) =
            await _dashboardRepository.GetDailySalesAsync(facilityId, todayStartUtc, todayEndUtc);

        var activeProductCount = await _dashboardRepository.GetActiveProductCountAsync();
        var totalInventoryValue = await _dashboardRepository.GetTotalInventoryValueAsync(facilityId);

        var lowStockCandidates = await _alertRepository.GetLowStockCandidatesAsync(facilityId);
        var expiryCandidates = await _alertRepository.GetExpiryCandidatesAsync(facilityId);

        return new DashboardMetrics
        {
            DailySalesAmount = dailySalesAmount,
            DailyTransactionCount = dailyTransactionCount,
            TotalActiveProducts = activeProductCount,
            LowStockAlertCount = lowStockCandidates.Count,
            ExpiryAlertCount = expiryCandidates.Count,
            TotalInventoryValue = totalInventoryValue
        };
    }
}