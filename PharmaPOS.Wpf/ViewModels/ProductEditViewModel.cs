using System.IO;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using PharmaPOS.Application.Products;
using System.Globalization;
using PharmaPOS.Application.Receipts;
using PharmaPOS.Domain.Entities;
using PharmaPOS.Domain.Enums;
using Lightweight_Digital_Inventory_Management___POS_System.ViewModels.Base;

namespace Lightweight_Digital_Inventory_Management___POS_System.ViewModels;

/// <summary>
/// 상품 등록/수정 화면(SCR-PROD-012)의 ViewModel.
/// 신규 등록과 수정을 겸용한다 (IsNewProduct로 구분).
/// </summary>
public class ProductEditViewModel : ViewModelBase
{
    private readonly IProductService _productService;
    private readonly IProductPhotoService _photoService;

    private string _productId = string.Empty;
    private string _barcode = string.Empty;
    private string _internalBarcode = string.Empty;
    private string _productName = string.Empty;
    private string _genericName = string.Empty;
    private string _strength = string.Empty;
    private DosageForm? _dosageForm;
    private string _unit = string.Empty;
    private string _manufacturer = string.Empty;
    private string _countryOfOrigin = string.Empty;
    private string _costPrice = string.Empty;
    private string _sellingPrice = string.Empty;
    private string _safetyStockLevel = string.Empty;
    private EntityStatus _status = EntityStatus.Active;
    private string _atcCode = string.Empty;
    private ProductCategory? _category;
    private bool _isCombination;
    private bool _sellsLooseUnits;
    private string _unitsPerBox = "1";
    private string _unitSellingPrice = string.Empty;
    private string _message = string.Empty;

    public bool IsNewProduct { get; }

    public string Barcode
    {
        get => _barcode;
        set => SetProperty(ref _barcode, value);
    }

    public string InternalBarcode
    {
        get => _internalBarcode;
        set
        {
            if (SetProperty(ref _internalBarcode, value))
            {
                OnPropertyChanged(nameof(UnitBarcodePreview));
            }
        }
    }

    /// <summary>
    /// 낱개 판매(소분) 사용 여부. 이걸 켜야 아래 세 가지 — 박스당 낱개 수,
    /// 낱개 판매가, 낱개 바코드 — 가 의미를 갖는다.
    /// 저장할 때는 별도 컬럼 없이 units_per_box로 표현된다 (꺼져 있으면 1).
    /// </summary>
    public bool SellsLooseUnits
    {
        get => _sellsLooseUnits;
        set
        {
            if (!SetProperty(ref _sellsLooseUnits, value))
            {
                return;
            }

            // 켜는 순간 1이 남아 있으면 "낱개로 파는데 박스당 1개"라는 모순이 된다.
            if (value && _unitsPerBox is "1" or "")
            {
                UnitsPerBox = string.Empty;
            }

            RaiseLooseUnitHints();
        }
    }

    /// <summary>
    /// 박스당 낱개 수. 문자열로 들고 있는 이유는 화면의 다른 숫자 입력칸과 같다 —
    /// 입력 중간 상태("", "1x")를 그대로 담아 두고 저장할 때 한 번에 검사한다.
    /// </summary>
    public string UnitsPerBox
    {
        get => _unitsPerBox;
        set
        {
            if (SetProperty(ref _unitsPerBox, value))
            {
                RaiseLooseUnitHints();
            }
        }
    }

    /// <summary>낱개 하나의 판매가. 비워 두면 박스가 ÷ 박스당 개수로 계산한다.</summary>
    public string UnitSellingPrice
    {
        get => _unitSellingPrice;
        set
        {
            if (SetProperty(ref _unitSellingPrice, value))
            {
                OnPropertyChanged(nameof(UnitPriceHint));
                OnPropertyChanged(nameof(UnitSellingPriceInOtherCurrency));
            }
        }
    }

