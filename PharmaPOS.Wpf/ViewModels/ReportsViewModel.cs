using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Text;
using Microsoft.Win32;
using PharmaPOS.Application.Reports;
using Lightweight_Digital_Inventory_Management___POS_System.ViewModels.Base;

namespace Lightweight_Digital_Inventory_Management___POS_System.ViewModels;

/// <summary>상품 순위의 정렬 기준.</summary>
public enum ProductSortOption
{
    Amount,
    Quantity
}

/// <summary>
/// 관리자 리포트 화면의 ViewModel.
/// 기간을 고르면 매출 요약 · 상품별 순위 · 항생제 성분별 판매를 함께 보여주고,
/// 각 값에 직전 기간 대비 증감을 붙인다.
/// </summary>
public class ReportsViewModel : ViewModelBase
{
    private readonly IReportService _reportService;
    private readonly string _facilityId;

    private DateTime? _dateFrom;
    private DateTime? _dateTo;
    private ReportData? _report;
    private string _message = string.Empty;
    private bool _isLoading;
    private ProductSortOption _selectedSort = ProductSortOption.Amount;

    /// <summary>
    /// 조회해 온 원본. 정렬을 바꿀 때 DB를 다시 치지 않고 이걸 다시 늘어놓는다.
    /// </summary>
    private IReadOnlyList<ProductSalesRow> _loadedProducts = Array.Empty<ProductSalesRow>();

    public ObservableCollection<ProductSalesRow> Products { get; } = new();
    public ObservableCollection<AntibioticSalesRow> Antibiotics { get; } = new();

    public ProductSortOption SelectedSort
    {
        get => _selectedSort;
        set
        {
            if (SetProperty(ref _selectedSort, value))
            {
                ApplySorting();
            }
        }
    }

    public IReadOnlyList<ProductSortOption> AvailableSorts { get; } = Enum.GetValues<ProductSortOption>();

    public DateTime? DateFrom
    {
        get => _dateFrom;
        set => SetProperty(ref _dateFrom, value);
    }

    public DateTime? DateTo
    {
        get => _dateTo;
        set => SetProperty(ref _dateTo, value);
    }

    /// <summary>집계 결과. 요약 카드들이 여기서 값을 꺼내 쓴다.</summary>
    public ReportData? Report
    {
        get => _report;
        private set
        {
            if (SetProperty(ref _report, value))
            {
                OnPropertyChanged(nameof(PeriodLabel));
                OnPropertyChanged(nameof(ComparisonLabel));
                OnPropertyChanged(nameof(AccessShareDisplay));
                OnPropertyChanged(nameof(CounsellingSummary));
                OnPropertyChanged(nameof(HasAntibioticData));
            }
        }
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
    }

    public string Message
    {
        get => _message;
        set => SetProperty(ref _message, value);
    }

    public string PeriodLabel => Report is null ? string.Empty : Report.Range.Label;

    /// <summary>무엇과 비교한 값인지 화면에 분명히 적어 둔다. 이게 없으면 증감을 신뢰할 수 없다.</summary>
    public string ComparisonLabel
    {
        get
        {
            if (Report is null)
            {
                return string.Empty;
            }

            var what = Report.Range.IsWholeCalendarMonth ? "previous month" : "previous period";
            return $"compared with the {what}: {Report.Range.PreviousLabel}";
        }
    }

    /// <summary>WHO는 항생제 소비의 70% 이상을 ACCESS로 권고한다. 그 기준으로 읽는 값이다.</summary>
    public string AccessShareDisplay =>
        Report?.AccessSharePercent is { } share
            ? share.ToString("0.#", CultureInfo.InvariantCulture) + "%"
            : "—";

    public string CounsellingSummary =>
        Report is null
            ? string.Empty
            : $"{Report.CounsellingPrintedCount} printed / {Report.AntibioticSaleCount} antibiotic sales";

    /// <summary>항생제 표가 비어 있을 때 안내 문구를 대신 보여주기 위한 값.</summary>
    public bool HasAntibioticData => Antibiotics.Count > 0;

    public RelayCommand RefreshCommand { get; }
    public RelayCommand ThisMonthCommand { get; }
    public RelayCommand LastMonthCommand { get; }
    public RelayCommand ExportCommand { get; }
    public RelayCommand BackCommand { get; }

    public event Action? NavigateBack;

    public ReportsViewModel(IReportService reportService, string facilityId)
    {
        _reportService = reportService;
        _facilityId = facilityId;

        RefreshCommand = new RelayCommand(async _ => await LoadAsync());
        ThisMonthCommand = new RelayCommand(async _ => await SelectMonthAsync(0));
        LastMonthCommand = new RelayCommand(async _ => await SelectMonthAsync(-1));
        ExportCommand = new RelayCommand(_ => ExecuteExport());
        BackCommand = new RelayCommand(_ => NavigateBack?.Invoke());

        // 처음 열면 이번 달 1일부터 오늘까지.
        var today = DateTime.Today;
        _dateFrom = new DateTime(today.Year, today.Month, 1);
        _dateTo = today;

        _ = LoadAsync();
    }

