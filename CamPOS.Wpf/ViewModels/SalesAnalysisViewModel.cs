using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Text;
using Microsoft.Win32;
using CamPOS.Application;
using CamPOS.Application.Receipts;
using CamPOS.Application.Reports;
using CamPOS.Wpf.ViewModels.Base;
using CamPOS.Wpf.Views;

namespace CamPOS.Wpf.ViewModels;

/// <summary>상품 순위의 정렬 기준.</summary>
public enum ProductSortOption
{
    Amount,
    Quantity
}

/// <summary>
/// 매출 분석 화면의 ViewModel (History → Analysis).
///
/// 항생제 리포트에서 갈라져 나온 쪽이다. 한 화면에 있던 동안 왼쪽은 약국 전체 매출,
/// 오른쪽은 항생제였는데, 두 축을 한 화면에 두면 기간을 고를 때마다 둘 다 다시 읽히는
/// 것 말고는 공유하는 것이 없었다. 더 중요한 차이는 <b>나가는 곳</b>이다 — 항생제 쪽은
/// 연구기관에 제출되고 이쪽은 약국이 자기 장부로 쓴다.
///
/// 그래서 접근 권한도 다르다. 이 화면은 <b>관리자 전용</b>이다(갈라지기 전 Reports 화면이
/// 그랬다). 매출을 직원에게 여는 것은 별개의 결정이고, 화면을 나누는 작업이 곁다리로
/// 해 버릴 일이 아니다.
/// </summary>
public class SalesAnalysisViewModel : ReportPeriodViewModelBase
{
    private readonly IReceiptSettingsService _receiptSettingsService;

    private ProductSortOption _selectedSort = ProductSortOption.Amount;

    /// <summary>
    /// 조회해 온 원본. 정렬을 바꿀 때 DB를 다시 치지 않고 이걸 다시 늘어놓는다.
    /// </summary>
    private IReadOnlyList<ProductSalesRow> _loadedProducts = Array.Empty<ProductSalesRow>();

    /// <summary>가장 높은 달의 순매출. 막대 높이의 기준이자 세로축 눈금이다.</summary>
    private decimal _salesTrendMax;

    /// <summary>달력이 지금 보여주는 달의 1일. 고른 기간과는 별개로 ◀ ▶로 움직인다.</summary>
    private DateTime _calendarMonth;

    /// <summary>달력이 아니라 12개월 그래프를 보고 있는지. 아래 패널은 둘 중 하나다.</summary>
    private bool _isCalendarVisible;

    /// <summary>달력의 그 달 합계. 칸을 다 더한 값이라 세로로 훑지 않아도 된다.</summary>
    private decimal _calendarTotal;

    private int _calendarTransactionCount;

    // 통화 설정. 리포트 금액은 달러로 저장돼 있고, 리엘은 화면에서만 환산한다.
    private decimal _exchangeRate;
    private int _rielRounding = 100;
    private bool _showRiel;

    public ObservableCollection<ProductSalesRow> Products { get; } = new();

    /// <summary>최근 12개월 순매출 추이의 막대들.</summary>
    public ObservableCollection<SalesTrendBar> SalesTrendBars { get; } = new();

    /// <summary>
    /// 매출 달력의 칸들. 앞뒤의 빈 자리까지 들어 있어 7의 배수다 —
    /// 그래야 1일이 제 요일 자리에 선다.
    /// </summary>
    public ObservableCollection<SalesCalendarCell> CalendarCells { get; } = new();

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

    /// <summary>
    /// 추이 그래프가 덮는 구간. 고른 기간과 다르다는 사실을 화면에 적어 두지 않으면
    /// 카드의 금액과 그래프의 합이 왜 다른지 알 수 없다.
    /// </summary>
    public string TrendPeriodLabel =>
        SalesTrendBars.Count == 0
            ? string.Empty
            : $"{SalesTrendBars[0].FullLabel} ~ {SalesTrendBars[^1].FullLabel}";

    /// <summary>매출 그래프의 세로축 눈금.</summary>
    public string SalesTrendMaxDisplay =>
        _salesTrendMax <= 0 ? string.Empty : _salesTrendMax.ToString("N0", CultureInfo.InvariantCulture);

    public bool HasSalesTrendData => _salesTrendMax > 0;

    // ── 리엘 환산 ────────────────────────────────────────────────────────────
    //
    // 합계 금액에만 붙인다. 표의 줄마다 붙이면 칸이 빽빽해지는 데 비해, 약국이
    // 리엘로 감을 잡고 싶은 것은 "이번 달에 얼마 벌었나" 한 줄이다.
    //
    // **환율 표기를 반드시 함께 낸다.** 판매 시점의 환율은 저장되지 않아 지난달
    // 매출도 오늘 환율로 환산된다. 환율이 오르면 과거 매출액이 저절로 올라가 보이는데
    // 그만큼 더 판 것이 아니다. 어느 환율로 계산한 값인지 적혀 있어야 참고값으로
    // 읽히고, 없으면 그냥 틀린 숫자가 된다.

