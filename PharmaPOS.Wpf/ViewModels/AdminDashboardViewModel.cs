using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.Win32;
using PharmaPOS.Application.Sync;
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

    private readonly SyncPayloadBuilder _syncPayloadBuilder;

    /// <summary>
    /// 서버로 보낼 묶음을 만들어 파일로 적는다. <b>보내지는 않는다.</b>
    ///
    /// 동기화에서 제일 위험한 것은 무엇을 보내는가이고, 한 번 보낸 것은 되돌릴 수 없다.
    /// 그래서 전송을 붙이기 전에 그 내용을 눈으로 확인할 수 있게 해 둔다.
    /// </summary>
    public RelayCommand PreviewSyncDataCommand { get; }

    /// <summary>미리보기 결과 한 줄. 어디에 적었는지와 몇 건인지.</summary>
    public string SyncPreviewMessage
    {
        get => _syncPreviewMessage;
        private set => SetProperty(ref _syncPreviewMessage, value);
    }

    private string _syncPreviewMessage = string.Empty;

    private async Task ExecutePreviewSyncDataAsync()
    {
        SyncPreviewMessage = string.Empty;

        var dialog = new OpenFolderDialog { Title = "Where to write the preview file" };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            var payload = await _syncPayloadBuilder.BuildAsync(_facilityId);

            // 사람이 읽을 파일이므로 들여쓴다. 실제 전송은 들여쓰지 않는다.
            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });

            var path = Path.Combine(
                dialog.FolderName,
                $"sync-preview-{DateTimeOffset.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)}.json");

            await File.WriteAllTextAsync(path, json, new UTF8Encoding(true));

            SyncPreviewMessage =
                $"{payload.RowCount:N0} rows written to {Path.GetFileName(path)} "
                + $"(products {payload.Products.Count:N0}, inventory {payload.Inventory.Count:N0}, "
                + $"staff {payload.Users.Count:N0}, transactions {payload.Transactions.Count:N0}, "
                + $"counselling {payload.CounsellingLogs.Count:N0})";
        }
        catch (Exception exception)
        {
            // 미리보기는 아무것도 바꾸지 않으므로, 실패해도 사유만 보여주고 넘어간다.
            SyncPreviewMessage = "The preview could not be written: " + exception.Message;
        }
    }

    public AdminDashboardViewModel(
        IAdminDashboardService dashboardService,
        IReceiptSettingsService receiptSettingsService,
        SyncPayloadBuilder syncPayloadBuilder,
        string facilityId)
    {
        _dashboardService = dashboardService;
        _receiptSettingsService = receiptSettingsService;
        _syncPayloadBuilder = syncPayloadBuilder;
        _facilityId = facilityId;

        ProductManagementCommand = new RelayCommand(_ => NavigateToProductManagement?.Invoke());
        UserManagementCommand = new RelayCommand(_ => NavigateToUserManagement?.Invoke());
        InventoryOverviewCommand = new RelayCommand(_ => NavigateToInventoryOverview?.Invoke());
        SalesHistoryCommand = new RelayCommand(_ => NavigateToSalesHistory?.Invoke());

        ReportsCommand = new RelayCommand(_ => NavigateToReports?.Invoke());
        BackupExportCommand = new RelayCommand(_ => NavigateToBackupExport?.Invoke());

        PreviewSyncDataCommand = new RelayCommand(async _ => await ExecutePreviewSyncDataAsync());

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