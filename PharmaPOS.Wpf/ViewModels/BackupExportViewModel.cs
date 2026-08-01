using System.Diagnostics;
using System.IO;
using System.Windows;
using ClosedXML.Excel;
using Microsoft.Win32;
using PharmaPOS.Application.Inventory;
using PharmaPOS.Application.Products;
using PharmaPOS.Domain.Entities;
using PharmaPOS.Domain.Enums;
using Lightweight_Digital_Inventory_Management___POS_System.ViewModels.Base;

namespace Lightweight_Digital_Inventory_Management___POS_System.ViewModels;

public class BackupExportViewModel : ViewModelBase
{
    private readonly IBackupService _backupService;
    private readonly IProductService _productService;

    private string _backupLocation = string.Empty;
    private string? _selectedExportType;
    private bool _isCsvFormat = true;
    private string _backupFilePath = string.Empty;
    private string _importFilePath = string.Empty;
    private string _message = string.Empty;

    public string BackupLocation
    {
        get => _backupLocation;
        set => SetProperty(ref _backupLocation, value);
    }

    public IReadOnlyList<string> AvailableExportTypes { get; }

    public string? SelectedExportType
    {
        get => _selectedExportType;
        set => SetProperty(ref _selectedExportType, value);
    }

    public bool IsCsvFormat
    {
        get => _isCsvFormat;
        set => SetProperty(ref _isCsvFormat, value);
    }

    public string BackupFilePath
    {
        get => _backupFilePath;
        set => SetProperty(ref _backupFilePath, value);
    }

    public string ImportFilePath
    {
        get => _importFilePath;
        set => SetProperty(ref _importFilePath, value);
    }

    public string Message
    {
        get => _message;
        set => SetProperty(ref _message, value);
    }

    public RelayCommand SelectBackupLocationCommand { get; }
    public RelayCommand CreateDbBackupCommand { get; }
    public RelayCommand ExportDataCommand { get; }
    public RelayCommand SelectBackupFileCommand { get; }
    public RelayCommand RestoreDbCommand { get; }
    public RelayCommand SelectImportFileCommand { get; }
    public RelayCommand ImportProductsCommand { get; }

  

    public BackupExportViewModel(IBackupService backupService, IProductService productService)
    {
        _backupService = backupService;
        _productService = productService;

        var exportTypes = new List<string> { "All" };
        exportTypes.AddRange(_backupService.GetExportableTableNames());
        AvailableExportTypes = exportTypes;

        SelectBackupLocationCommand = new RelayCommand(_ => ExecuteSelectBackupLocation());
        CreateDbBackupCommand = new RelayCommand(async _ => await ExecuteCreateDbBackupAsync());
        ExportDataCommand = new RelayCommand(async _ => await ExecuteExportDataAsync());
        SelectBackupFileCommand = new RelayCommand(_ => ExecuteSelectBackupFile());
        RestoreDbCommand = new RelayCommand(async _ => await ExecuteRestoreDbAsync());
        SelectImportFileCommand = new RelayCommand(_ => ExecuteSelectImportFile());
        ImportProductsCommand = new RelayCommand(async _ => await ExecuteImportProductsAsync());
    }

    // ── Import ──────────────────────────────────────────────────────────────