    public bool IsRielShown => _showRiel && _exchangeRate > 0;

    /// <summary>고른 기간 매출의 리엘 환산.</summary>
    public string SalesAmountInRiel =>
        IsRielShown && Report is not null
            ? RielConverter.Format(Report.Current.Amount, _exchangeRate, _rielRounding)
            : string.Empty;

    /// <summary>달력이 보여주는 달 합계의 리엘 환산.</summary>
    public string CalendarTotalInRiel =>
        IsRielShown && (_calendarTotal != 0 || _calendarTransactionCount > 0)
            ? RielConverter.Format(_calendarTotal, _exchangeRate, _rielRounding)
            : string.Empty;

    /// <summary>어느 환율로 환산한 값인지. 이게 없으면 위 숫자를 믿을 수 없다.</summary>
    public string ExchangeRateNote => IsRielShown
        ? $"1 USD = {_exchangeRate.ToString("N0", CultureInfo.InvariantCulture)} {RielConverter.RielSymbol}"
        : string.Empty;

    // ── 매출 달력 ────────────────────────────────────────────────────────────
    //
    // 아래 자리를 12개월 그래프와 나눠 쓴다. 같은 주제(약국 전체의 시간별 매출)를
    // 다른 배율로 본 것이라, 둘을 따로 놓기보다 한 자리에서 바꿔 보는 편이 맞다.

    /// <summary>달력을 보고 있는지. false면 12개월 그래프다.</summary>
    public bool IsCalendarVisible
    {
        get => _isCalendarVisible;
        private set
        {
            if (SetProperty(ref _isCalendarVisible, value))
            {
                OnPropertyChanged(nameof(IsTrendVisible));
            }
        }
    }

    public bool IsTrendVisible => !_isCalendarVisible;

    public string CalendarMonthLabel =>
        _calendarMonth.ToString("MMMM yyyy", CultureInfo.InvariantCulture);

    public string CalendarTotalLabel =>
        _calendarTransactionCount == 0 && _calendarTotal == 0
            ? "no sales this month"
            : $"{_calendarTotal.ToString("N2", CultureInfo.InvariantCulture)} · "
              + $"{_calendarTransactionCount.ToString("N0", CultureInfo.InvariantCulture)} transactions";

