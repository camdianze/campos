using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using Microsoft.Win32;
using CamPOS.Application;
using CamPOS.Application.Import;
using CamPOS.Application.Inventory;
using CamPOS.Application.Sync;
using CamPOS.Domain.Enums;
using CamPOS.Wpf.Services;
using CamPOS.Wpf.ViewModels.Base;
using CamPOS.Wpf.Views;

namespace CamPOS.Wpf.ViewModels;

/// <summary>
/// Data Center 화면의 ViewModel.
///
/// 화면은 왼쪽이 "파일 → 앱"(가져오기), 오른쪽이 "앱 → 파일"(내보내기·백업),
/// 그 아래가 서버 동기화이고, 현재 데이터를 통째로 갈아엎는 복원만 맨 아래
/// 위험 구역에 따로 둔다. 결과의 무게가 서로 달라 한 덩어리로 섞으면 잘못
/// 누르기 쉽다.
///
/// 동기화가 여기 있는 이유는 이 화면이 <b>데이터가 앱을 드나드는 곳 전부</b>를
/// 모아 둔 자리이기 때문이다. 관리자 대시보드에 따로 두었더니 백업을 찾던
/// 사람이 그 둘을 다른 종류의 일로 읽었다. 화면별 Export(판매 내역·재고
/// 이력·알림·보고서)는 그 자리에 남는다 — 그것들의 값어치는 <b>걸러 놓은
/// 목록 그대로</b>를 내보내는 데 있어서, 여기로 옮기면 그 화면들의 필터를
/// 여기에 다시 만들어야 한다.
/// </summary>
public class BackupExportViewModel : ViewModelBase
{
    /// <summary>오류 목록을 대화상자에 몇 줄까지 펼칠지. 그 뒤는 개수만 알린다.</summary>
    private const int MaxIssueLinesInDialog = 15;

    private readonly IBackupService _backupService;
    private readonly IInitialImportService _initialImportService;
    private readonly IPhotoImportService _photoImportService;
    private readonly string _facilityId;
    private readonly string _userId;

    private string _importFilePath = string.Empty;

    private string _exportFolder = string.Empty;
    private bool _exportProducts = true;
    private bool _exportInventory = true;
    private bool _exportSalesHistory = true;
    private bool _isCsvFormat = true;
    private DateTime? _exportDateFrom;
    private DateTime? _exportDateTo;

    private ImportPriceCurrency _importPriceCurrency = ImportPriceCurrency.Riel;
    private bool _assumeLoosePrices;
    private string _backupFilePath = string.Empty;
    private string _message = string.Empty;

    // ── 가져오기 ────────────────────────────────────────────────────────────

    /// <summary>가져올 파일. 1단계(상품)와 2단계(재고)가 같은 파일을 쓴다.</summary>
    public string ImportFilePath
    {
        get => _importFilePath;
        set => SetProperty(ref _importFilePath, value);
    }

    /// <summary>
    /// 표시가 없는 가격 칸을 무엇으로 읽을지. <b>화면이 처음 고르는 값은 리엘이다</b> —
    /// 이 약국이 받는 실사 시트가 리엘로 적혀 있기 때문이고, 그 사실은 코드가 아니라
    /// 약국이 정한다.
    ///
    /// Application 쪽 기본값(<c>PlanProductsAsync</c>)은 달러 그대로다. 둘은 다른 것을
    /// 정한다 — 저쪽은 "통화를 말하지 않으면 무슨 뜻인가"라는 해석 규칙이고, 이쪽은
    /// "이 약국의 시트가 무엇으로 적혀 있나"라는 사실이다. 해석 규칙을 바꾸면 그 규칙을
    /// 전제로 쓴 코드와 테스트가 함께 흔들린다.
    ///
    /// 칸마다 "$3.00"처럼 적는 방법은 그대로 살아 있고, 그쪽이 이긴다. 다만 실제로
    /// 받는 시트는 통째로 한 통화라, 200줄에 표시를 붙이다 한 줄을 빠뜨리면 그 상품만
    /// 4,000배로 들어간다. 그 값은 0보다 크고 자릿수도 맞아서 어떤 검사에도 안 걸린다.
    ///
    /// 달러로 적힌 파일을 넣을 일이 있다면 — 이 앱이 <i>내보낸</i> 상품 파일이 그렇다 —
    /// 누르기 전에 $로 바꿔야 한다. 미리보기 첫 줄이 무엇으로 읽었는지 알려 준다.
    /// </summary>
    public ImportPriceCurrency ImportPriceCurrency
    {
        get => _importPriceCurrency;
        private set
        {
            if (SetProperty(ref _importPriceCurrency, value))
            {
                OnPropertyChanged(nameof(IsUsdImport));
                OnPropertyChanged(nameof(IsRielImport));
            }
        }
    }

