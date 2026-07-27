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

            List<Product> products = ext switch
            {
                ".csv" => ParseCsv(ImportFilePath),
                ".xlsx" => ParseExcel(ImportFilePath),
                _ => throw new NotSupportedException("Only .csv and .xlsx are supported.")
            };

            if (products.Count == 0)
            {
                Message = "No valid products found in file.";
                return;
            }

            int success = 0, failed = 0;

            foreach (var product in products)
            {
                var result = await _productService.SaveProductAsync(product, isNewProduct: true);

                if (result.IsSuccess) success++;
                else failed++;
            }

            Message = $"✅ Import complete — Success: {success}, Failed: {failed} (Total: {products.Count})";
        }
        catch (Exception ex)
        {
            Message = $"Import error: {ex.Message}";
        }
    }

    private static List<Product> ParseCsv(string filePath)
    {
        var products = new List<Product>();
        var lines = File.ReadAllLines(filePath);
        if (lines.Length < 2) return products;

        var headers = lines[0].Split(',')
                               .Select(h => h.Trim().ToLowerInvariant())
                               .ToArray();

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

            products.Add(new Product
            {
                ProductId = Guid.NewGuid().ToString(),
                ProductName = Get("productname"),
                GenericName = Get("genericname"),
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

        return products;
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

    private static List<Product> ParseExcel(string filePath)
    {
        var products = new List<Product>();

        using var workbook = new XLWorkbook(filePath);
        var ws = workbook.Worksheet(1);
        var rows = ws.RangeUsed()?.RowsUsed().ToList();
        if (rows == null || rows.Count < 2) return products;

        var headers = rows[0].Cells()
                             .Select(c => c.GetString().Trim().ToLowerInvariant())
                             .ToArray();

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

            products.Add(new Product
            {
                ProductId = Guid.NewGuid().ToString(),
                ProductName = Get("productname"),
                GenericName = Get("genericname"),
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

        return products;
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