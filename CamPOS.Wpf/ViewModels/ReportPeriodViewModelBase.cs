using CamPOS.Application.Reports;
using CamPOS.Wpf.ViewModels.Base;

namespace CamPOS.Wpf.ViewModels;

/// <summary>
/// 기간을 골라 리포트를 읽는 화면들의 공통 부분.
///
/// 항생제 리포트와 매출 분석은 <b>같은 조회 결과</b>(<see cref="ReportData"/>)를 좌우로
/// 나눠 보는 두 화면이다. 기간 선택·프리셋·비교 기준 문구를 각자 들고 있으면 한쪽만
/// 고쳐졌을 때 두 화면이 같은 날짜에 서로 다른 숫자를 말하게 되고, 그게 틀렸다는 것을
/// 알아볼 방법이 없다 — 각자 자기 안에서는 앞뒤가 맞기 때문이다. 그래서 기간에 관한
/// 것은 전부 여기 있고, 아래 두 화면은 "읽은 결과로 무엇을 보여줄지"만 정한다.
///
/// 조회를 쪼개지 않은 것도 같은 이유다. <see cref="IReportService.GetReportAsync"/>는 한 번에
/// 다 집계하므로 두 화면이 각자 전체를 읽는 낭비가 있지만, 쿼리를 둘로 가르면
/// "표·카드·그래프의 합계가 일치한다"는 규칙(DailySalesTests)을 양쪽에서 다시 증명해야 한다.
/// </summary>
public abstract class ReportPeriodViewModelBase : ViewModelBase
{
    protected readonly IReportService ReportService;
    protected readonly string FacilityId;

    private DateTime? _dateFrom;
    private DateTime? _dateTo;
    private ReportData? _report;
    private string _message = string.Empty;
    private bool _isLoading;

    protected ReportPeriodViewModelBase(IReportService reportService, string facilityId)
    {
        ReportService = reportService;
        FacilityId = facilityId;

        RefreshCommand = new RelayCommand(async _ => await LoadAsync());
        ThisMonthCommand = new RelayCommand(async _ => await SelectMonthAsync(0));
        LastMonthCommand = new RelayCommand(async _ => await SelectMonthAsync(-1));
        ExportCommand = new RelayCommand(_ => ExecuteExport());
        BackCommand = new RelayCommand(_ => NavigateBack?.Invoke());

        // 처음 열면 이번 달 1일부터 오늘까지.
        var today = DateTime.Today;
        _dateFrom = new DateTime(today.Year, today.Month, 1);
        _dateTo = today;
    }

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
                OnReportPropertiesChanged();
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

    public RelayCommand RefreshCommand { get; }
    public RelayCommand ThisMonthCommand { get; }
    public RelayCommand LastMonthCommand { get; }
    public RelayCommand ExportCommand { get; }
    public RelayCommand BackCommand { get; }

    public event Action? NavigateBack;

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

        var result = await ReportService.GetReportAsync(FacilityId, DateFrom, DateTo);

        IsLoading = false;

        if (!result.IsSuccess)
        {
            Message = result.Message!;
            return;
        }

        var data = result.Data!;

        await OnReportLoadedAsync(data);

        Report = data;

        // 기간을 고를 때 화면이 비어 보이는 이유를 분명히 알려 준다.
        Message = EmptyPeriodMessage(data);
    }

    /// <summary>
    /// 읽은 결과를 화면의 목록·그래프로 옮긴다. <see cref="Report"/>가 세워지기 <b>전</b>에
    /// 불리므로, 여기서 채운 값에 의존하는 속성은 <see cref="OnReportPropertiesChanged"/>에서 알린다.
    /// </summary>
    protected abstract Task OnReportLoadedAsync(ReportData data);

    /// <summary>
    /// <see cref="Report"/>가 바뀔 때 함께 다시 읽혀야 하는 파생 속성을 알린다.
    /// </summary>
    protected virtual void OnReportPropertiesChanged()
    {
    }

    protected abstract void ExecuteExport();

    /// <summary>
    /// 기간에 아무것도 없을 때 띄울 문구. 두 화면이 세는 것이 다르다 —
    /// 매출 분석은 판매가 없는 것이, 항생제 리포트는 항생제 판매가 없는 것이 빈 상태다.
    /// </summary>
    protected virtual string EmptyPeriodMessage(ReportData data) => string.Empty;
}