    public bool IsUsdImport => ImportPriceCurrency == ImportPriceCurrency.Usd;
    public bool IsRielImport => ImportPriceCurrency == ImportPriceCurrency.Riel;

    /// <summary>
    /// 낱개가가 비어 있는 행에 "박스가 ÷ 박스당 개수를 지불 단위로 올린 값"을 넣을지.
    /// 약국이 낱개가를 그렇게 정한다고 밝힌 경우에만 켠다.
    ///
    /// 기본은 꺼짐이다. 계산해서 나온 값은 약국이 매긴 가격이 아니고 실제로는
    /// 상품마다 크게 다르다 — 박스를 헐어 파는 데 붙이는 마진을 앱이 알 수는 없다.
    /// 켜 두더라도 <b>이미 정해 둔 낱개가는 덮지 않고</b>, 미리보기가 몇 건이
    /// 이렇게 들어가는지 먼저 말한다.
    /// </summary>
    public bool AssumeLoosePrices
    {
        get => _assumeLoosePrices;
        set => SetProperty(ref _assumeLoosePrices, value);
    }

    public RelayCommand SelectImportFileCommand { get; }
    public RelayCommand ReadPricesInUsdCommand { get; }
    public RelayCommand ReadPricesInRielCommand { get; }
    public RelayCommand ImportProductsCommand { get; }
    public RelayCommand ImportInventoryCommand { get; }
    public RelayCommand ImportPhotosCommand { get; }

    // ── 내보내기 ────────────────────────────────────────────────────────────

    /// <summary>내보내기와 백업 파일이 저장될 폴더.</summary>
    public string ExportFolder
    {
        get => _exportFolder;
        set => SetProperty(ref _exportFolder, value);
    }

    public bool ExportProducts
    {
        get => _exportProducts;
        set => SetProperty(ref _exportProducts, value);
    }

    public bool ExportInventory
    {
        get => _exportInventory;
        set => SetProperty(ref _exportInventory, value);
    }

    public bool ExportSalesHistory
    {
        get => _exportSalesHistory;
        set => SetProperty(ref _exportSalesHistory, value);
    }

    /// <summary>
    /// 판매 내역에만 걸리는 기간이다. 상품은 현재 카탈로그라 잘라내면 다시 가져올 수 없고,
    /// 재고는 배치별 현재 수량이라 기간이라는 게 없다 (ExportDatasets.SupportsDateRange).
    /// </summary>
    public DateTime? ExportDateFrom
    {
        get => _exportDateFrom;
        set => SetProperty(ref _exportDateFrom, value);
    }

    public DateTime? ExportDateTo
    {
        get => _exportDateTo;
        set => SetProperty(ref _exportDateTo, value);
    }

    public bool IsCsvFormat
    {
        get => _isCsvFormat;
        set => SetProperty(ref _isCsvFormat, value);
    }

    public RelayCommand SelectExportFolderCommand { get; }
    public RelayCommand ExportDataCommand { get; }
    public RelayCommand CreateDbBackupCommand { get; }

    // ── 복원 ────────────────────────────────────────────────────────────────

    public string BackupFilePath
    {
        get => _backupFilePath;
        set => SetProperty(ref _backupFilePath, value);
    }

    public RelayCommand SelectBackupFileCommand { get; }
    public RelayCommand RestoreDbCommand { get; }

    public string Message
    {
        get => _message;
        set => SetProperty(ref _message, value);
    }

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

            // 전송과 같은 규칙으로 쓴다(SyncJson). 들여쓰기만 다르다 — 미리보기가
            // 전송과 다른 모양을 보여주면 미리 볼 이유가 없다.
            var json = JsonSerializer.Serialize(payload, SyncJson.PreviewOptions);

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

    // ── 전송 ─────────────────────────────────────────────────────────────

    public RelayCommand SynchronizeCommand { get; }

    /// <summary>
    /// 마지막으로 성공한 전송. 이 줄이 자동 동기화의 안전장치다 — 전송이 조용히
    /// 멈추면 앱은 평소와 똑같아 보이고, 그것을 알아차릴 사람은 그 PC 앞에 없다.
    /// </summary>
    public string SyncLastSuccessText
    {
        get => _syncLastSuccessText;
        private set => SetProperty(ref _syncLastSuccessText, value);
    }

