using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Text;
using Microsoft.Win32;
using PharmaPOS.Application.Counselling;
using PharmaPOS.Application.Reports;
using Lightweight_Digital_Inventory_Management___POS_System.ViewModels.Base;
using Lightweight_Digital_Inventory_Management___POS_System.Views;

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
    private readonly ICounsellingSettingsService _counsellingSettingsService;
    private readonly string _facilityId;

    /// <summary>
    /// 연구기관이 부여한 사이트 코드. 항생제 파일에만 실린다.
    /// 설정 화면(Antibiotic Counselling)에서 바뀔 수 있으므로 리포트를 읽을 때마다 다시 읽는다.
    /// </summary>
    private string _researchSiteCode = string.Empty;

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

    /// <summary>가장 높은 달의 판매 수량. 막대 높이의 기준이자 세로축 눈금이다.</summary>
    private int _trendMax;

    /// <summary>가장 높은 달의 순매출. 위와 같은 역할이다.</summary>
    private decimal _salesTrendMax;

    public ObservableCollection<ProductSalesRow> Products { get; } = new();
    public ObservableCollection<AntibioticSalesRow> Antibiotics { get; } = new();

    /// <summary>요약 카드의 ACCESS 비중 아래에 네 등급을 따로 늘어놓는다.</summary>
    public ObservableCollection<AwareGroupShare> GroupShares { get; } = new();

    /// <summary>최근 12개월 항생제 판매 추이의 막대들.</summary>
    public ObservableCollection<AntibioticTrendBar> TrendBars { get; } = new();

    /// <summary>최근 12개월 순매출 추이의 막대들. 항생제 추이와 같은 창을 쓴다.</summary>
    public ObservableCollection<SalesTrendBar> SalesTrendBars { get; } = new();

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
                OnPropertyChanged(nameof(CounsellingPrintRateDisplay));
                OnPropertyChanged(nameof(CounsellingSummary));
                OnPropertyChanged(nameof(HasAntibioticData));
                OnPropertyChanged(nameof(TrendPeriodLabel));
                OnPropertyChanged(nameof(HasTrendData));
                OnPropertyChanged(nameof(TrendMaxDisplay));
                OnPropertyChanged(nameof(HasSalesTrendData));
                OnPropertyChanged(nameof(SalesTrendMaxDisplay));
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

    /// <summary>
    /// 복약안내 출력률. 항생제를 판 줄 중 몇 %에 안내가 나갔는지다.
    ///
    /// 이 자리에 있던 ACCESS 비중은 바로 아래 등급별 비율이 네 등급과 함께
    /// 더 자세히 보여준다. 같은 숫자를 두 번 두는 대신, 화면 어디에도 한 값으로
    /// 없던 출력률을 여기 둔다 — 안내가 실제로 전달되고 있는지가 이 기능의 핵심이다.
    /// </summary>
    public string CounsellingPrintRateDisplay =>
        Report?.CounsellingPrintRatePercent is { } rate
            ? rate.ToString("0.#", CultureInfo.InvariantCulture) + "%"
            : "—";

    public string CounsellingSummary =>
        Report is null
            ? string.Empty
            : $"{Report.CounsellingPrintedCount} printed / {Report.AntibioticSaleCount} antibiotic sales";

    /// <summary>항생제 표가 비어 있을 때 안내 문구를 대신 보여주기 위한 값.</summary>
    public bool HasAntibioticData => Antibiotics.Count > 0;

    /// <summary>
    /// 추이 그래프가 덮는 구간. 고른 기간과 다르다는 사실을 화면에 적어 두지 않으면
    /// 표의 합과 그래프의 합이 왜 다른지 알 수 없다.
    /// </summary>
    public string TrendPeriodLabel
    {
        get
        {
            if (TrendBars.Count == 0)
            {
                return string.Empty;
            }

            return $"{TrendBars[0].FullLabel} ~ {TrendBars[^1].FullLabel}";
        }
    }

    /// <summary>세로축 눈금. 가장 높은 달이 몇 개였는지 적어야 막대 높이를 숫자로 읽을 수 있다.</summary>
    public string TrendMaxDisplay =>
        _trendMax == 0 ? string.Empty : _trendMax.ToString("N0", CultureInfo.InvariantCulture);

    /// <summary>1년 내내 항생제 판매가 없으면 빈 그래프 대신 안내 문구를 보여준다.</summary>
    public bool HasTrendData => _trendMax > 0;

    /// <summary>매출 그래프의 세로축 눈금.</summary>
    public string SalesTrendMaxDisplay =>
        _salesTrendMax <= 0 ? string.Empty : _salesTrendMax.ToString("N0", CultureInfo.InvariantCulture);

    public bool HasSalesTrendData => _salesTrendMax > 0;

    public RelayCommand RefreshCommand { get; }
    public RelayCommand ThisMonthCommand { get; }
    public RelayCommand LastMonthCommand { get; }
    public RelayCommand ExportCommand { get; }
    public RelayCommand BackCommand { get; }

    public event Action? NavigateBack;

    public ReportsViewModel(
        IReportService reportService,
        ICounsellingSettingsService counsellingSettingsService,
        string facilityId)
    {
        _reportService = reportService;
        _counsellingSettingsService = counsellingSettingsService;
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

        // 설정 화면에서 바뀌었을 수 있으므로 매번 다시 읽는다.
        // 읽지 못해도 리포트는 보여줘야 한다 — 내보낼 때 출처가 빠질 뿐이다.
        _researchSiteCode = (await _counsellingSettingsService.GetAsync()).ResearchSiteCode;

        IsLoading = false;

        if (!result.IsSuccess)
        {
            Message = result.Message!;
            return;
        }

        var data = result.Data!;

        _loadedProducts = data.Products;
        ApplySorting();

        // 비중의 분모. 표에 실린 줄들의 수량 합이라 화면의 비중을 다 더하면 100%가 된다.
        var totalAntibioticQuantity = data.Antibiotics.Sum(r => r.Quantity);

        Antibiotics.Clear();
        foreach (var row in data.Antibiotics)
        {
            row.TotalQuantityInPeriod = totalAntibioticQuantity;
            Antibiotics.Add(row);
        }

        GroupShares.Clear();
        foreach (var share in data.GroupShares)
        {
            GroupShares.Add(share);
        }

        BuildTrendBars(data.AntibioticTrend);
        BuildSalesTrendBars(data.SalesTrend);

        Report = data;
        OnPropertyChanged(nameof(HasAntibioticData));

        // 기간을 고를 때 화면이 비어 보이는 이유를 분명히 알려 준다.
        Message = data.Current.TransactionCount == 0
            ? $"No sales in {data.Range.Label}."
            : string.Empty;
    }

    /// <summary>
    /// 월별 집계를 막대 높이로 옮긴다.
    ///
    /// 기준(_trendMax)은 가장 높은 달의 총량이다. 등급별 최댓값이 아니라 총량으로 잡는
    /// 이유: 막대가 쌓여 올라가므로 총량이 곧 막대의 높이이고, 그걸 넘는 기준을 쓰면
    /// 그래프 위쪽이 늘 비어 보인다.
    /// </summary>
    private void BuildTrendBars(IReadOnlyList<AntibioticTrendPoint> trend)
    {
        _trendMax = trend.Count == 0 ? 0 : trend.Max(p => p.TotalQuantity);

        TrendBars.Clear();
        foreach (var point in trend)
        {
            TrendBars.Add(AntibioticTrendBar.From(point, _trendMax));
        }
    }

    /// <summary>
    /// 월별 순매출을 막대 높이로 옮긴다.
    ///
    /// 기준은 가장 큰 달의 금액이다. 환불이 판매보다 많았던 달은 금액이 음수인데
    /// 그런 달은 막대가 0으로 그려진다 — 아래로 자라는 막대를 그리려면 0선을 가운데로
    /// 옮겨야 하고, 그러면 흔한 경우인 "전부 양수"에서 위쪽 절반이 늘 비어 보인다.
    /// 실제 금액은 막대 위 숫자와 툴팁이 말해 준다.
    /// </summary>
    private void BuildSalesTrendBars(IReadOnlyList<SalesTrendPoint> trend)
    {
        _salesTrendMax = trend.Count == 0 ? 0m : trend.Max(p => p.Amount);

        SalesTrendBars.Clear();
        foreach (var point in trend)
        {
            SalesTrendBars.Add(SalesTrendBar.From(point, _salesTrendMax));
        }
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

        // 비중의 분모. 정렬을 바꿔도 같은 값이라 여기서 한 번만 구한다.
        // 표에 실린 줄들의 합을 쓰므로 화면의 비중을 다 더하면 100%가 된다.
        var totalAmount = _loadedProducts.Sum(r => r.Amount);

        Products.Clear();

        var rank = 1;
        foreach (var row in ordered)
        {
            row.Rank = rank++;
            row.TotalAmountInPeriod = totalAmount;
            Products.Add(row);
        }
    }

    /// <summary>
    /// 매출과 항생제를 각각 다른 파일로 내보낸다.
    ///
    /// 파일을 나누는 것은 보기 좋으라고가 아니라 <b>두 파일이 가는 곳이 다르기 때문</b>이다.
    /// 항생제 파일은 AMR 연구에 의무 제출되어 약국 밖으로 나간다. 매출 파일은 약국이
    /// 자기 장부로 쓰는 것이고, 매출액은 약국이 남에게 넘기기를 꺼리는 정보다.
    /// 둘이 한 파일에 있으면 의무 제출을 하려다 영업 정보까지 함께 넘기게 되고,
    /// 그 사실을 알아차린 약국은 제출 자체를 꺼리게 된다.
    ///
    /// 그래서 <b>항생제 파일에는 금액이 들어가지 않는다</b>. 그 파일을 만드는 코드는
    /// 주석만으로 규칙을 지킬 수 없어 테스트가 닿는 Application 쪽(AntibioticExportCsv)에 있다.
    ///
    /// 폴더를 고르고 데이터셋마다 파일을 하나씩 쓰는 방식은 Import/Export 화면의
    /// 내보내기와 같다. 이 저장소에 이미 있는 방식을 따른 것이다.
    /// </summary>
    private void ExecuteExport()
    {
        Message = string.Empty;

        if (Report is null || (Products.Count == 0 && Antibiotics.Count == 0))
        {
            Message = "No data to export.";
            return;
        }

        var dialog = new OpenFolderDialog { Title = "Select Export Folder" };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var salesFileName = $"report_sales_{Report.Range.From:yyyyMMdd}_{Report.Range.To:yyyyMMdd}.csv";
        var antibioticsFileName = $"report_antibiotics_{Report.Range.From:yyyyMMdd}_{Report.Range.To:yyyyMMdd}.csv";

        var salesPath = Path.Combine(dialog.FolderName, salesFileName);
        var antibioticsPath = Path.Combine(dialog.FolderName, antibioticsFileName);

        // 파일 이름이 기간으로 정해지므로 같은 기간을 두 번 내보내면 덮어쓴다.
        // 폴더를 고르는 방식에는 저장 대화상자의 덮어쓰기 경고가 없어 여기서 대신 묻는다.
        if ((File.Exists(salesPath) || File.Exists(antibioticsPath))
            && !AppDialog.Confirm(
                "Overwrite Files",
                "A report for this period is already in that folder.\n\nReplace it?",
                confirmText: "Replace",
                cancelText: "Cancel"))
        {
            Message = "Export cancelled.";
            return;
        }

        try
        {
            File.WriteAllText(salesPath, BuildSalesCsv(Report), Encoding.UTF8);
            File.WriteAllText(
                antibioticsPath, AntibioticExportCsv.Build(Report, _researchSiteCode), Encoding.UTF8);
        }
        catch (UnauthorizedAccessException)
        {
            Message = "Cannot write to the selected folder.";
            return;
        }
        catch (Exception)
        {
            Message = "Export failed. Please try again.";
            return;
        }

        // 파일이 두 개라 어느 것이 생겼는지 이름을 적어 준다.
        Message = $"Exported {salesFileName} and {antibioticsFileName}.";

        // 사이트 코드가 없으면 받는 쪽이 어느 약국 것인지 알 수 없다.
        // 내보내기를 막지는 않되(자기 확인용으로 뽑을 수 있다), 제출 전에 알려 준다.
        if (string.IsNullOrWhiteSpace(_researchSiteCode))
        {
            Message += " No research site code is set, so the antibiotic file cannot be attributed."
                + " Set it in Antibiotic Counselling settings before submitting it.";
        }
    }

    /// <summary>
    /// 약국 전체 매출. 화면 왼쪽 칸에 해당한다.
    /// </summary>
    private string BuildSalesCsv(ReportData report)
    {
        var builder = new StringBuilder();

        AppendPeriodHeader(builder, report);

        builder.AppendLine("Summary,Current,Previous,Change");
        builder.AppendLine($"Sales amount,{report.Current.Amount},{report.Previous.Amount},{report.AmountChange}");
        builder.AppendLine($"Transactions,{report.Current.TransactionCount},{report.Previous.TransactionCount},{report.TransactionChange}");
        builder.AppendLine($"Units sold,{report.Current.ItemCount},{report.Previous.ItemCount},{report.ItemChange}");
        builder.AppendLine();

        // 화면에는 비중만 두었지만 파일에는 증감도 함께 남긴다 —
        // 내보낸 파일로 기간을 비교하는 것은 화면과 다른 용도다.
        builder.AppendLine("Rank,Product,Generic,Strength,Quantity,Amount,AmountShare,PrevQuantity,PrevAmount,AmountChange");
        foreach (var row in Products)
        {
            builder.AppendLine(
                $"{row.Rank},{Escape(row.ProductName)},{Escape(row.GenericName)},{Escape(row.Strength)}," +
                $"{row.Quantity},{row.Amount},{Escape(row.AmountShare)}," +
                $"{row.PreviousQuantity},{row.PreviousAmount},{Escape(row.AmountChange)}");
        }

        // 성분별 항생제 매출. 항생제 파일에서 뺀 금액이 여기 있다 —
        // 그 파일은 약국 밖으로 나가지만 이 파일은 약국이 갖는다.
        // 상품 순위만으로는 알 수 없는 값이라(상품 여러 개가 한 성분인 경우가 흔하다)
        // 옮기지 않고 지우면 약국이 볼 곳이 없어진다.
        builder.AppendLine();
        builder.AppendLine("AntibioticRevenue");
        builder.AppendLine("Ingredient,Strength,AwareGroup,Quantity,Amount");
        foreach (var row in Antibiotics)
        {
            builder.AppendLine(
                $"{Escape(row.Ingredient)},{Escape(row.Strength)},{row.AwareGroup}," +
                $"{row.Quantity},{row.Amount}");
        }

        builder.AppendLine();
        AppendTrendHeader(builder, report, "SalesTrend");
        builder.AppendLine("Month,NetAmount,Transactions");
        foreach (var point in report.SalesTrend)
        {
            builder.AppendLine($"{point.FullLabel},{point.Amount},{point.TransactionCount}");
        }

        return builder.ToString();
    }

    /// <summary>
    /// 두 파일 모두 맨 위에 기간을 적는다. 파일 하나만 열어도 언제 것인지
    /// 알 수 있어야 하고, 파일 이름은 옮기다 보면 바뀐다.
    /// </summary>
    private static void AppendPeriodHeader(StringBuilder builder, ReportData report)
    {
        builder.AppendLine($"Report period,{report.Range.Label}");
        builder.AppendLine($"Compared with,{report.Range.PreviousLabel}");
        builder.AppendLine();
    }

    /// <summary>
    /// 추이는 고른 기간이 아니라 그 기간이 끝나는 달까지의 12개월이다.
    /// 파일에서도 헷갈리지 않도록 표 이름에 실제 구간을 적는다.
    /// </summary>
    private static void AppendTrendHeader(StringBuilder builder, ReportData report, string name)
    {
        var months = report.SalesTrend.Count > 0
            ? $"{report.SalesTrend[0].FullLabel} ~ {report.SalesTrend[^1].FullLabel}"
            : report.Range.Label;

        builder.AppendLine($"{name} ({months})");
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
