using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Text;
using Microsoft.Win32;
using CamPOS.Application;
using CamPOS.Application.Counselling;
using CamPOS.Application.Reports;
using CamPOS.Application.Repositories;
using CamPOS.Domain.Enums;
using CamPOS.Wpf.ViewModels.Base;
using CamPOS.Wpf.Views;

namespace CamPOS.Wpf.ViewModels;

/// <summary>
/// 항생제 리포트 화면의 ViewModel.
///
/// 왼쪽은 <b>무엇이 팔렸나</b>(등급별 비율 · 성분별 표 · 12개월 추이), 오른쪽은
/// <b>안내가 어떻게 나갔나</b>(용지 미리보기 · 설정 · 출력 현황)다. 매출은 이 화면에
/// 없다 — 매출 분석(History → Analysis)으로 갔다.
///
/// 한 화면에 모아 둔 이유는 셋이 한 고리이기 때문이다. 성분별 표에서 많이 팔린 항생제를
/// 고르면 그 성분의 안내지가 오른쪽에 그려지고, 설정을 고치면 그 자리에서 결과가 바뀌고,
/// 그 아래가 "그래서 실제로 몇 장 나갔나"를 말한다. 설정을 다른 화면에서 고치던 동안은
/// 바꾼 결과를 보려면 판매를 한 번 해 봐야 했다.
/// </summary>
public class AntibioticReportViewModel : ReportPeriodViewModelBase
{
    /// <summary>로케일 선택 목록의 한 줄.</summary>
    public class LocaleOption
    {
        public required string Code { get; init; }
        public required string Display { get; init; }
        public override string ToString() => Display;
    }

    private readonly ICounsellingSettingsService _settingsService;
    private readonly ICounsellingLocaleProvider _localeProvider;
    private readonly ICounsellingLogRepository _logRepository;
    private readonly UserRole _actingRole;

    /// <summary>
    /// 마지막으로 읽어 둔 설정 전부. 이 화면은 세 칸만 고치므로, 저장할 때 나머지
    /// 칸(인쇄 방식·출력 대상·폴더·사이트 코드)을 여기서 그대로 가져간다 —
    /// 빈 객체를 넘기면 손대지 않은 설정이 기본값으로 덮인다.
    /// </summary>
    private CounsellingSettings _settings = new();

    /// <summary>미리보기에 쓰는 현지어 레이어. 로케일 선택이 바뀔 때만 다시 읽는다.</summary>
    private CounsellingLocale _locale = CounsellingLocale.EnglishOnly;

    /// <summary>인쇄물에 출처로 찍히는 값. 참조 데이터에서 읽는다.</summary>
    private string? _sourceVersion;

    private int _trendMax;
    private CounsellingMetrics? _metrics;

    private AntibioticSalesRow? _selectedAntibiotic;
    private CounsellingSheetFormat _sheetFormat = CounsellingSheetFormat.Full;
    private LocaleOption? _selectedLocale;
    private string _qrUrl = string.Empty;
    private string _preview = string.Empty;
    private string _settingsMessage = string.Empty;
    private bool _hasUnsavedChanges;

    /// <summary>
    /// 설정을 처음 올려놓는 동안에는 바뀜 표시를 켜지 않는다. 화면을 열자마자
    /// "저장하지 않은 변경"이 떠 있으면 나갈 때마다 확인을 받게 된다.
    /// </summary>
    private bool _isLoadingSettings;

    public ObservableCollection<AntibioticSalesRow> Antibiotics { get; } = new();
    public ObservableCollection<AwareGroupShare> GroupShares { get; } = new();
    public ObservableCollection<AntibioticTrendBar> TrendBars { get; } = new();
    public ObservableCollection<LocaleOption> AvailableLocales { get; } = new();

    public IReadOnlyList<CounsellingSheetFormat> AvailableSheetFormats { get; } =
        Enum.GetValues<CounsellingSheetFormat>();

    // ── 왼쪽: 항생제 판매 현황 ───────────────────────────────────────────────

    /// <summary>항생제 표가 비어 있을 때 안내 문구를 대신 보여주기 위한 값.</summary>
    public bool HasAntibioticData => Antibiotics.Count > 0;

    /// <summary>
    /// 추이 그래프가 덮는 구간. 고른 기간과 다르다는 사실을 화면에 적어 두지 않으면
    /// 표의 합과 그래프의 합이 왜 다른지 알 수 없다.
    /// </summary>
    public string TrendPeriodLabel =>
        TrendBars.Count == 0 ? string.Empty : $"{TrendBars[0].FullLabel} ~ {TrendBars[^1].FullLabel}";