    /// <summary>아직 안 보낸 행 수. 늘어나기만 하면 뭔가 멈춘 것이다.</summary>
    public string SyncPendingText
    {
        get => _syncPendingText;
        private set => SetProperty(ref _syncPendingText, value);
    }

    /// <summary>
    /// 자동 전송 스위치. 끄면 시계만 멈추고 Synchronize 버튼은 그대로 동작한다.
    /// 값을 바꾸는 즉시 저장한다 — 설정 화면이 아니라 체크박스 하나라 "저장"을
    /// 따로 누르게 하면 안 누르고 나간다.
    /// </summary>
    public bool IsAutoSyncOn
    {
        get => _isAutoSyncOn;
        set
        {
            if (SetProperty(ref _isAutoSyncOn, value) && !_isLoadingSyncSettings)
            {
                _ = SaveAutoSyncAsync(value);
            }
        }
    }

    private async Task SaveAutoSyncAsync(bool on)
    {
        try
        {
            await _syncService.SetAutomaticAsync(on);

            SyncStatusMessage = on
                ? "Automatic sync is on. It sends every 30 minutes."
                : "Automatic sync is off. Press Synchronize to send.";
        }
        catch (Exception exception)
        {
            SyncStatusMessage = "The setting could not be saved: " + exception.Message;
        }
    }

    private bool _isAutoSyncOn = true;
    private bool _isLoadingSyncSettings;
    private string _syncLastSuccessText = string.Empty;
    private string _syncPendingText = string.Empty;

    private async Task ExecuteSynchronizeAsync()
    {
        if (IsSyncBusy) return;

        IsSyncBusy = true;
        SyncStatusMessage = "Sending…";

        try
        {
            var result = await _syncService.RunAsync(_facilityId);

            SyncStatusMessage = result.IsSuccess ? result.Message : "Not sent. " + result.Message;

            await RefreshSyncStateAsync();
        }
        catch (Exception exception)
        {
            SyncStatusMessage = "Sync failed: " + exception.Message;
        }
        finally
        {
            IsSyncBusy = false;
        }
    }

    /// <summary>마지막 성공 시각과 밀린 행 수를 다시 읽는다.</summary>
    private async Task RefreshSyncStateAsync()
    {
        try
        {
            var last = await _syncService.GetLastSuccessAsync();

            // 사람이 눈으로만 보는 글자라 PC의 지역 설정을 따른다. 저장은
            // InvariantCulture로 된 epoch 밀리초다.
            SyncLastSuccessText = last is null
                ? "Never sent."
                : "Last sent " + last.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm");

            var pending = await _syncService.CountPendingAsync(_facilityId);
            var photos = await _syncService.CountPhotosPendingAsync();

            SyncPendingText = (pending, photos) switch
            {
                (< 0, _) or (_, < 0) => string.Empty,
                (0, 0) => "Nothing waiting.",
                (_, 0) => $"{pending:N0} rows waiting to be sent.",
                (0, _) => $"{photos:N0} photos waiting to be sent.",
                _ => $"{pending:N0} rows and {photos:N0} photos waiting to be sent.",
            };
        }
        catch
        {
            // 상태 표시를 못 읽은 것으로 화면을 망가뜨리지 않는다.
            SyncLastSuccessText = string.Empty;
            SyncPendingText = string.Empty;
        }
    }

    // ── 서버 연결 ────────────────────────────────────────────────────────
    // 세 칸 중 토큰만 비밀이고, 그것만 DPAPI로 암호화되어 저장된다. 주소와
    // publishable 키는 모든 약국이 같고 앱에 실려 나가도 되는 값이다.

    public string SyncServerUrl
    {
        get => _syncServerUrl;
        set => SetProperty(ref _syncServerUrl, value);
    }

    public string SyncPublishableKey
    {
        get => _syncPublishableKey;
        set => SetProperty(ref _syncPublishableKey, value);
    }

    /// <summary>연결 상태 한 줄. 어느 약국으로 연결되었는지까지 말한다.</summary>
    public string SyncStatusMessage
    {
        get => _syncStatusMessage;
        private set => SetProperty(ref _syncStatusMessage, value);
    }

    /// <summary>보내는 중에는 버튼을 잠근다. 두 번 눌러 두 묶음이 가지 않게.</summary>
    public bool IsSyncBusy
    {
        get => _isSyncBusy;
        private set
        {
            if (SetProperty(ref _isSyncBusy, value))
            {
                OnPropertyChanged(nameof(IsSyncIdle));
            }
        }
    }

    public bool IsSyncIdle => !_isSyncBusy;

    private string _syncServerUrl = string.Empty;
    private string _syncPublishableKey = string.Empty;
    private string _syncStatusMessage = string.Empty;
    private bool _isSyncBusy;

