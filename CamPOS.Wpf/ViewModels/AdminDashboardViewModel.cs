using System.Globalization;
using CamPOS.Application;
using CamPOS.Application.Repositories;
using CamPOS.Application.Settings;
using CamPOS.Application.Updates;
using System.IO;
using System.Text;
using Microsoft.Win32;
using CamPOS.Application.Inventory;
using CamPOS.Application.Receipts;
using CamPOS.Wpf.ViewModels.Base;

namespace CamPOS.Wpf.ViewModels;

/// <summary>
/// 관리자 대시보드 화면(SCR-ADMIN-015)의 ViewModel.
/// </summary>
public class AdminDashboardViewModel : ViewModelBase
{
    private readonly IAdminDashboardService _dashboardService;
    private readonly IReceiptSettingsService _receiptSettingsService;
    private readonly IUpdateService _updateService;
    private readonly IAppSettingRepository _appSettings;
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

    // ── 자동 업데이트 ────────────────────────────────────────────────────
    // 약국에게는 묻지도 알리지도 않는다. 그래서 "이 PC가 새 버전을 받았나"를
    // 알 수 있는 자리가 여기뿐이고, 선유님이 전화로 물어볼 수 있어야 한다.

    public RelayCommand CheckForUpdateCommand { get; }

    public IReadOnlyList<string> UpdateChannels { get; } =
        [Application.Updates.UpdateChannels.Stable, Application.Updates.UpdateChannels.Test];

    public string UpdateCurrentVersion
    {
        get => _updateCurrentVersion;
        private set => SetProperty(ref _updateCurrentVersion, value);
    }

    public string UpdateLastChecked
    {
        get => _updateLastChecked;
        private set => SetProperty(ref _updateLastChecked, value);
    }

    public string UpdateDownloaded
    {
        get => _updateDownloaded;
        private set => SetProperty(ref _updateDownloaded, value);
    }

    public string UpdateMessage
    {
        get => _updateMessage;
        private set => SetProperty(ref _updateMessage, value);
    }

    /// <summary>접힌 채로 보이는 한 줄. 펴 봐야 아는 정보는 아무도 안 본다.</summary>
    public string UpdateHeaderSummary
    {
        get => _updateHeaderSummary;
        private set => SetProperty(ref _updateHeaderSummary, value);
    }

    public bool IsUpdateIdle
    {
        get => _isUpdateIdle;
        private set => SetProperty(ref _isUpdateIdle, value);
    }

    /// <summary>
    /// 고르는 즉시 저장한다. "저장" 버튼을 따로 두면 안 누르고 나가고, 그러면
    /// 바꿨다고 믿는 채널과 실제로 보는 채널이 달라진다.
    /// </summary>
    public string UpdateChannel
    {
        get => _updateChannel;
        set
        {
            if (SetProperty(ref _updateChannel, value) && !_isLoadingUpdateStatus)
            {
                _ = SaveUpdateChannelAsync(value);
            }
        }
    }

    private string _updateCurrentVersion = AppVersion.Number;
    private string _updateLastChecked = "Never";
    private string _updateDownloaded = "Nothing";
    private string _updateMessage = string.Empty;
    private string _updateHeaderSummary = string.Empty;
    private string _updateChannel = Application.Updates.UpdateChannels.Default;
    private bool _isUpdateIdle = true;
    private bool _isLoadingUpdateStatus;

    private async Task SaveUpdateChannelAsync(string channel)
    {
        try
        {
            await _appSettings.SetAsync(
                AppSettingKeys.UpdateChannel,
                Application.Updates.UpdateChannels.Normalize(channel),
                "enum");

            UpdateMessage = channel == Application.Updates.UpdateChannels.Test
                ? "This computer will now receive test builds."
                : "This computer will receive released versions only.";
        }
        catch (Exception exception)
        {
            UpdateMessage = "The channel could not be saved: " + exception.Message;
        }
    }

    private void ShowUpdateStatus(UpdateStatus status)
    {
        _isLoadingUpdateStatus = true;

        try
        {
            UpdateCurrentVersion = status.IsInstalled
                ? status.CurrentVersion
                : status.CurrentVersion + "  (not installed by the updater)";

            // 사람이 눈으로만 보는 글자라 PC의 지역 설정을 따른다.
            UpdateLastChecked = status.LastChecked is null
                ? "Never"
                : status.LastChecked.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm");

            UpdateDownloaded = status.DownloadedVersion is null
                ? "Nothing"
                : status.DownloadedVersion + " — installs when CamPOS is closed";

            UpdateChannel = status.Channel;
            UpdateMessage = status.LastMessage;

            UpdateHeaderSummary = status.DownloadedVersion is null
                ? status.CurrentVersion
                : $"{status.CurrentVersion} → {status.DownloadedVersion} on next close";
        }
        finally
        {
            _isLoadingUpdateStatus = false;
        }
    }

    private async Task ExecuteCheckForUpdateAsync()
    {
        IsUpdateIdle = false;
        UpdateMessage = "Checking…";

        try
        {
            ShowUpdateStatus(await _updateService.CheckNowAsync());
        }
        catch (Exception exception)
        {
            UpdateMessage = "The check failed: " + exception.Message;
        }
        finally
        {
            IsUpdateIdle = true;
        }
    }

    public AdminDashboardViewModel(
        IAdminDashboardService dashboardService,
        IReceiptSettingsService receiptSettingsService,
        IUpdateService updateService,
        IAppSettingRepository appSettings,
        string facilityId)
    {
        _dashboardService = dashboardService;
        _receiptSettingsService = receiptSettingsService;
        _updateService = updateService;
        _appSettings = appSettings;
        _facilityId = facilityId;

        ProductManagementCommand = new RelayCommand(_ => NavigateToProductManagement?.Invoke());
        UserManagementCommand = new RelayCommand(_ => NavigateToUserManagement?.Invoke());
        InventoryOverviewCommand = new RelayCommand(_ => NavigateToInventoryOverview?.Invoke());
        SalesHistoryCommand = new RelayCommand(_ => NavigateToSalesHistory?.Invoke());

        ReportsCommand = new RelayCommand(_ => NavigateToReports?.Invoke());
        BackupExportCommand = new RelayCommand(_ => NavigateToBackupExport?.Invoke());

        CheckForUpdateCommand = new RelayCommand(async _ => await ExecuteCheckForUpdateAsync());
        ShowUpdateStatus(_updateService.GetStatus());

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