using System.Collections.ObjectModel;
using Microsoft.Win32;
using Lightweight_Digital_Inventory_Management___POS_System.ViewModels.Base;
using PharmaPOS.Application.Counselling;
using PharmaPOS.Application.Repositories;

namespace Lightweight_Digital_Inventory_Management___POS_System.ViewModels;

/// <summary>
/// 항생제 복약안내(AMR) 설정 화면의 ViewModel.
///
/// 설정 자체보다 중요한 것이 참조 데이터 설치 상태 표시다.
/// 시드 파일이 없으면 기능이 조용히 아무것도 안 하게 되는데,
/// 그 사실을 여기서 확인할 수 없으면 "고장 났는지 원래 그런지"를 알 수 없다.
/// </summary>
public class CounsellingSettingsViewModel : ViewModelBase
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

    private CounsellingPrintMode _printMode = CounsellingPrintMode.Always;
    private CounsellingSheetFormat _sheetFormat = CounsellingSheetFormat.Full;
    private CounsellingOutput _output = CounsellingOutput.Printer;
    private string _fileOutputFolder = string.Empty;
    private LocaleOption? _selectedLocale;
    private string _qrUrl = string.Empty;
    private string _researchSiteCode = string.Empty;
    private string _referenceDataStatus = "Checking…";
    private string _metricsSummary = string.Empty;
    private string _message = string.Empty;

    public ObservableCollection<LocaleOption> AvailableLocales { get; } = new();

    public IReadOnlyList<CounsellingPrintMode> AvailablePrintModes { get; } =
        Enum.GetValues<CounsellingPrintMode>();

    public IReadOnlyList<CounsellingSheetFormat> AvailableSheetFormats { get; } =
        Enum.GetValues<CounsellingSheetFormat>();

    public CounsellingPrintMode PrintMode
    {
        get => _printMode;
        set => SetProperty(ref _printMode, value);
    }

    public CounsellingSheetFormat SheetFormat
    {
        get => _sheetFormat;
        set => SetProperty(ref _sheetFormat, value);
    }

    public IReadOnlyList<CounsellingOutput> AvailableOutputs { get; } =
        Enum.GetValues<CounsellingOutput>();

    /// <summary>
    /// 프린터 없이 용지 내용을 확인해야 할 때 File을 고른다.
    /// 프린터 드라이버가 변수로 끼는 상황에서 렌더링 결과 자체를 보려는 용도다.
    /// </summary>
    public CounsellingOutput Output
    {
        get => _output;
        set
        {
            if (SetProperty(ref _output, value))
            {
                OnPropertyChanged(nameof(IsFileOutput));
            }
        }
    }

    public bool IsFileOutput => Output == CounsellingOutput.File;

    public string FileOutputFolder
    {
        get => _fileOutputFolder;
        set => SetProperty(ref _fileOutputFolder, value);
    }

    public LocaleOption? SelectedLocale
    {
        get => _selectedLocale;
        set => SetProperty(ref _selectedLocale, value);
    }

    public string QrUrl
    {
        get => _qrUrl;
        set => SetProperty(ref _qrUrl, value);
    }

    /// <summary>
    /// 연구기관이 등록 때 부여한 사이트 코드.
    ///
    /// 항생제 내보내기 파일에서 이 약국을 가리키는 유일한 값이다. 코드 자체는
    /// 아무것도 드러내지 않고, 코드와 약국의 대응표는 연구기관만 갖는다.
    /// 약국이 자기 코드를 화면에서 볼 수 있어야 무엇이 나가는지 알 수 있다.
    /// </summary>
    public string ResearchSiteCode
    {
        get => _researchSiteCode;
        set => SetProperty(ref _researchSiteCode, value);
    }

    public string ReferenceDataStatus
    {
        get => _referenceDataStatus;
        private set => SetProperty(ref _referenceDataStatus, value);
    }

    public string MetricsSummary
    {
        get => _metricsSummary;
        private set => SetProperty(ref _metricsSummary, value);
    }

    public string Message
    {
        get => _message;
        set => SetProperty(ref _message, value);
    }

    public RelayCommand SaveCommand { get; }
    public RelayCommand BackCommand { get; }
    public RelayCommand BrowseFolderCommand { get; }

    public event Action? NavigateBack;

    public CounsellingSettingsViewModel(
        ICounsellingSettingsService settingsService,
        ICounsellingLocaleProvider localeProvider,
        ICounsellingLogRepository logRepository)
    {
        _settingsService = settingsService;
        _localeProvider = localeProvider;
        _logRepository = logRepository;

        SaveCommand = new RelayCommand(async _ => await ExecuteSaveAsync());
        BackCommand = new RelayCommand(_ => NavigateBack?.Invoke());
        BrowseFolderCommand = new RelayCommand(_ => ExecuteBrowseFolder());
    }

    public async Task LoadAsync()
    {
        var settings = await _settingsService.GetAsync();

        PrintMode = settings.PrintMode;
        SheetFormat = settings.SheetFormat;
        Output = settings.Output;
        FileOutputFolder = settings.FileOutputFolder;
        QrUrl = settings.QrUrl;
        ResearchSiteCode = settings.ResearchSiteCode;

        await LoadLocaleOptionsAsync(settings.LocaleCode);
        await LoadReferenceDataStatusAsync();
        await LoadMetricsAsync();
    }

    private async Task LoadLocaleOptionsAsync(string? currentCode)
    {
        AvailableLocales.Clear();

        // 첫 줄은 항상 "영어 단독"이다. 영어는 고정 레이어라 끌 수 없고,
        // 현지어를 붙이지 않는 선택지가 필요하다.
        AvailableLocales.Add(new LocaleOption { Code = string.Empty, Display = "English only" });

        var locales = await _localeProvider.ListAvailableLocalesAsync();

        foreach (var locale in locales)
        {
            // 미검수 로케일도 목록에는 보여준다. 고르더라도 현지어는 인쇄되지 않으며,
            // 그 사실을 라벨에 적어 둔다 — 안 보이면 왜 안 나오는지 알 수 없다.
            var status = locale.IsApproved
                ? "approved"
                : "not reviewed - English only";

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
            AvailableLocales.FirstOrDefault(o => string.Equals(o.Code, currentCode, StringComparison.OrdinalIgnoreCase))
            ?? AvailableLocales[0];
    }

    private async Task LoadReferenceDataStatusAsync()
    {
        var (count, sourceVersion) = await _settingsService.GetReferenceDataStatusAsync();

        ReferenceDataStatus = count == 0
            ? "Not installed. Counselling sheets cannot be printed until the AWaRe reference file is added."
            : $"{count} antibiotics loaded ({sourceVersion ?? "unknown source"}).";
    }

    private async Task LoadMetricsAsync()
    {
        try
        {
            var now = DateTimeOffset.UtcNow;
            var from = now.AddDays(-30).ToUnixTimeMilliseconds();

            var metrics = await _logRepository.GetMetricsAsync(from, now.ToUnixTimeMilliseconds());

            if (metrics.AntibioticSaleLines == 0 && metrics.UnmatchedCount == 0)
            {
                MetricsSummary = "No antibiotic sales recorded in the last 30 days.";
                return;
            }

            MetricsSummary =
                $"Last 30 days - antibiotic sales: {metrics.AntibioticSaleLines} of {metrics.TotalSaleLines} " +
                $"({metrics.AntibioticShare:P0}).  " +
                $"ACCESS share: {metrics.AccessShare:P0} " +
                $"(ACCESS {metrics.AccessCount} / WATCH {metrics.WatchCount} / " +
                $"RESERVE {metrics.ReserveCount} / NOT RECOMMENDED {metrics.NotRecommendedCount}).  " +
                $"Sheets printed: {metrics.PrintedCount}, skipped: {metrics.SkippedCount} " +
                $"(print rate {metrics.PrintRate:P0}).  " +
                $"Unmatched products: {metrics.UnmatchedCount}.";
        }
        catch (Exception)
        {
            MetricsSummary = "Stewardship figures are not available.";
        }
    }

    /// <summary>
    /// 폴더 선택 대화상자를 연다. ViewModel에서 대화상자를 여는 것은
    /// BackupExportViewModel과 같은 방식이라 이 저장소의 기존 방식을 따른 것이다.
    /// </summary>
    private void ExecuteBrowseFolder()
    {
        var dialog = new OpenFolderDialog { Title = "Select Counselling Sheet Folder" };

        if (dialog.ShowDialog() == true)
        {
            FileOutputFolder = dialog.FolderName;
        }
    }

    private async Task ExecuteSaveAsync()
    {
        Message = string.Empty;

        try
        {
            await _settingsService.SaveAsync(new CounsellingSettings
            {
                PrintMode = PrintMode,
                SheetFormat = SheetFormat,
                Output = Output,
                FileOutputFolder = FileOutputFolder?.Trim() ?? string.Empty,
                LocaleCode = SelectedLocale?.Code ?? string.Empty,
                QrUrl = QrUrl?.Trim() ?? string.Empty,
                ResearchSiteCode = ResearchSiteCode?.Trim() ?? string.Empty
            });

            Message = "Settings saved.";
        }
        catch (Exception)
        {
            Message = "Settings could not be saved. Please try again.";
        }
    }
}