    /// <summary>
    /// 저장된 설정을 화면에 올린다. <b>토큰도 칸에 되돌려 넣는다</b> — 비워 두면
    /// 주소만 고치고 Save를 누른 사람이 토큰을 지우게 되고, 그 PC는 다음 전송에서야
    /// 조용히 거부된다. 제품 화면이 발급된 내부 바코드를 칸에 채워 두는 것과 같은
    /// 이유다.
    /// </summary>
    public async Task<string> LoadSyncSettingsAsync()
    {
        _isLoadingSyncSettings = true;

        try
        {
            var settings = await _syncSettingsService.LoadAsync();

            // 읽어 오는 동안 setter가 저장을 부르지 않게 한다. 화면을 열기만 해도
            // 설정이 다시 쓰이는 것은 조용하지만 틀린 일이다.
            IsAutoSyncOn = await _syncService.IsAutomaticAsync();

            SyncServerUrl = settings.ServerUrl;
            SyncPublishableKey = settings.PublishableKey;
            SyncStatusMessage = settings.IsConfigured
                ? "Saved. Press Test Connection to check it."
                : "Not connected to a server.";

            await RefreshSyncStateAsync();

            return settings.Token;
        }
        catch (Exception exception)
        {
            SyncStatusMessage = "The saved connection could not be read: " + exception.Message;
            return string.Empty;
        }
        finally
        {
            _isLoadingSyncSettings = false;
        }
    }

    public async Task SaveSyncSettingsAsync(string token)
    {
        IsSyncBusy = true;

        try
        {
            var result = await _syncSettingsService.SaveAsync(SyncServerUrl, SyncPublishableKey, token);

            // 주소를 다듬어 저장했다면 칸에도 다듬은 것을 보여준다 — 저장된 것과
            // 보이는 것이 다르면 다음에 여는 사람이 무엇이 쓰이는지 알 수 없다.
            if (result.IsSuccess)
            {
                SyncServerUrl = SyncSettings.NormalizeUrl(SyncServerUrl);
            }

            SyncStatusMessage = result.Message;
        }
        finally
        {
            IsSyncBusy = false;
        }
    }

    /// <summary>
    /// 행을 하나도 담지 않은 묶음을 실제로 보낸다. 따로 만든 "확인용" 경로가
    /// 아니라 전송과 같은 길이므로, 여기서 통과하면 주소·공개 키·토큰·네트워크가
    /// 전부 맞다는 뜻이다. 서버에는 전부 0인 전송 기록이 한 줄 남는데, 그것이
    /// 바로 "이 PC가 연결을 확인했다"는 기록이다.
    /// </summary>
    public async Task TestSyncConnectionAsync(string token)
    {
        IsSyncBusy = true;
        SyncStatusMessage = "Checking…";

        try
        {
            var settings = new SyncSettings
            {
                ServerUrl = SyncSettings.NormalizeUrl(SyncServerUrl),
                PublishableKey = (SyncPublishableKey ?? string.Empty).Trim(),
                Token = (token ?? string.Empty).Trim(),
            };

            if (!settings.IsConfigured)
            {
                SyncStatusMessage = "Fill in the address, the publishable key and the token first.";
                return;
            }

            var empty = new SyncPayload
            {
                AppVersion = AppVersion.Display,
                ClientTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            };

            var result = await _syncClient.SendAsync(settings, empty);

            SyncStatusMessage = result.IsSuccess
                ? $"Connected. The server knows this computer as {result.SiteId}."
                : "Not connected. " + result.Message;
        }
        finally
        {
            IsSyncBusy = false;
        }
    }

    private readonly SyncPayloadBuilder _syncPayloadBuilder;
    private readonly SyncSettingsService _syncSettingsService;
    private readonly ISyncClient _syncClient;
    private readonly SyncService _syncService;

