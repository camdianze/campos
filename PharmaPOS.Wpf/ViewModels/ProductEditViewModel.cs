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
    private string _unit = string.Empty;
    private string _manufacturer = string.Empty;
    private string _countryOfOrigin = string.Empty;
    private string _costPrice = string.Empty;
    private string _sellingPrice = string.Empty;
    private string _safetyStockLevel = string.Empty;
    private EntityStatus _status = EntityStatus.Active;
    private string _atcCode = string.Empty;
    private bool _isCombination;
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
        set => SetProperty(ref _internalBarcode, value);
    }

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

    public string Unit
    {
        get => _unit;
        set => SetProperty(ref _unit, value);
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
        set => SetProperty(ref _sellingPrice, value);
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
            _unit = existingProduct.Unit;
            _manufacturer = existingProduct.Manufacturer ?? string.Empty;
            _countryOfOrigin = existingProduct.CountryOfOrigin ?? string.Empty;
            _costPrice = existingProduct.CostPrice.ToString();
            _sellingPrice = existingProduct.SellingPrice.ToString();
            _safetyStockLevel = existingProduct.SafetyStockLevel.ToString();
            _status = existingProduct.Status;
            _atcCode = existingProduct.AtcCode ?? string.Empty;
            _isCombination = existingProduct.IsCombination;
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

        var product = new Product
        {
            ProductId = _productId,
            Barcode = string.IsNullOrWhiteSpace(Barcode) ? null : Barcode,
            InternalBarcode = string.IsNullOrWhiteSpace(InternalBarcode) ? null : InternalBarcode,
            ProductName = ProductName,
            GenericName = string.IsNullOrWhiteSpace(GenericName) ? null : GenericName,
            Strength = string.IsNullOrWhiteSpace(Strength) ? null : Strength,
            Unit = Unit,
            Manufacturer = string.IsNullOrWhiteSpace(Manufacturer) ? null : Manufacturer,
            CountryOfOrigin = string.IsNullOrWhiteSpace(CountryOfOrigin) ? null : CountryOfOrigin,
            CostPrice = costPrice,
            SellingPrice = sellingPrice,
            SafetyStockLevel = safetyStockLevel,
            Status = Status,
            AtcCode = string.IsNullOrWhiteSpace(AtcCode) ? null : AtcCode.Trim().ToUpperInvariant(),
            IsCombination = IsCombination,
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