    private void ExecuteSelectImportFile()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select Product Import File",
            Filter = "CSV/Excel (*.csv;*.xlsx)|*.csv;*.xlsx|CSV (*.csv)|*.csv|Excel (*.xlsx)|*.xlsx"
        };

        if (dialog.ShowDialog() == true)
            ImportFilePath = dialog.FileName;
    }

    private async Task ExecuteImportProductsAsync()
    {
        Message = string.Empty;

        if (string.IsNullOrWhiteSpace(ImportFilePath))
        {
            Message = "Please select a file to import.";
            return;
        }

        if (!File.Exists(ImportFilePath))
        {
            Message = "File not found.";
            return;
        }

        try
        {
            var ext = Path.GetExtension(ImportFilePath).ToLowerInvariant();

            var parsed = ext switch
            {
                ".csv" => ParseCsv(ImportFilePath),
                ".xlsx" => ParseExcel(ImportFilePath),
                _ => throw new NotSupportedException("Only .csv and .xlsx are supported.")
            };

            // 헤더가 어긋나 파일 전체를 못 읽은 경우. 어느 컬럼이 없는지 그대로 알려준다.
            if (parsed.Error is not null)
            {
                Message = parsed.Error;
                return;
            }

            var products = parsed.Products;

            if (products.Count == 0)
            {
                Message = "No rows with a product name were found in the file.";
                return;
            }

            int success = 0, failed = 0;
            string? firstFailure = null;

            foreach (var product in products)
            {
                var result = await _productService.SaveProductAsync(product, isNewProduct: true);

                if (result.IsSuccess)
                {
                    success++;
                }
                else
                {
                    failed++;
                    // 실패 사유를 버리면 몇 건 실패했다는 숫자만 남아 원인을 알 수 없다.
                    firstFailure ??= result.Message;
                }
            }

            Message = failed == 0
                ? $"✅ Import complete — {success} products added."
                : $"Import complete — Success: {success}, Failed: {failed} (Total: {products.Count}). First error: {firstFailure}";
        }
        catch (Exception ex)
        {
            Message = $"Import error: {ex.Message}";
        }
    }

    /// <summary>
    /// 가져오기에 반드시 있어야 하는 컬럼. 없으면 어차피 상품 저장 단계에서 전부 실패한다.
    /// (ProductService의 필수값 검증과 같은 목록이다.)
    /// </summary>
    private static readonly string[] RequiredColumns =
    {
        "productname", "unit", "costprice", "sellingprice"
    };

    /// <summary>
    /// 헤더 이름에서 대소문자와 구분자를 없앤다.
    /// "ProductName" / "product_name" / "Product Name"은 같은 컬럼을 가리키는데,
    /// 예전에는 문자열이 정확히 일치할 때만 찾아서 snake_case 파일이 통째로
    /// 무시됐다 (한 행도 안 읽히고 "No valid products found"만 떴다).
    /// 앞에 붙는 BOM도 여기서 같이 털어낸다.
    /// </summary>
    private static string NormalizeHeader(string header)
        => new(header.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

    /// <summary>
    /// 필수 컬럼이 빠졌을 때, 파일에서 실제로 읽힌 헤더까지 함께 알려준다.
    /// 이름이 어긋난 경우 이 목록을 보면 바로 원인을 알 수 있다.
    /// </summary>
    private static string? DescribeMissingColumns(IReadOnlyList<string> normalizedHeaders)
    {
        var missing = RequiredColumns.Where(c => !normalizedHeaders.Contains(c)).ToList();

        if (missing.Count == 0)
        {
            return null;
        }

        return $"The file is missing required columns: {string.Join(", ", missing)}. "
             + $"Columns found: {string.Join(", ", normalizedHeaders.Where(h => h.Length > 0))}.";
    }

    private static ProductImportResult ParseCsv(string filePath)
    {
        var result = new ProductImportResult();
        var products = result.Products;

        var lines = File.ReadAllLines(filePath);
        if (lines.Length < 2) return result;

        var headers = lines[0].Split(',')
                               .Select(NormalizeHeader)
                               .ToArray();

        result.Error = DescribeMissingColumns(headers);
        if (result.Error is not null) return result;

        int Idx(string name) => Array.IndexOf(headers, name);

        for (int i = 1; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (string.IsNullOrWhiteSpace(line)) continue;

            var cols = SplitCsvLine(line);

            string Get(string field)
            {
                int idx = Idx(field);
                return idx >= 0 && idx < cols.Length ? cols[idx].Trim() : string.Empty;
            }

            if (string.IsNullOrWhiteSpace(Get("productname"))) continue;

            decimal.TryParse(Get("costprice"), out var cost);
            decimal.TryParse(Get("sellingprice"), out var sell);
            int.TryParse(Get("safetystocklevel"), out var safety);

            var status = Get("status").ToLowerInvariant() == "inactive"
                         ? EntityStatus.Inactive : EntityStatus.Active;

            // atccode / iscombination은 항생제 복약안내(AMR)용 컬럼이다.
            // 상품을 수백 건 한 번에 등록하는 경로가 여기뿐이라, 여기서 못 넣으면
            // 상품마다 손으로 채워야 해서 기능이 사실상 안 쓰이게 된다.
            var atcCode = Get("atccode");
            var isCombination = Get("iscombination").ToLowerInvariant() is "true" or "1" or "y" or "yes";

            products.Add(new Product
            {
                ProductId = Guid.NewGuid().ToString(),
                ProductName = Get("productname"),
                GenericName = Get("genericname"),
                AtcCode = string.IsNullOrWhiteSpace(atcCode) ? null : atcCode,
                IsCombination = isCombination,
                Barcode = string.IsNullOrWhiteSpace(Get("barcode")) ? null : Get("barcode"),
                Strength = Get("strength"),
                Unit = Get("unit"),
                Manufacturer = Get("manufacturer"),
                CountryOfOrigin = Get("countryoforigin"),
                CostPrice = cost,
                SellingPrice = sell,
                SafetyStockLevel = safety,
                Status = status,
                CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()

            });
        }

        return result;
    }

    /// <summary>파일에서 읽어낸 상품과, 파일 자체가 잘못됐을 때의 사유.</summary>
    private sealed class ProductImportResult
    {
        public List<Product> Products { get; } = new();

        public string? Error { get; set; }
    }

    private static string[] SplitCsvLine(string line)
    {
        var result = new List<string>();
        bool inQuotes = false;
        var current = new System.Text.StringBuilder();

        foreach (char c in line)
        {
            if (c == '"') { inQuotes = !inQuotes; }
            else if (c == ',' && !inQuotes) { result.Add(current.ToString()); current.Clear(); }
            else { current.Append(c); }
        }
        result.Add(current.ToString());
        return result.ToArray();
    }

    private static ProductImportResult ParseExcel(string filePath)
    {
        var result = new ProductImportResult();
        var products = result.Products;

        using var workbook = new XLWorkbook(filePath);
        var ws = workbook.Worksheet(1);
        var rows = ws.RangeUsed()?.RowsUsed().ToList();
        if (rows == null || rows.Count < 2) return result;

        var headers = rows[0].Cells()
                             .Select(c => NormalizeHeader(c.GetString()))
                             .ToArray();

        result.Error = DescribeMissingColumns(headers);
        if (result.Error is not null) return result;

        int Idx(string name) => Array.IndexOf(headers, name);

        for (int i = 1; i < rows.Count; i++)
        {
            var row = rows[i];

            string Get(string field)
            {
                int idx = Idx(field);
                return idx >= 0 ? row.Cell(idx + 1).GetString().Trim() : string.Empty;
            }

            if (string.IsNullOrWhiteSpace(Get("productname"))) continue;

            decimal.TryParse(Get("costprice"), out var cost);
            decimal.TryParse(Get("sellingprice"), out var sell);
            int.TryParse(Get("safetystocklevel"), out var safety);

            var status = Get("status").ToLowerInvariant() == "inactive"
                         ? EntityStatus.Inactive : EntityStatus.Active;

            // atccode / iscombination은 항생제 복약안내(AMR)용 컬럼이다.
            // 상품을 수백 건 한 번에 등록하는 경로가 여기뿐이라, 여기서 못 넣으면
            // 상품마다 손으로 채워야 해서 기능이 사실상 안 쓰이게 된다.
            var atcCode = Get("atccode");
            var isCombination = Get("iscombination").ToLowerInvariant() is "true" or "1" or "y" or "yes";

            products.Add(new Product
            {
                ProductId = Guid.NewGuid().ToString(),
                ProductName = Get("productname"),
                GenericName = Get("genericname"),
                AtcCode = string.IsNullOrWhiteSpace(atcCode) ? null : atcCode,
                IsCombination = isCombination,
                Barcode = string.IsNullOrWhiteSpace(Get("barcode")) ? null : Get("barcode"),
                Strength = Get("strength"),
                Unit = Get("unit"),
                Manufacturer = Get("manufacturer"),
                CountryOfOrigin = Get("countryoforigin"),
                CostPrice = cost,
                SellingPrice = sell,
                SafetyStockLevel = safety,
                Status = status,
                CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            });
        }

        return result;
    }

    // ── 기존 기능 ────────────────────────────────────────────────────────────

    private void ExecuteSelectBackupLocation()
    {
        var dialog = new OpenFolderDialog { Title = "Select Backup Location" };
        if (dialog.ShowDialog() == true) BackupLocation = dialog.FolderName;
    }

    private async Task ExecuteCreateDbBackupAsync()
    {
        Message = string.Empty;
        var result = await _backupService.CreateDatabaseBackupAsync(BackupLocation);
        Message = result.IsSuccess ? result.Message ?? "Backup created successfully." : result.Message!;
    }

    private async Task ExecuteExportDataAsync()
    {
        Message = string.Empty;
        var result = await _backupService.ExportDataAsync(BackupLocation, SelectedExportType, IsCsvFormat);
        Message = result.IsSuccess ? result.Message ?? "Export completed successfully." : result.Message!;
    }

    private void ExecuteSelectBackupFile()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select Backup File",
            Filter = "SQLite Database (*.db)|*.db|All files (*.*)|*.*"
        };
        if (dialog.ShowDialog() == true) BackupFilePath = dialog.FileName;
    }

    private async Task ExecuteRestoreDbAsync()
    {
        Message = string.Empty;

        if (string.IsNullOrWhiteSpace(BackupFilePath))
        {
            Message = "Please select a backup file.";
            return;
        }

        var confirm = MessageBox.Show(
            "Current data will be replaced. Continue?",
            "Confirm Restore", MessageBoxButton.YesNo, MessageBoxImage.Warning);

        if (confirm != MessageBoxResult.Yes)
        {
            Message = "Please confirm database restore.";
            return;
        }

        var autoBackupFolder = string.IsNullOrWhiteSpace(BackupLocation)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PharmaPOS")
            : BackupLocation;

        var result = await _backupService.RestoreDatabaseAsync(BackupFilePath, autoBackupFolder);

        if (!result.IsSuccess) { Message = result.Message!; return; }

        MessageBox.Show(
            "Database restored successfully. The application will now restart.",
            "Restart Required", MessageBoxButton.OK, MessageBoxImage.Information);

        RestartApplication();
    }

    private static void RestartApplication()
    {
        var exePath = Environment.ProcessPath;
        if (!string.IsNullOrEmpty(exePath)) Process.Start(exePath);
        Application.Current.Shutdown();
    }
}