    /// <summary>세로축 눈금. 가장 높은 달이 몇 개였는지 적어야 막대 높이를 숫자로 읽을 수 있다.</summary>
    public string TrendMaxDisplay =>
        _trendMax == 0 ? string.Empty : _trendMax.ToString("N0", CultureInfo.InvariantCulture);

    public bool HasTrendData => _trendMax > 0;

    // ── 오른쪽 위: 용지 미리보기 ─────────────────────────────────────────────

    /// <summary>
    /// 성분별 표에서 고른 줄. 고르면 그 성분의 안내지가 미리보기에 뜬다.
    /// 아무것도 고르지 않았으면 기간 내 가장 많이 팔린 항생제를 쓴다 — 빈 미리보기는
    /// 설정을 고쳐도 결과를 볼 수 없다는 뜻이고, 그러면 이 자리에 둔 이유가 없어진다.
    /// </summary>
    public AntibioticSalesRow? SelectedAntibiotic
    {
        get => _selectedAntibiotic;
        set
        {
            if (SetProperty(ref _selectedAntibiotic, value))
            {
                OnPropertyChanged(nameof(PreviewSubject));
                RefreshPreview();
            }
        }
    }

    /// <summary>
    /// 지금 미리보기에 그려진 것이 무엇인지. 고른 것이 없을 때 "최다 판매"가 나와 있으면
    /// 왜 그 성분이 떠 있는지 알 수 없어서 적어 둔다.
    /// </summary>
    public string PreviewSubject
    {
        get
        {
            var row = EffectivePreviewRow;

            if (row is null)
            {
                return string.Empty;
            }

            return _selectedAntibiotic is null
                ? $"{row.Ingredient} — most sold in this period"
                : row.Ingredient;
        }
    }

    /// <summary>실제로 그릴 줄. 고른 것이 없으면 수량이 가장 많은 줄.</summary>
    private AntibioticSalesRow? EffectivePreviewRow =>
        _selectedAntibiotic ?? Antibiotics.OrderByDescending(r => r.Quantity).FirstOrDefault();

    /// <summary>
    /// 그려진 용지. <see cref="CounsellingSheetRenderer"/>를 그대로 쓴다 — 미리보기를
    /// 따로 그리면 화면과 종이가 갈라지고, 그때부터 미리보기는 아무것도 보증하지 않는다.
    /// (영수증 설정 화면이 같은 이유로 ReceiptRenderer를 쓴다.)
    /// </summary>
    public string Preview
    {
        get => _preview;
        private set => SetProperty(ref _preview, value);
    }

    public bool HasPreview => Preview.Length > 0;

    // ── 오른쪽 가운데: 안내지 설정 ───────────────────────────────────────────

    /// <summary>
    /// 저장이 허용되는지. <b>화면을 숨기는 용도가 아니다</b> — 실제 거부는
    /// <see cref="ICounsellingSettingsService.SaveAsync"/>가 하고, 이 값은 단추를 흐리게
    /// 하고 왜 흐린지 적어 주는 데만 쓴다.
    /// </summary>
    public bool CanEditSettings => _actingRole == UserRole.Administrator;

    public string SettingsPermissionNote => CanEditSettings
        ? string.Empty
        : "Only an administrator can change these.";

    public CounsellingSheetFormat SheetFormat
    {
        get => _sheetFormat;
        set
        {
            if (SetProperty(ref _sheetFormat, value))
            {
                MarkChanged();
                RefreshPreview();
            }
        }
    }

    public LocaleOption? SelectedLocale
    {
        get => _selectedLocale;
        set
        {
            if (SetProperty(ref _selectedLocale, value))
            {
                MarkChanged();
                _ = ReloadLocaleAsync();
            }
        }
    }

    public string QrUrl
    {
        get => _qrUrl;
        set
        {
            if (SetProperty(ref _qrUrl, value))
            {
                MarkChanged();
                RefreshPreview();
            }
        }
    }

    /// <summary>저장하지 않고 나가면 확인을 받기 위한 값.</summary>
    public bool HasUnsavedChanges
    {
        get => _hasUnsavedChanges;
        private set => SetProperty(ref _hasUnsavedChanges, value);
    }

