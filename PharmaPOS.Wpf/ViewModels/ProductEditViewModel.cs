using PharmaPOS.Application.Products;
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
        set => SetProperty(ref _costPrice, value);
    }

    public string SellingPrice
    {
        get => _sellingPrice;
        set
        {
            if (SetProperty(ref _sellingPrice, value))
            {
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

    public ProductEditViewModel(IProductService productService, Product? existingProduct)
    {
        _productService = productService;

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

        SaveCommand = new RelayCommand(async _ => await ExecuteSaveAsync(acknowledgeWarning: false));
        CancelCommand = new RelayCommand(_ => NavigateBackToList?.Invoke());
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
        if (!decimal.TryParse(CostPrice, out var costPrice))
        {
            Message = "Cost price must be greater than zero.";
            return;
        }

        if (!decimal.TryParse(SellingPrice, out var sellingPrice))
        {
            Message = "Selling price must be greater than zero.";
            return;
        }

        if (!int.TryParse(SafetyStockLevel, out var safetyStockLevel))
        {
            Message = "Safety stock level cannot be negative.";
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
            if (!string.IsNullOrWhiteSpace(UnitSellingPrice))
            {
                if (!decimal.TryParse(UnitSellingPrice, out var parsedUnitPrice))
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

        var result = await _productService.SaveProductAsync(product, IsNewProduct, acknowledgeWarning);

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
}