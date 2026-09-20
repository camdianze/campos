using PharmaPOS.Application.Inventory;
using PharmaPOS.Application.Receipts;
using Lightweight_Digital_Inventory_Management___POS_System.ViewModels.Base;

namespace Lightweight_Digital_Inventory_Management___POS_System.ViewModels;

/// <summary>
/// 관리자 대시보드 화면(SCR-ADMIN-015)의 ViewModel.
/// </summary>
public class AdminDashboardViewModel : ViewModelBase
{
    private readonly IAdminDashboardService _dashboardService;
    private readonly IReceiptSettingsService _receiptSettingsService;
    private readonly string _facilityId;

    private DashboardMetrics? _metrics;
    private string _message = string.Empty;

    // 통화 설정. 카드의 금액은 달러이고 리엘은 화면 환산이다.
    private decimal _exchangeRate;
    private int _rielRounding = 100;
    private bool _showRiel;

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

    public bool IsRielShown => _showRiel && _exchangeRate > 0;

    /// <summary>오늘 매출의 리엘 환산.</summary>
    public string DailySalesInRiel =>
        IsRielShown && Metrics is not null
            ? RielConverter.Format(Metrics.DailySalesAmount, _exchangeRate, _rielRounding)
            : string.Empty;

    /// <summary>
    /// 어느 환율로 환산했는지. 오늘 매출이라 환율이 바뀔 틈은 없지만, 리포트와 같은
    /// 자리에 같은 모양으로 적어 두는 편이 읽는 사람에게 일관된다.
    /// </summary>
    public string ExchangeRateNote => IsRielShown
        ? $"1 USD = {_exchangeRate.ToString("N0", System.Globalization.CultureInfo.InvariantCulture)} "
          + RielConverter.RielSymbol
        : string.Empty;

    private async Task LoadCurrencySettingsAsync()
    {
        try
        {
            var settings = await _receiptSettingsService.GetAsync();

            _exchangeRate = settings.ExchangeRate;
            _rielRounding = settings.RielRounding;
            _showRiel = settings.ShowRiel;
        }
        catch (Exception)
        {
            _showRiel = false;
        }

        OnPropertyChanged(nameof(IsRielShown));
        OnPropertyChanged(nameof(DailySalesInRiel));
        OnPropertyChanged(nameof(ExchangeRateNote));
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
    public event Action? NavigateToReports;
    public event Action? NavigateToBackupExport;
    public event Action? NavigateBack;

    public AdminDashboardViewModel(
        IAdminDashboardService dashboardService,
        IReceiptSettingsService receiptSettingsService,
        string facilityId)
    {
        _dashboardService = dashboardService;
        _receiptSettingsService = receiptSettingsService;
        _facilityId = facilityId;

        ProductManagementCommand = new RelayCommand(_ => NavigateToProductManagement?.Invoke());
        UserManagementCommand = new RelayCommand(_ => NavigateToUserManagement?.Invoke());
        InventoryOverviewCommand = new RelayCommand(_ => NavigateToInventoryOverview?.Invoke());
        SalesHistoryCommand = new RelayCommand(_ => NavigateToSalesHistory?.Invoke());

        ReportsCommand = new RelayCommand(_ => NavigateToReports?.Invoke());
        BackupExportCommand = new RelayCommand(_ => NavigateToBackupExport?.Invoke());

        BackCommand = new RelayCommand(_ => NavigateBack?.Invoke());

        _ = LoadCurrencySettingsAsync();
        _ = ReloadAsync();
    }

    public async Task ReloadAsync()
    {
        try
        {
            Metrics = await _dashboardService.GetDashboardMetricsAsync(_facilityId);
            OnPropertyChanged(nameof(DailySalesInRiel));
        }
        catch (Exception)
        {
            Message = "Admin dashboard could not be loaded.";
        }
    }
}