    /// <summary>
    /// 낱개용 바코드 미리보기. 내부 바코드가 아직 없는 신규 상품은 저장할 때 생기므로
    /// 그 사실을 대신 알려 준다.
    /// </summary>
    public string UnitBarcodePreview
    {
        get
        {
            if (!SellsLooseUnits)
            {
                return string.Empty;
            }

            return string.IsNullOrWhiteSpace(InternalBarcode)
                ? "Generated on save."
                : InternalBarcode + Product.UnitBarcodeSuffix;
        }
    }

    /// <summary>낱개가를 비워 뒀을 때 실제로 어떤 값이 쓰이는지 보여준다.</summary>
    public string UnitPriceHint
    {
        get
        {
            if (!SellsLooseUnits)
            {
                return string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(UnitSellingPrice))
            {
                return $"One {UnitLabel} is sold at this price.";
            }

            if (decimal.TryParse(SellingPrice, out var boxPrice)
                && int.TryParse(UnitsPerBox, out var perBox)
                && perBox > 1)
            {
                return $"Leave empty to sell one {UnitLabel} at {boxPrice / perBox} ({boxPrice} ÷ {perBox}).";
            }

            return $"Leave empty to sell one {UnitLabel} at the box price divided by the count above.";
        }
    }

    /// <summary>위쪽 원가·판매가가 어느 단위 기준인지 알려 준다. 이게 혼동의 핵심이었다.</summary>
    public string BoxPriceHint =>
        SellsLooseUnits
            ? "Cost price and selling price above are for one box."
            : $"Cost price and selling price above are for one {UnitLabel}.";

    /// <summary>
    /// 라벨에 쓸 낱개 단위 이름. Unit 칸에 적은 값(Sachet, Tablet…)을 그대로 쓴다 —
    /// "unit"이라는 말이 제형 이름과 낱개 개수 두 뜻으로 읽히던 혼동을 없애기 위해서다.
    /// </summary>
    public string UnitLabel => string.IsNullOrWhiteSpace(Unit) ? "unit" : Unit.Trim();

    private void RaiseLooseUnitHints()
    {
        OnPropertyChanged(nameof(UnitBarcodePreview));
        OnPropertyChanged(nameof(UnitPriceHint));
        OnPropertyChanged(nameof(BoxPriceHint));
        OnPropertyChanged(nameof(UnitsPerBoxLabel));
    }

    // ── 통화 ────────────────────────────────────────────────────────────────
    //
    // 캄보디아는 달러와 리엘을 함께 쓴다. 약국이 "20,000리엘짜리"로 알고 있는 상품을
    // 달러로 환산해서 적게 하면 그 자리에서 암산이 필요하고, 그 암산이 값이 된다.
    //
    // 저장되는 값은 언제나 달러다(이 앱의 모든 금액이 그렇다). 리엘은 적고 보는
    // 단위일 뿐이고, 저장할 때 환율로 되돌린다.

    /// <summary>리엘 칸을 보여줄지. 환율이 없으면 환산할 방법이 없다.</summary>
    public bool IsRielAvailable => _showRiel && _exchangeRate > 0;

    /// <summary>지금 입력 단위가 리엘인지.</summary>
    public bool IsRielInput
    {
        get => _isRielInput;
        private set
        {
            if (SetProperty(ref _isRielInput, value))
            {
                OnPropertyChanged(nameof(IsUsdInput));
                OnPropertyChanged(nameof(CurrencySuffix));
                RaisePriceHintsChanged();
            }
        }
    }

    public bool IsUsdInput => !_isRielInput;

    /// <summary>입력칸 옆에 붙는 단위 표시.</summary>
    public string CurrencySuffix => _isRielInput ? RielConverter.RielSymbol : "$";

    public string ExchangeRateNote => IsRielAvailable
        ? $"1 USD = {_exchangeRate.ToString("N0", CultureInfo.InvariantCulture)} {RielConverter.RielSymbol}"
        : string.Empty;