    /// <summary>달 단위 프리셋. 달 전체를 고르면 비교 대상이 자동으로 전월이 된다.</summary>
    private async Task SelectMonthAsync(int monthOffset)
    {
        var target = DateTime.Today.AddMonths(monthOffset);
        var start = new DateTime(target.Year, target.Month, 1);

        DateFrom = start;

        // 이번 달은 아직 안 끝났으므로 오늘까지만 본다. 지난달은 말일까지 전부.
        DateTo = monthOffset == 0
            ? DateTime.Today
            : start.AddMonths(1).AddDays(-1);

        await LoadAsync();
    }

    public async Task LoadAsync()
    {
        IsLoading = true;
        Message = string.Empty;

        var result = await _reportService.GetReportAsync(_facilityId, DateFrom, DateTo);

        IsLoading = false;

        if (!result.IsSuccess)
        {
            Message = result.Message!;
            return;
        }

        var data = result.Data!;

        _loadedProducts = data.Products;
        ApplySorting();

        Antibiotics.Clear();
        foreach (var row in data.Antibiotics)
        {
            Antibiotics.Add(row);
        }

        Report = data;
        OnPropertyChanged(nameof(HasAntibioticData));

        // 기간을 고를 때 화면이 비어 보이는 이유를 분명히 알려 준다.
        Message = data.Current.TransactionCount == 0
            ? $"No sales in {data.Range.Label}."
            : string.Empty;
    }

    /// <summary>
    /// 고른 정렬 기준으로 늘어놓고 순위를 다시 매긴다.
    ///
    /// 순위를 서비스가 아니라 여기서 매기는 이유: 정렬 기준이 화면에서 바뀌기 때문이다.
    /// 판매수로 정렬해 놓고 순위만 매출 기준으로 남아 있으면 1위가 맨 위에 없다.
    /// </summary>
    private void ApplySorting()
    {
        var ordered = _selectedSort == ProductSortOption.Quantity
            ? _loadedProducts.OrderByDescending(r => r.Quantity).ThenByDescending(r => r.Amount)
            : _loadedProducts.OrderByDescending(r => r.Amount).ThenByDescending(r => r.Quantity);

        Products.Clear();

        var rank = 1;
        foreach (var row in ordered)
        {
            row.Rank = rank++;
            Products.Add(row);
        }
    }

    private void ExecuteExport()
    {
        if (Report is null || (Products.Count == 0 && Antibiotics.Count == 0))
        {
            Message = "No data to export.";
            return;
        }

        var dialog = new SaveFileDialog
        {
            Filter = "CSV files (*.csv)|*.csv",
            FileName = $"report_{Report.Range.From:yyyyMMdd}_{Report.Range.To:yyyyMMdd}.csv"
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            var builder = new StringBuilder();

            builder.AppendLine($"Report period,{Report.Range.Label}");
            builder.AppendLine($"Compared with,{Report.Range.PreviousLabel}");
            builder.AppendLine();

            builder.AppendLine("Summary,Current,Previous,Change");
            builder.AppendLine($"Sales amount,{Report.Current.Amount},{Report.Previous.Amount},{Report.AmountChange}");
            builder.AppendLine($"Transactions,{Report.Current.TransactionCount},{Report.Previous.TransactionCount},{Report.TransactionChange}");
            builder.AppendLine($"Units sold,{Report.Current.ItemCount},{Report.Previous.ItemCount},{Report.ItemChange}");
            builder.AppendLine();

            builder.AppendLine("Rank,Product,Generic,Strength,Quantity,Amount,PrevQuantity,PrevAmount,AmountChange");
            foreach (var row in Products)
            {
                builder.AppendLine(
                    $"{row.Rank},{Escape(row.ProductName)},{Escape(row.GenericName)},{Escape(row.Strength)}," +
                    $"{row.Quantity},{row.Amount},{row.PreviousQuantity},{row.PreviousAmount},{row.AmountChange}");
            }

            builder.AppendLine();
            builder.AppendLine("Ingredient,Strength,AwareGroup,Quantity,Amount,Counselled,Sales,PrintRate,PrevQuantity,QuantityChange");
            foreach (var row in Antibiotics)
            {
                builder.AppendLine(
                    $"{Escape(row.Ingredient)},{Escape(row.Strength)},{row.AwareGroup}," +
                    $"{row.Quantity},{row.Amount},{row.CounsellingPrinted},{row.SaleCount}," +
                    $"{row.PrintedPercentDisplay},{row.PreviousQuantity},{row.QuantityChange}");
            }

            File.WriteAllText(dialog.FileName, builder.ToString(), Encoding.UTF8);
            Message = "Export completed successfully.";
        }
        catch (Exception)
        {
            Message = "Export failed. Please try again.";
        }
    }

    /// <summary>성분명에 쉼표가 들어가는 복합제가 있어 CSV 값은 감싸 준다.</summary>
    private static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value.Contains(',') || value.Contains('"')
            ? "\"" + value.Replace("\"", "\"\"") + "\""
            : value;
    }
}
