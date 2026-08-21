using System.Globalization;
using System.Runtime.CompilerServices;
using Lightweight_Digital_Inventory_Management___POS_System.ViewModels.Base;
using PharmaPOS.Application.Counselling;
using PharmaPOS.Application.Inventory;
using PharmaPOS.Application.Receipts;
using PharmaPOS.Application.Settings;
using PharmaPOS.Domain.Enums;

namespace Lightweight_Digital_Inventory_Management___POS_System.ViewModels;

/// <summary>
/// 관리자 대시보드의 영수증 설정 구역 ViewModel.
///
/// 왼쪽 입력이 바뀔 때마다 오른쪽 미리보기를 다시 그린다. 미리보기는 실제 인쇄와
/// 같은 ReceiptRenderer를 쓴다 — 미리보기를 따로 그리면 화면과 종이가 갈라지고,
/// 그 차이는 종이가 나온 뒤에야 드러난다.
///
/// 저장 권한은 화면이 아니라 IReceiptSettingsService가 판단한다. 여기서 버튼을
/// 감추는 것은 편의일 뿐이고, 실제 거절은 서비스가 한다.
/// </summary>
public class ReceiptSettingsViewModel : ViewModelBase
{
    /// <summary>
    /// 미리보기에 쓰는 가짜 판매. 약품명은 번역 대상이 아니므로 영어 표기 그대로 둔다.
    /// 실제 재고를 읽지 않는 이유: 설정 화면이 상품 데이터에 기대면 재고가 빈
    /// 첫 실행에서 미리보기가 비어 버린다.
    /// </summary>
    private static readonly IReadOnlyList<SaleLineItem> SampleLines = new[]
    {
        SampleLine("Paracetamol 500mg", 20, 0.05m),
        SampleLine("Amoxicillin 250mg", 15, 0.12m),
        SampleLine("ORS Sachet", 5, 0.30m),
        SampleLine("Zinc Sulfate 20mg", 10, 0.08m)
    };

    private readonly IReceiptSettingsService _settingsService;
    private readonly ICounsellingLocaleProvider _localeProvider;
    private readonly UserRole _currentUserRole;
    private readonly string _currentUserId;

    private CounsellingLocale _locale = CounsellingLocale.EnglishOnly;
    private Dictionary<string, string> _fieldErrors = new();

    private string _shopNameKm = string.Empty;
    private string _shopNameEn = string.Empty;
    private string _shopAddressKm = string.Empty;
    private string _shopAddressEn = string.Empty;
    private string _shopTel = string.Empty;
    private ReceiptPrintLanguage _printLanguage = ReceiptPrintLanguage.KhmerAndEnglish;
    private ReceiptPaperWidth _paperWidth = ReceiptPaperWidth.Mm80;
    private bool _showRiel = true;
    private string _exchangeRate = string.Empty;
    private int _rielRounding = 100;
    private bool _showReceiptNumber = true;
    private bool _showStaffName = true;
    private bool _showUnitPrice = true;
    private bool _showUnitLabel = true;
    private string _receiptPrefix = string.Empty;
    private ReceiptNumberResetCycle _resetCycle = ReceiptNumberResetCycle.Daily;
    private string _footerKm = string.Empty;
    private string _footerEn = string.Empty;
    private bool _vatEnabled;
    private string _vatTin = string.Empty;
    private string _vatRate = string.Empty;

    private bool _isDirty;
    private string _preview = string.Empty;
    private string _message = string.Empty;

    public ReceiptSettingsViewModel(
        IReceiptSettingsService settingsService,
        ICounsellingLocaleProvider localeProvider,
        UserRole currentUserRole,
        string currentUserId)
    {
        _settingsService = settingsService;
        _localeProvider = localeProvider;
        _currentUserRole = currentUserRole;
        _currentUserId = currentUserId;

        SaveCommand = new RelayCommand(async _ => await ExecuteSaveAsync());
        RevertCommand = new RelayCommand(async _ => await LoadAsync());
    }