    public RelayCommand ShowUsdCommand { get; private set; } = null!;
    public RelayCommand ShowRielCommand { get; private set; } = null!;

    /// <summary>화면을 열 때 환율을 읽는다. 못 읽으면 달러만 쓰던 종전대로 동작한다.</summary>
    public async Task LoadCurrencySettingsAsync()
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

        OnPropertyChanged(nameof(IsRielAvailable));
        OnPropertyChanged(nameof(ExchangeRateNote));
        RaisePriceHintsChanged();
    }

    private void SwitchCurrency(bool toRiel)
    {
        if (!IsRielAvailable || toRiel == _isRielInput)
        {
            return;
        }

        SellingPrice = ConvertForDisplay(nameof(SellingPrice), SellingPrice, toRiel);
        CostPrice = ConvertForDisplay(nameof(CostPrice), CostPrice, toRiel);
        UnitSellingPrice = ConvertForDisplay(nameof(UnitSellingPrice), UnitSellingPrice, toRiel);

        IsRielInput = toRiel;
    }

    /// <summary>
    /// 한 칸의 글자를 반대 통화로 바꾼다.
    ///
    /// 손대지 않고 돌아온 경우에는 환산하지 않고 원래 글자를 그대로 되돌린다.
    /// 환산은 양쪽 모두 반올림이 걸려서, 왕복할 때마다 4.53 → 18,600 → 4.54처럼
    /// 값이 조금씩 밀린다. 사람이 고치지 않은 가격이 화면을 오갔다는 이유로
    /// 달라지면 안 된다.
    /// </summary>
    private string ConvertForDisplay(string field, string current, bool toRiel)
    {
        if (_priceBeforeSwitch.TryGetValue(field, out var remembered))
        {
            var untouched = toRiel ? remembered.Usd : remembered.Riel;

            if (string.Equals(current.Trim(), untouched, StringComparison.Ordinal))
            {
                return toRiel ? remembered.Riel : remembered.Usd;
            }
        }

        if (string.IsNullOrWhiteSpace(current) || !decimal.TryParse(current, out var value))
        {
            _priceBeforeSwitch.Remove(field);
            return current;
        }

        string usd, riel;

        if (toRiel)
        {
            usd = current.Trim();
            riel = RielConverter.ToRiel(value, _exchangeRate, _rielRounding)
                .ToString(CultureInfo.InvariantCulture);
        }
        else
        {
            riel = current.Trim();
            usd = decimal.Round(value / _exchangeRate, ProductService.LooseUnitPriceDecimals, MidpointRounding.AwayFromZero)
                .ToString(CultureInfo.InvariantCulture);
        }

        _priceBeforeSwitch[field] = (usd, riel);
        return toRiel ? riel : usd;
    }

    /// <summary>저장할 값. 리엘로 적혀 있으면 달러로 되돌린다.</summary>
    private string ToStoredPrice(string field, string text)
    {
        if (!_isRielInput || string.IsNullOrWhiteSpace(text))
        {
            return text;
        }

        if (_priceBeforeSwitch.TryGetValue(field, out var remembered)
            && string.Equals(text.Trim(), remembered.Riel, StringComparison.Ordinal))
        {
            // 화면에서 손대지 않은 값이다. 환산해서 되돌리면 원래 달러 금액이 밀린다.
            return remembered.Usd;
        }

        if (!decimal.TryParse(text, out var riel))
        {
            return text;
        }

        return decimal.Round(riel / _exchangeRate, ProductService.LooseUnitPriceDecimals, MidpointRounding.AwayFromZero)
            .ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>입력칸 아래에 반대 통화를 적는다. 두 값이 동시에 보여야 한다.</summary>
    private string OtherCurrencyOf(string text)
    {
        if (!IsRielAvailable || string.IsNullOrWhiteSpace(text) || !decimal.TryParse(text, out var value))
        {
            return string.Empty;
        }

        return _isRielInput
            ? "= $" + decimal.Round(value / _exchangeRate, ProductService.LooseUnitPriceDecimals, MidpointRounding.AwayFromZero)
                .ToString("0.00##", CultureInfo.InvariantCulture)
            : "= " + RielConverter.Format(value, _exchangeRate, _rielRounding);
    }

    public string SellingPriceInOtherCurrency => OtherCurrencyOf(SellingPrice);
    public string CostPriceInOtherCurrency => OtherCurrencyOf(CostPrice);
    public string UnitSellingPriceInOtherCurrency => OtherCurrencyOf(UnitSellingPrice);

    private void RaisePriceHintsChanged()
    {
        OnPropertyChanged(nameof(SellingPriceInOtherCurrency));
        OnPropertyChanged(nameof(CostPriceInOtherCurrency));
        OnPropertyChanged(nameof(UnitSellingPriceInOtherCurrency));
    }

    /// <summary>"Sachets Per Box"처럼 제형 이름을 넣어 준다.</summary>
    public string UnitsPerBoxLabel => $"{UnitLabel}s Per Box *";

    public string ProductName
    {
        get => _productName;
        set => SetProperty(ref _productName, value);
    }

    public string GenericName
    {
        get => _genericName;
        set => SetProperty(ref _genericName, value);
    }

    public string Strength
    {
        get => _strength;
        set => SetProperty(ref _strength, value);
    }

    /// <summary>
    /// 제형. <b>선택 입력</b>이라 비워 둬도(null) 저장된다 — 비의약품에는 제형이 없다.
    /// 아래 Unit과 다른 값이다: Unit은 낱개를 세는 이름(Bottle, Tube)이고 이건 약의 형태(Syrup, Ointment)다.
    /// </summary>
    public DosageForm? DosageForm
    {
        get => _dosageForm;
        set => SetProperty(ref _dosageForm, value);
    }

    /// <summary>빈 항목(=아직 정하지 않음)을 맨 앞에 두려고 nullable 목록으로 만든다.</summary>
    /// <summary>
    /// 판매 단위 후보. 실제로 등록돼 있던 값을 빈도순으로 옮겼다. 자유 입력만 두면
    /// "yg" 같은 오타가 들어오고, 목록만 두면 없는 단위를 못 넣는다 — 그래서 고르되
    /// 직접 칠 수도 있는 편집 가능 콤보로 쓴다.
    /// </summary>
    public IReadOnlyList<string> AvailableUnits { get; } = new[]
    {
        "Tablet", "Capsule", "Piece", "Bottle", "Vial", "Ampoule",
        "Tube", "Roll", "Sachet", "Pack", "Pair", "Box"
    };

    public IReadOnlyList<DosageForm?> AvailableDosageForms { get; } =
        new List<DosageForm?> { null }
            .Concat(Enum.GetValues<DosageForm>().Cast<DosageForm?>())
            .ToList();

    public string Unit
    {
        get => _unit;
        set
        {
            if (SetProperty(ref _unit, value))
            {
                // 제형 이름이 바뀌면 아래 라벨·안내문("Sachets Per Box" 등)도 따라 바뀐다.
                OnPropertyChanged(nameof(UnitLabel));
                RaiseLooseUnitHints();
            }
        }
    }

    public string Manufacturer
    {
        get => _manufacturer;
        set => SetProperty(ref _manufacturer, value);
    }

    public string CountryOfOrigin
    {
        get => _countryOfOrigin;
        set => SetProperty(ref _countryOfOrigin, value);
    }

    public string CostPrice
    {
        get => _costPrice;
        set
        {
            if (SetProperty(ref _costPrice, value))
            {
                OnPropertyChanged(nameof(CostPriceInOtherCurrency));
            }
        }
    }

    public string SellingPrice
    {
        get => _sellingPrice;
        set
        {
            if (SetProperty(ref _sellingPrice, value))
            {
                OnPropertyChanged(nameof(SellingPriceInOtherCurrency));
                // 박스가를 고치면 아래에 보여주는 낱개 환산가도 따라 바뀌어야 한다.
                OnPropertyChanged(nameof(UnitPriceHint));
            }
        }
    }

    public string SafetyStockLevel
    {
        get => _safetyStockLevel;
        set => SetProperty(ref _safetyStockLevel, value);
    }

    public EntityStatus Status
    {
        get => _status;
        set => SetProperty(ref _status, value);
    }

    /// <summary>
    /// WHO ATC 코드. 채워 두면 항생제 복약안내가 성분명 표기 흔들림 없이 매칭된다.
    /// 항생제가 아닌 상품은 비워 둔다.
    /// </summary>
    public string AtcCode
    {
        get => _atcCode;
        set => SetProperty(ref _atcCode, value);
    }

    /// <summary>
    /// 의약품 / 비의약품. <b>선택 입력</b>이라 비워 둬도(null) 저장된다.
    /// </summary>
    public ProductCategory? Category
    {
        get => _category;
        set => SetProperty(ref _category, value);
    }

    /// <summary>빈 항목(=아직 정하지 않음)을 맨 앞에 두려고 nullable 목록으로 만든다.</summary>
    public IReadOnlyList<ProductCategory?> AvailableCategories { get; } =
        new List<ProductCategory?> { null, ProductCategory.Medicine, ProductCategory.NonMedicine };

    /// <summary>복합제 여부. 성분이 여럿이어도 AWaRe 분류는 조합 자체의 것 하나를 따른다.</summary>
    public bool IsCombination
    {
        get => _isCombination;
        set => SetProperty(ref _isCombination, value);
    }

    public IReadOnlyList<EntityStatus> AvailableStatuses { get; } = Enum.GetValues<EntityStatus>();

    public string Message
    {
        get => _message;
        set => SetProperty(ref _message, value);
    }

    public RelayCommand SaveCommand { get; }
    public RelayCommand CancelCommand { get; }

    /// <summary>저장 성공 또는 Cancel 클릭 시 발생. View가 구독해서 목록 화면으로 전환한다.</summary>
    public event Action? NavigateBackToList;

    /// <summary>
    /// "Selling Price &lt; Cost Price" 경고 확인이 필요할 때 발생.
    /// View가 구독해서 확인 다이얼로그를 띄우고, 결과를 ConfirmLowerSellingPrice로 알려준다.
    /// </summary>
    public event Action<string>? ConfirmationRequested;

    private readonly string _userId;
    private readonly IReceiptSettingsService _receiptSettingsService;

    // 통화 설정. 가격을 리엘로 보고 적을 수 있게 하려면 환율이 있어야 한다.
    private decimal _exchangeRate;
    private int _rielRounding = 100;
    private bool _showRiel;
    private bool _isRielInput;

    // 달러로 적혀 있던 원래 글자. 리엘로 바꿨다가 손대지 않고 돌아오면 이 값을 그대로
    // 되돌린다 — 매번 환산해서 되돌리면 4.53이 4.54가 되는 식으로 조금씩 밀린다.
    private readonly Dictionary<string, (string Usd, string Riel)> _priceBeforeSwitch = new();

    public ProductEditViewModel(
        IProductService productService,
        IProductPhotoService photoService,
        IReceiptSettingsService receiptSettingsService,
        Product? existingProduct,
        string userId)
    {
        _productService = productService;
        _photoService = photoService;
        _receiptSettingsService = receiptSettingsService;
        _userId = userId;

        IsNewProduct = existingProduct is null;

        if (existingProduct is not null)
        {
            _productId = existingProduct.ProductId;
            _barcode = existingProduct.Barcode ?? string.Empty;
            _internalBarcode = existingProduct.InternalBarcode ?? string.Empty;
            _productName = existingProduct.ProductName;
            _genericName = existingProduct.GenericName ?? string.Empty;
            _strength = existingProduct.Strength ?? string.Empty;
            _dosageForm = existingProduct.DosageForm;
            _unit = existingProduct.Unit;
            _manufacturer = existingProduct.Manufacturer ?? string.Empty;
            _countryOfOrigin = existingProduct.CountryOfOrigin ?? string.Empty;
            _costPrice = existingProduct.CostPrice.ToString();
            _sellingPrice = existingProduct.SellingPrice.ToString();
            _safetyStockLevel = existingProduct.SafetyStockLevel.ToString();
            _status = existingProduct.Status;
            _atcCode = existingProduct.AtcCode ?? string.Empty;
            _category = existingProduct.Category;
            _isCombination = existingProduct.IsCombination;
            // 낱개 판매 여부는 별도 컬럼이 아니라 박스당 개수로 표현된다.
            _sellsLooseUnits = existingProduct.IsBoxedProduct;
            _unitsPerBox = existingProduct.UnitsPerBox.ToString();
            _unitSellingPrice = existingProduct.UnitSellingPrice?.ToString() ?? string.Empty;
        }

        ShowUsdCommand = new RelayCommand(_ => SwitchCurrency(toRiel: false));
        ShowRielCommand = new RelayCommand(_ => SwitchCurrency(toRiel: true));

        SaveCommand = new RelayCommand(async _ => await ExecuteSaveAsync(acknowledgeWarning: false));
        CancelCommand = new RelayCommand(_ => NavigateBackToList?.Invoke());

        SetPhotoCommand = new RelayCommand(async _ => await ExecuteSetPhotoAsync(), _ => CanEditPhoto);
        RemovePhotoCommand = new RelayCommand(async _ => await ExecuteRemovePhotoAsync(), _ => CanEditPhoto && HasPhoto);

        _ = LoadCurrencySettingsAsync();
    }

    /// <summary>
    /// View가 확인 다이얼로그에서 "예"를 받았을 때 호출한다.
    /// </summary>
    public async void ConfirmLowerSellingPrice()
    {
        await ExecuteSaveAsync(acknowledgeWarning: true);
    }

    private async Task ExecuteSaveAsync(bool acknowledgeWarning)
    {
        Message = string.Empty;

        // 가격/재고 숫자 파싱. Screen §4.3절 필수값 검증은 서비스가 담당하지만,
        // 애초에 숫자로 변환이 안 되는 입력(문자 등)은 화면 단에서 먼저 걸러준다.
        // 원가는 선택이라 비워 두면 0이다. 숫자가 아닌 글자가 들어온 경우만 막는다.
        // 리엘로 적혀 있으면 달러로 되돌린다. 저장되는 금액은 언제나 달러다.
        var costPriceText = ToStoredPrice(nameof(CostPrice), CostPrice);
        var sellingPriceText = ToStoredPrice(nameof(SellingPrice), SellingPrice);
        var unitSellingPriceText = ToStoredPrice(nameof(UnitSellingPrice), UnitSellingPrice);

        var costPrice = 0m;
        if (!string.IsNullOrWhiteSpace(costPriceText) && !decimal.TryParse(costPriceText, out costPrice))
        {
            Message = "Cost price must be a number.";
            return;
        }

        if (!decimal.TryParse(sellingPriceText, out var sellingPrice))
        {
            Message = "Selling price must be greater than zero.";
            return;
        }

        // 안전재고도 선택이다. 비우면 0 — 부족 알림이 뜨지 않을 뿐이다.
        var safetyStockLevel = 0;
        if (!string.IsNullOrWhiteSpace(SafetyStockLevel) && !int.TryParse(SafetyStockLevel, out safetyStockLevel))
        {
            Message = "Safety stock level must be a whole number.";
            return;
        }

        // 낱개 판매를 끄면 박스/낱개 구분이 없는 상품이다 — 박스당 1개, 낱개가 없음.
        var unitsPerBox = 1;
        decimal? unitSellingPrice = null;

        if (SellsLooseUnits)
        {
            if (!int.TryParse(UnitsPerBox, out unitsPerBox) || unitsPerBox < 2)
            {
                Message = $"Enter how many {UnitLabel}s are in one box (2 or more).";
                return;
            }

            // 비워 두는 것과 잘못 적은 것은 다르게 다뤄야 한다. 비었으면 "박스가에서 계산"이고,
            // 숫자가 아니면 입력 실수라 조용히 넘어가면 안 된다.
            if (!string.IsNullOrWhiteSpace(unitSellingPriceText))
            {
                if (!decimal.TryParse(unitSellingPriceText, out var parsedUnitPrice))
                {
                    Message = "Loose unit price must be a number.";
                    return;
                }

                unitSellingPrice = parsedUnitPrice;
            }
        }

        var product = new Product
        {
            ProductId = _productId,
            Barcode = string.IsNullOrWhiteSpace(Barcode) ? null : Barcode,
            InternalBarcode = string.IsNullOrWhiteSpace(InternalBarcode) ? null : InternalBarcode,
            ProductName = ProductName,
            GenericName = string.IsNullOrWhiteSpace(GenericName) ? null : GenericName,
            Strength = string.IsNullOrWhiteSpace(Strength) ? null : Strength,
            DosageForm = DosageForm,
            Unit = Unit,
            Manufacturer = string.IsNullOrWhiteSpace(Manufacturer) ? null : Manufacturer,
            CountryOfOrigin = string.IsNullOrWhiteSpace(CountryOfOrigin) ? null : CountryOfOrigin,
            CostPrice = costPrice,
            SellingPrice = sellingPrice,
            SafetyStockLevel = safetyStockLevel,
            Status = Status,
            AtcCode = string.IsNullOrWhiteSpace(AtcCode) ? null : AtcCode.Trim().ToUpperInvariant(),
            Category = Category,
            IsCombination = IsCombination,
            UnitsPerBox = unitsPerBox,
            UnitSellingPrice = unitSellingPrice,
            CreatedAt = 0 // 신규 등록 시 서비스가 채운다. 수정 시에는 DB의 기존 값이 UPDATE 대상에서 그대로 유지된다.
        };

        var result = await _productService.SaveProductAsync(product, IsNewProduct, _userId, acknowledgeWarning);

        if (result.IsSuccess)
        {
            NavigateBackToList?.Invoke();
        }
        else if (result.RequiresConfirmation)
        {
            ConfirmationRequested?.Invoke(result.Message!);
        }
        else
        {
            Message = result.Message!;
        }
    }

    // ── 사진 ────────────────────────────────────────────────────────────────
    //
    // 사진이 이 화면에 함께 있는 이유: 상세 보기와 수정 화면을 따로 두면 같은 입력 폼이
    // 두 벌이 되고, 검증 규칙이 갈라지면서 한쪽만 고치는 일이 생긴다.
    // 목록에서 Edit으로 들어오든 우클릭으로 들어오든 같은 화면이다.

    private BitmapImage? _photo;
    private string _photoUpdatedText = string.Empty;
    private bool _isPhotoBusy;

    /// <summary>사진. 없으면 null이고 화면은 빈 자리로 둔다.</summary>
    public BitmapImage? Photo
    {
        get => _photo;
        private set
        {
            if (SetProperty(ref _photo, value))
            {
                OnPropertyChanged(nameof(HasPhoto));
                RemovePhotoCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool HasPhoto => _photo is not null;

    /// <summary>사진 아래에 붙는 갱신 시각. 사진이 없으면 빈 문자열이다.</summary>
    public string PhotoUpdatedText
    {
        get => _photoUpdatedText;
        private set => SetProperty(ref _photoUpdatedText, value);
    }

    /// <summary>
    /// 신규 등록 중에는 사진을 붙일 수 없다. 사진은 상품 ID에 매달아 저장하는데
    /// 그 ID가 저장하는 순간에 생기기 때문이다. 저장한 뒤 다시 열어서 넣는다.
    /// </summary>
    public bool CanEditPhoto => !IsNewProduct && !_isPhotoBusy;

    /// <summary>사진 칸에 띄우는 안내. 신규 등록일 때 왜 못 넣는지 알려 준다.</summary>
    public string PhotoHint => IsNewProduct
        ? "Save the product first, then reopen it to add a photo."
        : string.Empty;

    public RelayCommand SetPhotoCommand { get; }
    public RelayCommand RemovePhotoCommand { get; }

    /// <summary>사진은 화면을 띄운 뒤에 읽는다. 목록에서 들어오는 길이 사진 때문에 느려지면 안 된다.</summary>
    public async Task LoadPhotoAsync()
    {
        if (IsNewProduct)
        {
            return;
        }

        ApplyPhoto(await _photoService.GetAsync(_productId));
    }

    private async Task ExecuteSetPhotoAsync()
    {
        Message = string.Empty;

        var dialog = new OpenFileDialog
        {
            Title = "Select product photo",
            Filter = "Images (*.jpg;*.jpeg;*.png;*.bmp)|*.jpg;*.jpeg;*.png;*.bmp|All files (*.*)|*.*",
            CheckFileExists = true
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        SetPhotoBusy(true);

        try
        {
            byte[] fileBytes;

            try
            {
                fileBytes = await File.ReadAllBytesAsync(dialog.FileName);
            }
            catch (Exception)
            {
                Message = "The selected file could not be read.";
                return;
            }

            var result = await _photoService.SaveAsync(_productId, fileBytes);

            if (!result.IsSuccess)
            {
                Message = result.Message!;
                return;
            }

            ApplyPhoto(result.Photo);
        }
        finally
        {
            SetPhotoBusy(false);
        }
    }

    private async Task ExecuteRemovePhotoAsync()
    {
        Message = string.Empty;
        SetPhotoBusy(true);

        try
        {
            var result = await _photoService.RemoveAsync(_productId);

            if (!result.IsSuccess)
            {
                Message = result.Message!;
                return;
            }

            ApplyPhoto(null);
        }
        finally
        {
            SetPhotoBusy(false);
        }
    }

    private void SetPhotoBusy(bool isBusy)
    {
        _isPhotoBusy = isBusy;
        OnPropertyChanged(nameof(CanEditPhoto));
        SetPhotoCommand.RaiseCanExecuteChanged();
        RemovePhotoCommand.RaiseCanExecuteChanged();
    }

    private void ApplyPhoto(ProductPhoto? photo)
    {
        if (photo is null)
        {
            Photo = null;
            PhotoUpdatedText = string.Empty;
            return;
        }

        Photo = ToBitmap(photo.Bytes);

        // 시각이 0이면 언제 넣었는지 알 수 없는 사진이다(컬럼이 생기기 전에 넣었거나 직접 손댄 DB).
        // 그때 1970년을 찍으면 틀린 정보가 되므로 날짜 줄을 아예 붙이지 않는다.
        PhotoUpdatedText = photo.UpdatedAt <= 0
            ? string.Empty
            : "Photo updated " +
              DateTimeOffset.FromUnixTimeMilliseconds(photo.UpdatedAt).ToLocalTime().ToString("yyyy-MM-dd");
    }

    /// <summary>
    /// 바이트를 화면에 쓸 수 있는 이미지로. Freeze해 두면 스트림을 붙들고 있지 않아
    /// 사진을 바꿔도 파일이 잠기지 않는다.
    /// </summary>
    private static BitmapImage? ToBitmap(byte[] bytes)
    {
        try
        {
            using var stream = new MemoryStream(bytes);

            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();

            return image;
        }
        catch (Exception)
        {
            // 저장된 바이트가 깨졌더라도 화면은 열려야 한다.
            return null;
        }
    }
}