    public string SettingsMessage
    {
        get => _settingsMessage;
        private set => SetProperty(ref _settingsMessage, value);
    }

    // ── 오른쪽 아래: 출력 현황 ───────────────────────────────────────────────
    //
    // 출력률과 인쇄 건수는 리포트 집계(ReportData)에서 그대로 가져온다. 나누기 전
    // Reports 화면의 요약 카드가 쓰던 값과 같은 자리라, 화면이 갈라졌다고 숫자가
    // 달라지지 않는다. 건너뜀의 이유(약사 판단 / 인쇄 실패)만은 거기서 알 수 없어
    // Counselling_Log를 따로 센다.

    public string PrintRateDisplay =>
        Report?.CounsellingPrintRatePercent is { } rate
            ? rate.ToString("0.#", CultureInfo.InvariantCulture) + "%"
            : "—";

    public string CounsellingSummary =>
        Report is null
            ? string.Empty
            : $"{Report.CounsellingPrintedCount} printed / {Report.AntibioticSaleCount} antibiotic sales";

    public string PharmacistSkippedDisplay => Count(_metrics?.PharmacistSkippedCount);
    public string PrintFailedDisplay => Count(_metrics?.PrintFailedCount);
    public string UnmatchedDisplay => Count(_metrics?.UnmatchedCount);

    /// <summary>
    /// 인쇄가 한 번이라도 실패했는지. 프린터를 봐야 한다는 뜻이라 눈에 띄게 표시한다 —
    /// 약사가 건너뛴 것과 달리 이쪽은 사람이 고쳐야 하고, 그대로 두면 출력률이
    /// 계속 떨어지는데 이유가 화면 어디에도 없다.
    /// </summary>
    public bool HasPrintFailures => _metrics?.PrintFailedCount > 0;

    /// <summary>
    /// 참조 데이터에서 찾지 못한 판매가 있는지. 시드 보강 대상이고, 그 판매에는
    /// 안내가 나가지 않았다.
    /// </summary>
    public bool HasUnmatched => _metrics?.UnmatchedCount > 0;

    private static string Count(int? value) =>
        value is null ? "—" : value.Value.ToString("N0", CultureInfo.InvariantCulture);

    public RelayCommand SaveSettingsCommand { get; }
    public RelayCommand MoreSettingsCommand { get; }

    /// <summary>인쇄 방식과 출력 대상은 기존 설정 화면에 남아 있어, 그쪽으로 넘긴다.</summary>
    public event Action? NavigateToMoreSettings;

    public AntibioticReportViewModel(
        IReportService reportService,
        ICounsellingSettingsService settingsService,
        ICounsellingLocaleProvider localeProvider,
        ICounsellingLogRepository logRepository,
        string facilityId,
        UserRole actingRole)
        : base(reportService, facilityId)
    {
        _settingsService = settingsService;
        _localeProvider = localeProvider;
        _logRepository = logRepository;
        _actingRole = actingRole;

        SaveSettingsCommand = new RelayCommand(async _ => await ExecuteSaveSettingsAsync());
        MoreSettingsCommand = new RelayCommand(_ => NavigateToMoreSettings?.Invoke());

        // 설정은 리포트와 별개로 읽는다. 하나가 다른 하나를 기다릴 이유가 없다.
        _ = LoadSettingsAsync();
        _ = LoadAsync();
    }

    // ── 설정 읽기 ────────────────────────────────────────────────────────────

    private async Task LoadSettingsAsync()
    {
        _isLoadingSettings = true;

        try
        {
            _settings = await _settingsService.GetAsync();

            SheetFormat = _settings.SheetFormat;
            QrUrl = _settings.QrUrl;

            await LoadLocaleOptionsAsync(_settings.LocaleCode);

            var (_, sourceVersion) = await _settingsService.GetReferenceDataStatusAsync();
            _sourceVersion = sourceVersion;
        }
        finally
        {
            _isLoadingSettings = false;
            HasUnsavedChanges = false;
        }

        RefreshPreview();
    }