    /// <summary>다음 달 단추. 아직 오지 않은 달로는 넘어갈 수 없다.</summary>
    public bool CanGoToNextMonth =>
        _calendarMonth < new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);

    public RelayCommand ShowCalendarCommand { get; }
    public RelayCommand ShowTrendCommand { get; }
    public RelayCommand PreviousMonthCommand { get; }
    public RelayCommand NextMonthCommand { get; }

    /// <summary>날짜 칸을 누르면 그 하루를 기간으로 삼고 리포트를 다시 돌린다.</summary>
    public RelayCommand SelectDayCommand { get; }

    public SalesAnalysisViewModel(
        IReportService reportService,
        IReceiptSettingsService receiptSettingsService,
        string facilityId)
        : base(reportService, facilityId)
    {
        _receiptSettingsService = receiptSettingsService;

        ShowCalendarCommand = new RelayCommand(async _ => await ShowCalendarAsync());
        ShowTrendCommand = new RelayCommand(_ => IsCalendarVisible = false);
        PreviousMonthCommand = new RelayCommand(async _ => await MoveCalendarAsync(-1));
        NextMonthCommand = new RelayCommand(
            async _ => await MoveCalendarAsync(1), _ => CanGoToNextMonth);
        SelectDayCommand = new RelayCommand(async cell => await SelectDayAsync(cell as SalesCalendarCell));

        _calendarMonth = DateFrom ?? DateTime.Today;

        // 환율은 리포트와 별개로 읽는다. 금액은 달러로 저장돼 있고 리엘은 화면 환산이라,
        // 리포트 조회가 통화 설정을 기다릴 이유가 없다.
        _ = LoadCurrencySettingsAsync();
        _ = LoadAsync();
    }

    /// <summary>화면을 열 때 한 번 읽는다. 못 읽으면 리엘 줄 없이 종전대로 나온다.</summary>
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
        OnPropertyChanged(nameof(ExchangeRateNote));
        OnPropertyChanged(nameof(SalesAmountInRiel));
        OnPropertyChanged(nameof(CalendarTotalInRiel));
    }

    /// <summary>
    /// 달력으로 바꾼다. 처음 열 때는 고른 기간이 끝나는 달을 보여준다 —
    /// 지난달 리포트를 보다 달력을 켰는데 이번 달이 나오면 방금 보던 것과 어긋난다.
    /// </summary>
    private async Task ShowCalendarAsync()
    {
        IsCalendarVisible = true;

        if (CalendarCells.Count == 0)
        {
            var end = DateTo ?? DateTime.Today;
            _calendarMonth = new DateTime(end.Year, end.Month, 1);
            await LoadCalendarAsync();
        }
    }

    private async Task MoveCalendarAsync(int monthOffset)
    {
        _calendarMonth = _calendarMonth.AddMonths(monthOffset);
        await LoadCalendarAsync();
    }

    /// <summary>
    /// 달력 한 달을 읽어 칸을 짓는다. 리포트 본체는 건드리지 않는다 —
    /// 달만 넘기는데 표와 그래프까지 다시 조회할 이유가 없다.
    /// </summary>
    private async Task LoadCalendarAsync()
    {
        OnPropertyChanged(nameof(CalendarMonthLabel));
        NextMonthCommand.RaiseCanExecuteChanged();

        var result = await ReportService.GetDailySalesAsync(FacilityId, _calendarMonth);

        CalendarCells.Clear();
        _calendarTotal = 0;
        _calendarTransactionCount = 0;

        if (!result.IsSuccess)
        {
            Message = result.Message!;
            OnPropertyChanged(nameof(CalendarTotalLabel));
            OnPropertyChanged(nameof(CalendarTotalInRiel));
            return;
        }

        var today = DateTime.Today;

        // 1일 앞의 빈 칸. 이 앱의 달력은 월요일에서 시작한다.
        var leading = ((int)_calendarMonth.DayOfWeek + 6) % 7;

        for (var i = 0; i < leading; i++)
        {
            CalendarCells.Add(SalesCalendarCell.Filler());
        }

        foreach (var day in result.Days)
        {
            CalendarCells.Add(SalesCalendarCell.For(day, today));
            _calendarTotal += day.Amount;
            _calendarTransactionCount += day.TransactionCount;
        }

        // 마지막 주를 7칸으로 채운다. 비워 두면 마지막 줄만 칸 너비가 달라진다.
        while (CalendarCells.Count % 7 != 0)
        {
            CalendarCells.Add(SalesCalendarCell.Filler());
        }

        OnPropertyChanged(nameof(CalendarTotalLabel));
        OnPropertyChanged(nameof(CalendarTotalInRiel));
    }

    /// <summary>
    /// 그 하루를 기간으로 삼는다. 따로 팝업을 띄우지 않는 이유: 매출 카드와 상품 순위가
    /// 모두 그 날 기준으로 바뀌므로 팝업 하나보다 보이는 것이 많다. 줄 단위 내역은
    /// 판매 이력 화면이 이미 하는 일이라 여기서 되풀이하지 않는다.
    /// </summary>
    private async Task SelectDayAsync(SalesCalendarCell? cell)
    {
        if (cell?.Date is not { } date)
        {
            return;
        }

        DateFrom = date;
        DateTo = date;

        await LoadAsync();
    }

    protected override async Task OnReportLoadedAsync(ReportData data)
    {
        _loadedProducts = data.Products;
        ApplySorting();

        BuildSalesTrendBars(data.SalesTrend);

        // 달력을 켜 둔 채 기간을 바꿨으면 달력도 그 달로 따라간다. 화면에 보이는 달과
        // 리포트가 말하는 달이 다르면 어느 쪽 숫자인지 알 수 없다.
        if (IsCalendarVisible)
        {
            var end = data.Range.To;
            var month = new DateTime(end.Year, end.Month, 1);

            if (month != _calendarMonth || CalendarCells.Count == 0)
            {
                _calendarMonth = month;
                await LoadCalendarAsync();
            }
        }
    }

    protected override void OnReportPropertiesChanged()
    {
        OnPropertyChanged(nameof(TrendPeriodLabel));
        OnPropertyChanged(nameof(HasSalesTrendData));
        OnPropertyChanged(nameof(SalesTrendMaxDisplay));
        OnPropertyChanged(nameof(SalesAmountInRiel));
    }

    protected override string EmptyPeriodMessage(ReportData data) =>
        data.Current.TransactionCount == 0 ? $"No sales in {data.Range.Label}." : string.Empty;

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
    /// 약국의 매출 파일만 쓴다.
    ///
    /// <b>이 파일은 약국 밖으로 나가지 않는다.</b> 항생제 파일은 AMR 연구에 의무
    /// 제출되는데, 매출액이 거기 함께 실리면 약국이 제출 자체를 꺼리게 된다. 그래서
    /// 두 파일은 따로이고, 이제는 화면도 따로다.
    ///
    /// 성분별 항생제 <b>매출액</b>은 여기 있다 — 항생제 파일에서 뺀 값이고, 상품 순위만으로는
    /// 알 수 없다(상품 여러 개가 한 성분인 경우가 흔하다). 화면에 표가 없어도 파일에는
    /// 남긴다. 지우면 약국이 그 값을 볼 곳이 없어진다.
    /// </summary>
    protected override void ExecuteExport()
    {
        Message = string.Empty;

        if (Report is null || Products.Count == 0)
        {
            Message = "No data to export.";
            return;
        }

        var dialog = new OpenFolderDialog { Title = "Select Export Folder" };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var fileName =
            $"report_sales_{AppVersion.FileTag}_{Report.Range.From:yyyyMMdd}_{Report.Range.To:yyyyMMdd}.csv";
        var path = Path.Combine(dialog.FolderName, fileName);

        // 파일 이름이 기간으로 정해지므로 같은 기간을 두 번 내보내면 덮어쓴다.
        // 폴더를 고르는 방식에는 저장 대화상자의 덮어쓰기 경고가 없어 여기서 대신 묻는다.
        if (File.Exists(path)
            && !AppDialog.Confirm(
                "Overwrite File",
                "A report for this period is already in that folder.\n\nReplace it?",
                confirmText: "Replace",
                cancelText: "Cancel"))
        {
            Message = "Export cancelled.";
            return;
        }

        try
        {
            File.WriteAllText(path, BuildSalesCsv(Report), Encoding.UTF8);
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

        Message = $"Exported {fileName}.";
    }

    private string BuildSalesCsv(ReportData report)
    {
        var builder = new StringBuilder();

        AppendPeriodHeader(builder, report);

        builder.AppendLine("Summary,Current,Previous,Change");
        builder.AppendLine(CultureInfo.InvariantCulture, $"Sales amount,{report.Current.Amount:0.00},{report.Previous.Amount:0.00},{report.AmountChange}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"Transactions,{report.Current.TransactionCount},{report.Previous.TransactionCount},{report.TransactionChange}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"Units sold,{report.Current.ItemCount},{report.Previous.ItemCount},{report.ItemChange}");
        builder.AppendLine();

        // 화면에는 비중만 두었지만 파일에는 증감도 함께 남긴다 —
        // 내보낸 파일로 기간을 비교하는 것은 화면과 다른 용도다.
        builder.AppendLine("Rank,Product,Generic,Strength,Quantity,Amount,AmountShare,PrevQuantity,PrevAmount,AmountChange");
        foreach (var row in Products)
        {
            builder.AppendLine(CultureInfo.InvariantCulture, $"{row.Rank},{Escape(row.ProductName)},{Escape(row.GenericName)},{Escape(row.Strength)}," +
                $"{row.Quantity},{row.Amount:0.00},{Escape(row.AmountShare)}," +
                $"{row.PreviousQuantity},{row.PreviousAmount:0.00},{Escape(row.AmountChange)}");
        }

        // 성분별 항생제 매출. 항생제 파일에서 뺀 금액이 여기 있다 —
        // 그 파일은 약국 밖으로 나가지만 이 파일은 약국이 갖는다.
        // 화면에 이 표가 없어도(항생제 리포트로 갔다) 파일에는 그대로 남긴다.
        builder.AppendLine();
        builder.AppendLine("AntibioticRevenue");
        builder.AppendLine("Ingredient,Strength,AwareGroup,Quantity,Amount");
        foreach (var row in report.Antibiotics)
        {
            builder.AppendLine(CultureInfo.InvariantCulture, $"{Escape(row.Ingredient)},{Escape(row.Strength)},{row.AwareGroup}," +
                $"{row.Quantity},{row.Amount:0.00}");
        }

        builder.AppendLine();
        AppendTrendHeader(builder, report, "SalesTrend");
        builder.AppendLine("Month,NetAmount,Transactions");
        foreach (var point in report.SalesTrend)
        {
            builder.AppendLine(CultureInfo.InvariantCulture, $"{point.FullLabel},{point.Amount},{point.TransactionCount}");
        }

        return builder.ToString();
    }

    /// <summary>
    /// 파일 맨 위에 기간을 적는다. 파일 하나만 열어도 언제 것인지 알 수 있어야 하고,
    /// 파일 이름은 옮기다 보면 바뀐다.
    /// </summary>
    private static void AppendPeriodHeader(StringBuilder builder, ReportData report)
    {
        builder.AppendLine(CultureInfo.InvariantCulture, $"App version,{AppVersion.Display}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"Report period,{report.Range.Label}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"Compared with,{report.Range.PreviousLabel}");
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

        builder.AppendLine(CultureInfo.InvariantCulture, $"{name} ({months})");
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