    // ── 약국 정보 ─────────────────────────────────────────────────────────

    public string ShopNameKm
    {
        get => _shopNameKm;
        set => SetSetting(ref _shopNameKm, value, AppSettingKeys.ShopNameKm);
    }

    public string ShopNameEn
    {
        get => _shopNameEn;
        set => SetSetting(ref _shopNameEn, value, AppSettingKeys.ShopNameEn);
    }

    public string ShopAddressKm
    {
        get => _shopAddressKm;
        set => SetSetting(ref _shopAddressKm, value, AppSettingKeys.ShopAddressKm);
    }

    public string ShopAddressEn
    {
        get => _shopAddressEn;
        set => SetSetting(ref _shopAddressEn, value, AppSettingKeys.ShopAddressEn);
    }

    public string ShopTel
    {
        get => _shopTel;
        set => SetSetting(ref _shopTel, value, AppSettingKeys.ShopTel);
    }

    public string ShopNameKmError => ErrorFor(AppSettingKeys.ShopNameKm);
    public string ShopNameEnError => ErrorFor(AppSettingKeys.ShopNameEn);

    // ── 언어와 용지 ───────────────────────────────────────────────────────

    public IReadOnlyList<ReceiptPrintLanguage> AvailableLanguages { get; } =
        Enum.GetValues<ReceiptPrintLanguage>();

    public IReadOnlyList<ReceiptPaperWidth> AvailableWidths { get; } =
        Enum.GetValues<ReceiptPaperWidth>();

    public ReceiptPrintLanguage PrintLanguage
    {
        get => _printLanguage;
        set => SetSetting(ref _printLanguage, value, AppSettingKeys.PrintLanguage);
    }

    public ReceiptPaperWidth PaperWidth
    {
        get => _paperWidth;
        set => SetSetting(ref _paperWidth, value, AppSettingKeys.PrintWidth);
    }

    /// <summary>
    /// 로케일 파일이 검수 전이면 크메르어는 한 글자도 인쇄되지 않는다.
    /// 그 사실을 여기서 알려주지 않으면 "왜 영어만 나오지"로 남는다.
    /// </summary>
    public string LocaleStatus => _locale.IsApproved
        ? $"Khmer text comes from locales/{_locale.LocaleCode}.json (approved)."
        : "The Khmer translation has not been reviewed yet, so receipts print in English only. "
          + "Approve locales/km-KH.json to switch it on.";

    // ── 통화 ──────────────────────────────────────────────────────────────

    public bool ShowRiel
    {
        get => _showRiel;
        set
        {
            if (SetSetting(ref _showRiel, value, AppSettingKeys.CurrencyShowRiel))
            {
                OnPropertyChanged(nameof(IsRielVisible));
            }
        }
    }

    /// <summary>리엘 표기를 끄면 환율·반올림 입력을 감춘다.</summary>
    public bool IsRielVisible => ShowRiel;

    public string ExchangeRate
    {
        get => _exchangeRate;
        set => SetSetting(ref _exchangeRate, value, AppSettingKeys.CurrencyRate);
    }

    public string ExchangeRateError => ErrorFor(AppSettingKeys.CurrencyRate);

    public IReadOnlyList<int> AvailableRoundings { get; } = new[] { 100, 500, 0 };

    public int RielRounding
    {
        get => _rielRounding;
        set => SetSetting(ref _rielRounding, value, AppSettingKeys.CurrencyRounding);
    }

    // ── 표시 항목 ─────────────────────────────────────────────────────────

    public bool ShowReceiptNumber
    {
        get => _showReceiptNumber;
        set => SetSetting(ref _showReceiptNumber, value, AppSettingKeys.ReceiptShowNo);
    }

    public bool ShowStaffName
    {
        get => _showStaffName;
        set => SetSetting(ref _showStaffName, value, AppSettingKeys.ReceiptShowStaff);
    }