    private async Task LoadLocaleOptionsAsync(string? currentCode)
    {
        AvailableLocales.Clear();

        // 첫 줄은 항상 "영어 단독"이다. 영어는 고정 레이어라 끌 수 없고,
        // 현지어를 붙이지 않는 선택지가 필요하다.
        AvailableLocales.Add(new LocaleOption { Code = string.Empty, Display = "English only" });

        foreach (var locale in await _localeProvider.ListAvailableLocalesAsync())
        {
            // 미검수 로케일도 목록에는 보여주고, 고르더라도 현지어는 인쇄되지 않는다는
            // 사실을 라벨에 적는다 — 안 보이면 왜 안 나오는지 알 수 없다.
            var status = locale.IsApproved ? "approved" : "not reviewed - English only";

            var name = string.IsNullOrWhiteSpace(locale.LanguageName)
                ? locale.LocaleCode
                : $"{locale.LocaleCode} ({locale.LanguageName})";

            AvailableLocales.Add(new LocaleOption
            {
                Code = locale.LocaleCode,
                Display = $"{name} - {status}"
            });
        }

        SelectedLocale =
            AvailableLocales.FirstOrDefault(
                o => string.Equals(o.Code, currentCode, StringComparison.OrdinalIgnoreCase))
            ?? AvailableLocales[0];

        await ReloadLocaleAsync();
    }

    /// <summary>
    /// 고른 로케일의 문구를 읽어 미리보기를 다시 그린다. 검수되지 않은 로케일을
    /// 골랐다면 <see cref="CounsellingLocale"/>이 현지어를 내보내지 않으므로,
    /// 미리보기에도 영어만 뜬다 — 종이와 같다.
    /// </summary>
    private async Task ReloadLocaleAsync()
    {
        _locale = await _localeProvider.GetLocaleAsync(SelectedLocale?.Code);
        RefreshPreview();
    }

    // ── 미리보기 ─────────────────────────────────────────────────────────────

    /// <summary>
    /// 저장 전 상태를 그대로 그린다. 폭을 지정하지 않는 것은 실제 인쇄 경로
    /// (<see cref="CounsellingService"/>)도 지정하지 않기 때문이다 — 기본값 32자(58mm)가
    /// 양쪽에서 같아야 미리보기가 종이를 보증한다.
    /// </summary>
    private void RefreshPreview()
    {
        var row = EffectivePreviewRow;

        if (row is null || !AwareGroupCodes.TryParse(row.AwareGroup, out var group))
        {
            Preview = string.Empty;
            OnPropertyChanged(nameof(HasPreview));
            return;
        }

        // 표의 한 줄은 성분 단위이고 용지는 상품 단위다. 상품명 자리에 성분과 용량을
        // 넣는 것이 이 자리에서 할 수 있는 가장 가까운 표현이고, 그래서 위에
        // 무엇을 그린 것인지(PreviewSubject) 적어 둔다.
        var name = string.IsNullOrWhiteSpace(row.Strength)
            ? row.Ingredient
            : $"{row.Ingredient} {row.Strength}";

        var document = CounsellingSheetRenderer.Render(new CounsellingSheetRequest
        {
            ProductName = name,
            GenericName = row.Ingredient,
            AwareGroup = group,
            SourceVersion = _sourceVersion,
            Locale = _locale,
            Format = SheetFormat,
            QrUrl = string.IsNullOrWhiteSpace(QrUrl) ? null : QrUrl.Trim()
        });

        Preview = string.Join(Environment.NewLine, document.Lines);
        OnPropertyChanged(nameof(HasPreview));
    }

    // ── 설정 저장 ────────────────────────────────────────────────────────────

    private void MarkChanged()
    {
        if (_isLoadingSettings)
        {
            return;
        }

        HasUnsavedChanges = true;
        SettingsMessage = string.Empty;
    }

    private async Task ExecuteSaveSettingsAsync()
    {
        // 읽어 둔 설정에서 이 화면이 다루는 세 칸만 바꿔 넘긴다. 나머지는 손대지 않는다.
        _settings.SheetFormat = SheetFormat;
        _settings.LocaleCode = SelectedLocale?.Code ?? string.Empty;
        _settings.QrUrl = QrUrl?.Trim() ?? string.Empty;

        var result = await _settingsService.SaveAsync(_settings, _actingRole);

        SettingsMessage = result.Message;

        if (result.IsSuccess)
        {
            HasUnsavedChanges = false;
        }
    }

    // ── 조회 ─────────────────────────────────────────────────────────────────

