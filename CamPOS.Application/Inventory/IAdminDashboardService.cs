namespace PharmaPOS.Application.Inventory;

/// <summary>
/// F-14 관리자 대시보드 지표 조합을 담당하는 인터페이스. (Screen SCR-ADMIN-015)
/// </summary>
public interface IAdminDashboardService
{
    Task<DashboardMetrics> GetDashboardMetricsAsync(string facilityId);
}