    public bool ShowUnitPrice
    {
        get => _showUnitPrice;
        set => SetSetting(ref _showUnitPrice, value, AppSettingKeys.ReceiptShowPrice);
    }

    public bool ShowUnitLabel
    {
        get => _showUnitLabel;
        set => SetSetting(ref _showUnitLabel, value, AppSettingKeys.ReceiptShowUnit);
    }

    // ── 번호 체계와 맺음 문구 ─────────────────────────────────────────────

    public string ReceiptPrefix
    {
        get => _receiptPrefix;
        set => SetSetting(ref _receiptPrefix, value, AppSettingKeys.ReceiptPrefix);
    }

    public string ReceiptPrefixError => ErrorFor(AppSettingKeys.ReceiptPrefix);

    public IReadOnlyList<ReceiptNumberResetCycle> AvailableResetCycles { get; } =
        Enum.GetValues<ReceiptNumberResetCycle>();

    public ReceiptNumberResetCycle ResetCycle
    {
        get => _resetCycle;
        set => SetSetting(ref _resetCycle, value, AppSettingKeys.ReceiptResetCycle);
    }

    public string FooterKm
    {
        get => _footerKm;
        set => SetSetting(ref _footerKm, value, AppSettingKeys.ReceiptFooterKm);
    }

    public string FooterEn
    {
        get => _footerEn;
        set => SetSetting(ref _footerEn, value, AppSettingKeys.ReceiptFooterEn);
    }

    // ── 세무 ──────────────────────────────────────────────────────────────

    public bool VatEnabled
    {
        get => _vatEnabled;
        set
        {
            if (SetSetting(ref _vatEnabled, value, AppSettingKeys.VatEnabled))
            {
                OnPropertyChanged(nameof(IsVatVisible));
            }
        }
    }

    /// <summary>부가세 표기를 끄면 등록번호·세율 입력을 감춘다.</summary>
    public bool IsVatVisible => VatEnabled;

    public string VatTin
    {
        get => _vatTin;
        set => SetSetting(ref _vatTin, value, AppSettingKeys.VatTin);
    }

    public string VatTinError => ErrorFor(AppSettingKeys.VatTin);

    public string VatRate
    {
        get => _vatRate;
        set => SetSetting(ref _vatRate, value, AppSettingKeys.VatRate);
    }

    public string VatRateError => ErrorFor(AppSettingKeys.VatRate);

    // ── 상태 ──────────────────────────────────────────────────────────────

    /// <summary>저장하지 않은 변경이 있는지. 화면을 벗어나기 전에 이 값을 확인한다.</summary>
    public bool IsDirty
    {
        get => _isDirty;
        private set
        {
            if (SetProperty(ref _isDirty, value))
            {
                OnPropertyChanged(nameof(DirtyStatus));
            }
        }
    }

    public string DirtyStatus => IsDirty ? "Unsaved changes" : "No changes";

    /// <summary>미리보기에 그릴 영수증 전문.</summary>
    public string Preview
    {
        get => _preview;
        private set => SetProperty(ref _preview, value);
    }

    public string Message
    {
        get => _message;
        private set => SetProperty(ref _message, value);
    }

    /// <summary>관리자가 아니면 입력을 잠근다. 실제 거절은 저장 시 서비스가 한다.</summary>
    public bool CanEdit => _currentUserRole == UserRole.Administrator;

    public RelayCommand SaveCommand { get; }
    public RelayCommand RevertCommand { get; }

    // ── 적재 / 저장 ───────────────────────────────────────────────────────

