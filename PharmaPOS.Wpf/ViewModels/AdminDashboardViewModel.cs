using PharmaPOS.Application.Inventory;
using Lightweight_Digital_Inventory_Management___POS_System.ViewModels.Base;

namespace Lightweight_Digital_Inventory_Management___POS_System.ViewModels;

/// <summary>
/// 관리자 대시보드 화면(SCR-ADMIN-015)의 ViewModel.
/// </summary>
public class AdminDashboardViewModel : ViewModelBase
{
    private readonly IAdminDashboardService _dashboardService;
    private readonly string _facilityId;

    private DashboardMetrics? _metrics;
    private string _message = string.Empty;

    public DashboardMetrics? Metrics
    {
        get => _metrics;
        private set => SetProperty(ref _metrics, value);
    }

    public string Message
    {
        get => _message;
        set => SetProperty(ref _message, value);
    }

    public RelayCommand ProductManagementCommand { get; }
    public RelayCommand UserManagementCommand { get; }
    public RelayCommand InventoryOverviewCommand { get; }
    public RelayCommand SalesHistoryCommand { get; }
    public RelayCommand ReportsCommand { get; }
    public RelayCommand BackupExportCommand { get; }
    public RelayCommand BackCommand { get; }

    public event Action? NavigateToProductManagement;
    public event Action? NavigateToUserManagement;
    public event Action? NavigateToInventoryOverview;
    public event Action? NavigateToSalesHistory;
    public event Action? NavigateToBackupExport;
    public event Action? NavigateBack;

    public AdminDashboardViewModel(IAdminDashboardService dashboardService, string facilityId)
    {
        _dashboardService = dashboardService;
        _facilityId = facilityId;

        ProductManagementCommand = new RelayCommand(_ => NavigateToProductManagement?.Invoke());
        UserManagementCommand = new RelayCommand(_ => NavigateToUserManagement?.Invoke());
        InventoryOverviewCommand = new RelayCommand(_ => NavigateToInventoryOverview?.Invoke());
        SalesHistoryCommand = new RelayCommand(_ => NavigateToSalesHistory?.Invoke());

        // 아직 화면이 없는 기능들: 자리만 만들고 클릭 시 안내 메시지만 표시한다.
        ReportsCommand = new RelayCommand(_ => Message = "Reports screen is not yet available.");
        BackupExportCommand = new RelayCommand(_ => NavigateToBackupExport?.Invoke());

        BackCommand = new RelayCommand(_ => NavigateBack?.Invoke());

        _ = ReloadAsync();
    }

    public async Task ReloadAsync()
    {
        try
        {
            Metrics = await _dashboardService.GetDashboardMetricsAsync(_facilityId);
        }
        catch (Exception)
        {
            Message = "Admin dashboard could not be loaded.";
        }
    }
}