    public BackupExportViewModel(
        IBackupService backupService,
        IInitialImportService initialImportService,
        IPhotoImportService photoImportService,
        SyncPayloadBuilder syncPayloadBuilder,
        SyncSettingsService syncSettingsService,
        ISyncClient syncClient,
        SyncService syncService,
        string facilityId,
        string userId)
    {
        _backupService = backupService;
        _initialImportService = initialImportService;
        _photoImportService = photoImportService;
        _syncPayloadBuilder = syncPayloadBuilder;
        _syncSettingsService = syncSettingsService;
        _syncClient = syncClient;
        _syncService = syncService;
        _facilityId = facilityId;
        _userId = userId;

        PreviewSyncDataCommand = new RelayCommand(async _ => await ExecutePreviewSyncDataAsync());
        SynchronizeCommand = new RelayCommand(async _ => await ExecuteSynchronizeAsync());

        SelectImportFileCommand = new RelayCommand(_ => ExecuteSelectImportFile());
        ReadPricesInUsdCommand = new RelayCommand(_ => ImportPriceCurrency = ImportPriceCurrency.Usd);
        ReadPricesInRielCommand = new RelayCommand(_ => ImportPriceCurrency = ImportPriceCurrency.Riel);
        ImportProductsCommand = new RelayCommand(async _ => await ExecuteImportProductsAsync());
        ImportInventoryCommand = new RelayCommand(async _ => await ExecuteImportInventoryAsync());
        ImportPhotosCommand = new RelayCommand(async _ => await ExecuteImportPhotosAsync());

        SelectExportFolderCommand = new RelayCommand(_ => ExecuteSelectExportFolder());
        ExportDataCommand = new RelayCommand(async _ => await ExecuteExportDataAsync());
        CreateDbBackupCommand = new RelayCommand(async _ => await ExecuteCreateDbBackupAsync());

        SelectBackupFileCommand = new RelayCommand(_ => ExecuteSelectBackupFile());
        RestoreDbCommand = new RelayCommand(async _ => await ExecuteRestoreDbAsync());
    }

    // ── 가져오기: 파일 → 앱 ─────────────────────────────────────────────────
    //
    // 실사 파일 하나로 상품(1단계)과 재고(2단계)를 차례로 넣는다. 두 단계를 나눈 이유는
    // 재고가 상품을 전제로 하기 때문이고, 그래서 같은 파일을 두 번 고르는 것이 정상 흐름이다.
    // 어느 단계든 순서는 같다: 같은 파일인지 확인 → 무엇이 들어가는지 보여주고 확인받기 → 반영.