    protected override async Task OnReportLoadedAsync(ReportData data)
    {
        // 비중의 분모. 표에 실린 줄들의 수량 합이라 화면의 비중을 다 더하면 100%가 된다.
        var totalQuantity = data.Antibiotics.Sum(r => r.Quantity);

        Antibiotics.Clear();
        foreach (var row in data.Antibiotics)
        {
            row.TotalQuantityInPeriod = totalQuantity;
            Antibiotics.Add(row);
        }

        GroupShares.Clear();
        foreach (var share in data.GroupShares)
        {
            GroupShares.Add(share);
        }

        BuildTrendBars(data.AntibioticTrend);

        // 고른 줄이 새 기간에 없으면 선택을 버린다. 남겨 두면 표에 없는 성분의
        // 안내지가 미리보기에 떠 있게 된다.
        if (_selectedAntibiotic is not null
            && !Antibiotics.Any(r => r.Ingredient == _selectedAntibiotic.Ingredient
                                     && r.Strength == _selectedAntibiotic.Strength))
        {
            _selectedAntibiotic = null;
            OnPropertyChanged(nameof(SelectedAntibiotic));
        }

        await LoadMetricsAsync(data.Range);

        RefreshPreview();
    }

    /// <summary>
    /// 건너뜀의 이유별 건수. 리포트 집계가 알려 주지 않는 값이라 로그를 따로 센다.
    /// 실패해도 화면은 그려야 한다 — 이유를 모른다고 표와 그래프를 못 보여줄 이유가 없다.
    /// </summary>
    private async Task LoadMetricsAsync(ReportRange range)
    {
        try
        {
            // ToUtc는 종료일 자정 직전(23:59:59.999)이고 집계 쿼리는 `< to`로 자르므로,
            // 그 1밀리초를 포함시키려면 1을 더해야 한다.
            _metrics = await _logRepository.GetMetricsAsync(range.FromUtc, range.ToUtc + 1);
        }
        catch (Exception)
        {
            _metrics = null;
        }

        OnPropertyChanged(nameof(PharmacistSkippedDisplay));
        OnPropertyChanged(nameof(PrintFailedDisplay));
        OnPropertyChanged(nameof(UnmatchedDisplay));
        OnPropertyChanged(nameof(HasPrintFailures));
        OnPropertyChanged(nameof(HasUnmatched));
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

    protected override void OnReportPropertiesChanged()
    {
        OnPropertyChanged(nameof(HasAntibioticData));
        OnPropertyChanged(nameof(TrendPeriodLabel));
        OnPropertyChanged(nameof(HasTrendData));
        OnPropertyChanged(nameof(TrendMaxDisplay));
        OnPropertyChanged(nameof(PrintRateDisplay));
        OnPropertyChanged(nameof(CounsellingSummary));
        OnPropertyChanged(nameof(PreviewSubject));
    }

    protected override string EmptyPeriodMessage(ReportData data) =>
        data.Antibiotics.Count == 0 ? $"No antibiotic sales in {data.Range.Label}." : string.Empty;

    // ── 내보내기 ─────────────────────────────────────────────────────────────

    /// <summary>
    /// 항생제 파일만 쓴다.
    ///
    /// <b>이 파일에는 금액이 한 줄도 들어가지 않는다.</b> AMR 연구에 의무 제출되어
    /// 약국 밖으로 나가는 파일이고, 매출액이 함께 실려 나가는 것을 알아차린 약국은
    /// 제출 자체를 꺼리게 된다. 그 규칙은 주석이 아니라 테스트가 지켜야 하므로
    /// 파일을 만드는 코드는 Application 쪽(<see cref="AntibioticExportCsv"/>)에 있다.
    ///
    /// 성분별 매출액은 매출 분석 화면의 파일에 있다 — 그 파일은 약국이 갖는다.
    /// </summary>
    protected override void ExecuteExport()
    {
        Message = string.Empty;

        if (Report is null || Antibiotics.Count == 0)
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
            $"report_antibiotics_{AppVersion.FileTag}_{Report.Range.From:yyyyMMdd}_{Report.Range.To:yyyyMMdd}.csv";
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
            File.WriteAllText(
                path, AntibioticExportCsv.Build(Report, _settings.ResearchSiteCode), Encoding.UTF8);
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

        // 사이트 코드가 없으면 받는 쪽이 어느 약국 것인지 알 수 없다.
        // 내보내기를 막지는 않되(자기 확인용으로 뽑을 수 있다), 제출 전에 알려 준다.
        if (string.IsNullOrWhiteSpace(_settings.ResearchSiteCode))
        {
            Message += " No research site code is set, so this file cannot be attributed."
                       + " Set it in Antibiotic Counselling settings before submitting it.";
        }
    }
}