    public async Task LoadAsync()
    {
        _locale = await _localeProvider.GetLocaleAsync("km-KH");

        var settings = await _settingsService.GetAsync();

        _shopNameKm = settings.ShopNameKm;
        _shopNameEn = settings.ShopNameEn;
        _shopAddressKm = settings.ShopAddressKm;
        _shopAddressEn = settings.ShopAddressEn;
        _shopTel = settings.ShopTel;
        _printLanguage = settings.PrintLanguage;
        _paperWidth = settings.PaperWidth;
        _showRiel = settings.ShowRiel;
        _exchangeRate = settings.ExchangeRate.ToString("0.####", CultureInfo.InvariantCulture);
        _rielRounding = settings.RielRounding;
        _showReceiptNumber = settings.ShowReceiptNumber;
        _showStaffName = settings.ShowStaffName;
        _showUnitPrice = settings.ShowUnitPrice;
        _showUnitLabel = settings.ShowUnitLabel;
        _receiptPrefix = settings.ReceiptPrefix;
        _resetCycle = settings.ResetCycle;
        _footerKm = settings.FooterKm;
        _footerEn = settings.FooterEn;
        _vatEnabled = settings.VatEnabled;
        _vatTin = settings.VatTin;
        _vatRate = settings.VatRate.ToString("0.####", CultureInfo.InvariantCulture);

        _fieldErrors = new Dictionary<string, string>();
        Message = string.Empty;
        IsDirty = false;

        // 필드를 직접 채웠으므로 화면에 전부 다시 읽으라고 알린다.
        OnPropertyChanged(string.Empty);

        RefreshPreview();
    }

    private async Task ExecuteSaveAsync()
    {
        Message = string.Empty;

        var result = await _settingsService.SaveAsync(BuildSettings(), _currentUserRole, _currentUserId);

        SetFieldErrors(result.FieldErrors);

        if (!result.IsSuccess)
        {
            // 항목별 오류는 각 입력 옆에 이미 붙었다. 아래 줄은 항목에 매이지 않는
            // 실패(권한 없음, 저장 실패)만 말한다.
            Message = result.Message ?? string.Empty;
            return;
        }

        Message = result.Message ?? string.Empty;
        IsDirty = false;
    }

    /// <summary>화면의 현재 값으로 설정 객체를 만든다. 저장과 미리보기가 같은 것을 쓴다.</summary>
    private ReceiptSettings BuildSettings() => new()
    {
        ShopNameKm = ShopNameKm,
        ShopNameEn = ShopNameEn,
        ShopAddressKm = ShopAddressKm,
        ShopAddressEn = ShopAddressEn,
        ShopTel = ShopTel,
        PrintLanguage = PrintLanguage,
        PaperWidth = PaperWidth,
        ShowRiel = ShowRiel,
        ExchangeRate = ParseDecimal(ExchangeRate),
        RielRounding = RielRounding,
        ShowReceiptNumber = ShowReceiptNumber,
        ShowStaffName = ShowStaffName,
        ShowUnitPrice = ShowUnitPrice,
        ShowUnitLabel = ShowUnitLabel,
        ReceiptPrefix = ReceiptPrefix,
        ResetCycle = ResetCycle,
        FooterKm = FooterKm,
        FooterEn = FooterEn,
        VatEnabled = VatEnabled,
        VatTin = VatTin,
        VatRate = ParseDecimal(VatRate)
    };

    /// <summary>
    /// 숫자로 못 읽히면 -1로 둔다. 0으로 두면 "입력 안 함"과 "0을 넣음"이 같아져
    /// 검증이 잘못된 문구를 내보낸다.
    /// </summary>
    private static decimal ParseDecimal(string value) =>
        decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : -1m;

    // ── 미리보기 ──────────────────────────────────────────────────────────