    private void ExecuteSelectImportFile()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select Import File",
            Filter = "CSV/Excel (*.csv;*.xlsx)|*.csv;*.xlsx|CSV (*.csv)|*.csv|Excel (*.xlsx)|*.xlsx"
        };

        if (dialog.ShowDialog() == true)
        {
            ImportFilePath = dialog.FileName;
        }
    }

    private async Task ExecuteImportProductsAsync()
    {
        Message = string.Empty;

        var file = await TryReadImportFileAsync(ImportType.Products);

        if (file is null)
        {
            return;
        }

        var plan = await _initialImportService.PlanProductsAsync(
            file.Rows, ImportPriceCurrency, AssumeLoosePrices);

        if (plan.HasFileError)
        {
            AppDialog.Show("Import Products", plan.FileError!);
            return;
        }

        var preview = new StringBuilder();
        preview.AppendLine("STEP 1 — PRODUCTS");
        preview.AppendLine("--------------------------------");
        preview.AppendLine($"Prices read as        : {plan.PriceFormatDescription}");
        preview.AppendLine($"Rows in file          : {plan.TotalRows}");
        preview.AppendLine($"New products          : {plan.CreateCount}");
        preview.AppendLine($"Products to update    : {plan.UpdateCount}");
        preview.AppendLine($"Unchanged             : {plan.UnchangedCount}");
        preview.AppendLine($"Duplicate rows skipped: {plan.DuplicateRowCount}");
        preview.AppendLine($"Rows with errors      : {plan.ErrorRowCount}");

        // 계산해서 들어가는 가격은 적용 전에 반드시 말한다. 숫자만 보면 약국이
        // 정한 가격과 구별되지 않는다.
        if (plan.AssumedLoosePriceCount > 0)
        {
            preview.AppendLine();
            preview.AppendLine($"Loose price worked out: {plan.AssumedLoosePriceCount} products");
            preview.AppendLine("  (box price / count, rounded up to a payable amount - review these)");
        }

        AppendIssues(preview, "Errors", plan.Issues);
        AppendUnknownHeaders(preview, plan.UnknownHeaders);

        if (!plan.HasWork)
        {
            preview.AppendLine();
            preview.AppendLine("There is nothing to import.");
            AppDialog.Show("Import Products", preview.ToString(), monospace: true);
            return;
        }

        preview.AppendLine();
        preview.AppendLine("Existing products keep any value the file leaves empty.");
        preview.AppendLine("Rows listed above are skipped. Continue?");

        if (!AppDialog.Confirm("Import Products", preview.ToString(), "Import", "Cancel"))
        {
            Message = "Import cancelled.";
            return;
        }

        var result = await _initialImportService.ApplyProductsAsync(
            plan, file.Hash, Path.GetFileName(ImportFilePath), _facilityId, _userId);

        ShowApplyResult("Import Products", result, "products");
    }

    private async Task ExecuteImportInventoryAsync()
    {
        Message = string.Empty;

        var file = await TryReadImportFileAsync(ImportType.Inventory);

        if (file is null)
        {
            return;
        }

        var plan = await _initialImportService.PlanInventoryAsync(file.Rows);

        if (plan.HasFileError)
        {
            AppDialog.Show("Import Inventory", plan.FileError!);
            return;
        }

        var preview = new StringBuilder();
        preview.AppendLine("STEP 2 — INVENTORY");
        preview.AppendLine("--------------------------------");
        preview.AppendLine($"Rows in file          : {plan.TotalRows}");
        preview.AppendLine($"Batches to add        : {plan.BatchCount}");
        preview.AppendLine($"Product not found     : {plan.UnmatchedRowCount}");
        preview.AppendLine($"Without expiry date   : {plan.NoExpiryCount}");
        preview.AppendLine($"Rows with errors      : {plan.ErrorRowCount}");
        AppendIssues(preview, "Product not found", plan.UnmatchedRows);
        AppendIssues(preview, "Errors", plan.Issues);
        AppendUnknownHeaders(preview, plan.UnknownHeaders);

        if (!plan.HasWork)
        {
            preview.AppendLine();
            preview.AppendLine("There is nothing to import.");
            AppDialog.Show("Import Inventory", preview.ToString(), monospace: true);
            return;
        }

        preview.AppendLine();
        preview.AppendLine("quantity is the number of boxes, as on the Stock-IN screen. Use loose_quantity for loose units.");
        preview.AppendLine("Rows listed above are skipped. Continue?");

        if (!AppDialog.Confirm("Import Inventory", preview.ToString(), "Import", "Cancel"))
        {
            Message = "Import cancelled.";
            return;
        }

        var result = await _initialImportService.ApplyInventoryAsync(
            plan, file.Hash, Path.GetFileName(ImportFilePath), _facilityId, _userId);

        ShowApplyResult("Import Inventory", result, "batches");
    }

    /// <summary>읽어 들인 파일과 그 내용의 해시.</summary>
    private sealed record ImportFile(IReadOnlyList<ImportSourceRow> Rows, string Hash);

    /// <summary>
    /// 파일을 읽고 같은 파일을 다시 넣는 것인지까지 확인한다.
    /// 진행할 수 없으면 그 자리에서 알리고 null을 돌려준다.
    /// </summary>
    private async Task<ImportFile?> TryReadImportFileAsync(ImportType importType)
    {
        if (string.IsNullOrWhiteSpace(ImportFilePath))
        {
            Message = "Please select a file to import.";
            return null;
        }

        if (!File.Exists(ImportFilePath))
        {
            Message = "File not found.";
            return null;
        }

        string fileHash;
        IReadOnlyList<ImportSourceRow> rows;

        try
        {
            fileHash = ImportFileReader.ComputeHash(ImportFilePath);
        }
        catch (Exception ex)
        {
            Message = $"Import error: {ex.Message}";
            return null;
        }

        // 같은 파일을 두 번 넣으면 재고가 두 배가 된다. 되돌리기가 매우 번거로운 사고라
        // 진행 선택지를 주지 않고 여기서 끊는다.
        if (await _initialImportService.WasAlreadyImportedAsync(importType, fileHash))
        {
            AppDialog.Show("Import Blocked", "This file has already been imported.");
            Message = "This file has already been imported.";
            return null;
        }

        try
        {
            rows = ImportFileReader.Read(ImportFilePath);
        }
        catch (Exception ex)
        {
            // 라이브러리 예외 문구를 그대로 보여 주면 약국은 무엇을 해야 할지 알 수 없다.
            // Excel이 열리지 않는 경우에는 당장 할 수 있는 일을 알려 준다.
            Message = IsExcel(ImportFilePath)
                ? "This Excel file could not be read. Open it in Excel and save a copy as CSV, "
                  + "then import that file."
                : $"Import error: {ex.Message}";
            return null;
        }

        return new ImportFile(rows, fileHash);
    }

    private static void AppendIssues(StringBuilder builder, string title, IReadOnlyList<ImportIssue> issues)
    {
        if (issues.Count == 0)
        {
            return;
        }

        builder.AppendLine();
        builder.AppendLine($"{title}:");

        foreach (var issue in issues.Take(MaxIssueLinesInDialog))
        {
            builder.AppendLine($"  {issue}");
        }

        if (issues.Count > MaxIssueLinesInDialog)
        {
            builder.AppendLine($"  … and {issues.Count - MaxIssueLinesInDialog} more");
        }
    }

    /// <summary>고른 파일이 Excel인지. 읽기 실패 안내를 형식에 맞게 내기 위한 것이다.</summary>
    private static bool IsExcel(string filePath) =>
        Path.GetExtension(filePath).Equals(".xlsx", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// 못 알아본 머리글을 미리보기에 적는다. 그 칸의 값은 통째로 버려지는데,
    /// 알려 주지 않으면 찾을 방법이 없다 — manufacturer를 "maker"로 적어 두면
    /// 제조사가 전부 빈 값이 되고, 이름이 같은 상품이 한 상품으로 합쳐진다.
    /// 그러고도 화면에는 오류가 없고, 합쳐진 뒤에는 되돌릴 수 없다.
    /// </summary>
    private static void AppendUnknownHeaders(StringBuilder builder, IReadOnlyList<string> headers)
    {
        if (headers.Count == 0)
        {
            return;
        }

        builder.AppendLine();
        builder.AppendLine("Columns this import does not know (their values are ignored):");

        foreach (var header in headers)
        {
            builder.AppendLine($"  {header}");
        }

        builder.AppendLine("Check the spelling against the column list on the screen.");
    }

    private void ShowApplyResult(string title, ImportApplyResult result, string unitLabel)
    {
        var summary = new StringBuilder();
        summary.AppendLine($"Imported : {result.SuccessCount} {unitLabel}");
        summary.AppendLine($"Failed   : {result.FailureCount}");
        AppendIssues(summary, "Failures", result.Failures);

        if (result.HistoryWarning is not null)
        {
            summary.AppendLine();
            summary.AppendLine(result.HistoryWarning);
        }

        AppDialog.Show(title, summary.ToString(), monospace: true);

        Message = result.FailureCount == 0
            ? $"Import complete — {result.SuccessCount} {unitLabel} added."
            : $"Import complete — Success: {result.SuccessCount}, Failed: {result.FailureCount}.";
    }

    // ── 내보내기: 앱 → 파일 ─────────────────────────────────────────────────

    private void ExecuteSelectExportFolder()
    {
        var dialog = new OpenFolderDialog { Title = "Select Export Folder" };

        if (dialog.ShowDialog() == true)
        {
            ExportFolder = dialog.FolderName;
        }
    }

    private async Task ExecuteExportDataAsync()
    {
        Message = string.Empty;

        var datasets = new List<ExportDataset>();

        if (ExportProducts) datasets.Add(ExportDataset.Products);
        if (ExportInventory) datasets.Add(ExportDataset.Inventory);
        if (ExportSalesHistory) datasets.Add(ExportDataset.SalesHistory);

        // 기간을 비워 둔 채 판매 내역을 뽑으면 개업 이후 전부가 나간다. 몇 해 쓰고 나면
        // 그건 실수로 만들 파일의 크기가 아니라서, 한 번 묻고 넘어간다.
        // 기간이 걸리지 않는 묶음만 골랐다면 물을 것도 없다.
        var exportsEveryPeriod =
            datasets.Any(ExportDatasets.SupportsDateRange)
            && ExportDateFrom is null
            && ExportDateTo is null;

        if (exportsEveryPeriod
            && !AppDialog.Confirm(
                "Export",
                "No period is set, so the sales history file will cover every sale since the pharmacy opened."
                + "\n\nExport the whole period?",
                confirmText: "Export all",
                cancelText: "Cancel"))
        {
            Message = "Export cancelled.";
            return;
        }

        var result = await _backupService.ExportDatasetsAsync(
            ExportFolder, datasets, IsCsvFormat, ExportDateFrom, ExportDateTo);

        Message = result.IsSuccess ? result.Message ?? "Export completed successfully." : result.Message!;
    }

    private async Task ExecuteCreateDbBackupAsync()
    {
        Message = string.Empty;

        var result = await _backupService.CreateDatabaseBackupAsync(ExportFolder);

        Message = result.IsSuccess ? result.Message ?? "Backup created successfully." : result.Message!;
    }

    // ── 복원: 현재 데이터를 통째로 교체 ─────────────────────────────────────

    private void ExecuteSelectBackupFile()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select Backup File",
            Filter = "SQLite Database (*.db)|*.db|All files (*.*)|*.*"
        };

        if (dialog.ShowDialog() == true)
        {
            BackupFilePath = dialog.FileName;
        }
    }

    private async Task ExecuteRestoreDbAsync()
    {
        Message = string.Empty;

        if (string.IsNullOrWhiteSpace(BackupFilePath))
        {
            Message = "Please select a backup file.";
            return;
        }

        if (!AppDialog.Confirm("Confirm Restore", "Current data will be replaced. Continue?"))
        {
            Message = "Please confirm database restore.";
            return;
        }

        // 복원 전 자동 백업이 들어갈 폴더. 내보내기 폴더를 고르지 않았으면 앱 데이터 폴더에 남긴다.
        var autoBackupFolder = string.IsNullOrWhiteSpace(ExportFolder)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PharmaPOS")
            : ExportFolder;

        var result = await _backupService.RestoreDatabaseAsync(BackupFilePath, autoBackupFolder);

        if (!result.IsSuccess)
        {
            Message = result.Message!;
            return;
        }

        AppDialog.Show("Restart Required", "Database restored successfully. The application will now restart.");

        RestartApplication();
    }

    private static void RestartApplication()
    {
        var exePath = Environment.ProcessPath;

        if (!string.IsNullOrEmpty(exePath))
        {
            Process.Start(exePath);
        }

        System.Windows.Application.Current.Shutdown();
    }

    // ── 사진 가져오기 ───────────────────────────────────────────────────────
    //
    // 상품·재고와 달리 파일 하나가 아니라 폴더를 받는다. CSV 셀에는 이미지를 담을 수 없고
    // (200KB 사진이 270KB 텍스트가 되는데 엑셀 셀 한도가 32,767자다), 파일명을 바코드로
    // 두면 시트를 손대지 않아도 된다.
    //
    // 같은 폴더를 다시 넣는 것도 막지 않는다. 그 차단은 재고가 두 배가 되는 것을 막으려는
    // 장치인데 사진은 덮어쓰기라 쌓이지 않고, 다시 찍어 넣는 것이 정상적인 사용이다.

    private async Task ExecuteImportPhotosAsync()
    {
        Message = string.Empty;

        var dialog = new OpenFolderDialog { Title = "Select the folder that holds the product photos" };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        IImportPhotoSource source;

        try
        {
            source = new FolderPhotoSource(dialog.FolderName);
        }
        catch (Exception ex)
        {
            Message = $"Import error: {ex.Message}";
            return;
        }

        var plan = await _photoImportService.PlanAsync(source);

        var preview = new StringBuilder();
        preview.AppendLine("STEP 3 - PHOTOS");
        preview.AppendLine("--------------------------------");
        preview.AppendLine($"Files in folder       : {plan.TotalFiles}");
        preview.AppendLine($"New photos            : {plan.NewCount}");
        preview.AppendLine($"Photos to replace     : {plan.ReplaceCount}");
        preview.AppendLine($"No matching product   : {plan.UnmatchedFiles.Count}");
        preview.AppendLine($"Cannot be read        : {plan.SkippedFiles.Count}");

        AppendNames(preview, "No matching product", plan.UnmatchedFiles);
        AppendIssues(preview, "Cannot be read", plan.SkippedFiles);
        AppendIssues(preview, "Errors", plan.Issues);

        if (!plan.HasWork)
        {
            preview.AppendLine();
            preview.AppendLine("There is nothing to import.");
            preview.AppendLine("Name each photo after the product's barcode, for example 8801234567890.jpg.");
            AppDialog.Show("Import Photos", preview.ToString(), monospace: true);
            return;
        }

        preview.AppendLine();

        if (plan.ReplaceCount > 0)
        {
            preview.AppendLine($"{plan.ReplaceCount} product(s) already have a photo. It will be replaced.");
        }

        preview.AppendLine("Files listed above are skipped. Continue?");

        if (!AppDialog.Confirm("Import Photos", preview.ToString(), "Import", "Cancel"))
        {
            Message = "Import cancelled.";
            return;
        }

        var result = await _photoImportService.ApplyAsync(plan, source);

        ShowApplyResult("Import Photos", result, "photos");
    }

    /// <summary>파일 이름만 늘어놓는 목록. 오류 줄 번호가 뜻이 없는 경우에 쓴다.</summary>
    private static void AppendNames(StringBuilder builder, string title, IReadOnlyList<string> names)
    {
        if (names.Count == 0)
        {
            return;
        }

        builder.AppendLine();
        builder.AppendLine($"{title}:");

        foreach (var name in names.Take(MaxIssueLinesInDialog))
        {
            builder.AppendLine($"  {name}");
        }

        if (names.Count > MaxIssueLinesInDialog)
        {
            builder.AppendLine($"  ... and {names.Count - MaxIssueLinesInDialog} more");
        }
    }
}
