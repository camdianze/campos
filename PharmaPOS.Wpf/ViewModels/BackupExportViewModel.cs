using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using Microsoft.Win32;
using PharmaPOS.Application.Import;
using PharmaPOS.Application.Inventory;
using PharmaPOS.Domain.Enums;
using Lightweight_Digital_Inventory_Management___POS_System.Services;
using Lightweight_Digital_Inventory_Management___POS_System.ViewModels.Base;
using Lightweight_Digital_Inventory_Management___POS_System.Views;

namespace Lightweight_Digital_Inventory_Management___POS_System.ViewModels;

/// <summary>
/// Import / Export 화면의 ViewModel.
///
/// 화면은 왼쪽이 "파일 → 앱"(가져오기), 오른쪽이 "앱 → 파일"(내보내기·백업)이고,
/// 현재 데이터를 통째로 갈아엎는 복원만 아래 위험 구역에 따로 둔다.
/// 이 셋은 결과의 무게가 서로 달라 한 덩어리로 섞으면 잘못 누르기 쉽다.
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

    public BackupExportViewModel(
        IBackupService backupService,
        IInitialImportService initialImportService,
        IPhotoImportService photoImportService,
        string facilityId,
        string userId)
    {
        _backupService = backupService;
        _initialImportService = initialImportService;
        _photoImportService = photoImportService;
        _facilityId = facilityId;
        _userId = userId;

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

        var plan = await _initialImportService.PlanProductsAsync(file.Rows, ImportPriceCurrency);

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

        Application.Current.Shutdown();
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