    /// <summary>
    /// 저장 전 상태를 그대로 그린다. 실제 인쇄와 같은 렌더러를 쓰되,
    /// 번호만은 발번하지 않고 예시를 넣는다 — 미리보기를 열 때마다 일련번호가
    /// 올라가면 실제 영수증 번호에 구멍이 생긴다.
    /// </summary>
    private void RefreshPreview()
    {
        var settings = BuildSettings();

        // 환율이 비어 있거나 잘못돼도 미리보기는 그려야 한다. 값을 못 읽으면
        // 리엘 줄만 빠지고 나머지 배치는 그대로 보인다.
        if (settings.ExchangeRate < 0)
        {
            settings.ExchangeRate = 0;
        }

        if (settings.VatRate < 0)
        {
            settings.VatRate = 0;
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        var document = ReceiptRenderer.Render(new ReceiptRenderRequest
        {
            Settings = settings,
            Text = new ReceiptText(
                settings.PrintLanguage,
                settings.PrintLanguage == ReceiptPrintLanguage.English
                    ? CounsellingLocale.EnglishOnly
                    : _locale),
            Lines = SampleLines,
            TotalAmount = SampleLines.Sum(l => l.LineTotal),
            TransactionTime = now,
            ReceiptNumber = SamplePreviewNumber(settings, now),
            StaffName = "Sample Staff",
            PaymentMethod = PaymentMethod.Cash.ToString()
        });

        Preview = string.Join(Environment.NewLine, document.Lines);
    }

    private static string SamplePreviewNumber(ReceiptSettings settings, long now)
    {
        var prefix = string.IsNullOrWhiteSpace(settings.ReceiptPrefix)
            ? "…"
            : settings.ReceiptPrefix.Trim().ToUpperInvariant();

        var date = PhnomPenhClock.ToLocal(now).ToString("yyyyMMdd", CultureInfo.InvariantCulture);

        return prefix + "-" + date + "-0001";
    }

    private static SaleLineItem SampleLine(string name, int quantity, decimal unitPrice) => new()
    {
        ProductId = "sample",
        ProductName = name,
        InventoryId = "sample",
        BatchNumber = "sample",
        ExpiryDate = 0,
        Quantity = quantity,
        UnitPrice = unitPrice,
        CostPrice = 0m
    };

    // ── 잔심부름 ──────────────────────────────────────────────────────────

    /// <summary>
    /// 설정 한 칸이 바뀌었을 때 할 일을 한곳에 모은다: 미저장 표시를 켜고,
    /// 그 칸에 붙어 있던 오류 문구를 지우고, 미리보기를 다시 그린다.
    /// </summary>
    private bool SetSetting<T>(
        ref T field, T value, string settingKey, [CallerMemberName] string? propertyName = null)
    {
        if (!SetProperty(ref field, value, propertyName))
        {
            return false;
        }

        IsDirty = true;

        if (_fieldErrors.Remove(settingKey))
        {
            OnPropertyChanged(ErrorPropertyName(settingKey));
        }

        RefreshPreview();

        return true;
    }

    private void SetFieldErrors(IReadOnlyDictionary<string, string> errors)
    {
        var affected = new HashSet<string>(_fieldErrors.Keys);
        affected.UnionWith(errors.Keys);

        _fieldErrors = new Dictionary<string, string>(errors);

        foreach (var key in affected)
        {
            OnPropertyChanged(ErrorPropertyName(key));
        }
    }

    private string ErrorFor(string settingKey) =>
        _fieldErrors.TryGetValue(settingKey, out var message) ? message : string.Empty;

    /// <summary>설정 키 → 화면이 묶여 있는 오류 속성 이름.</summary>
    private static string ErrorPropertyName(string settingKey) => settingKey switch
    {
        AppSettingKeys.ShopNameKm => nameof(ShopNameKmError),
        AppSettingKeys.ShopNameEn => nameof(ShopNameEnError),
        AppSettingKeys.ReceiptPrefix => nameof(ReceiptPrefixError),
        AppSettingKeys.CurrencyRate => nameof(ExchangeRateError),
        AppSettingKeys.VatTin => nameof(VatTinError),
        AppSettingKeys.VatRate => nameof(VatRateError),
        _ => string.Empty
    };
}
