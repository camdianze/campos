using System.Collections.ObjectModel;
using PharmaPOS.Application.Counselling;
using PharmaPOS.Application.Inventory;
using PharmaPOS.Application.Products;
using PharmaPOS.Application.Repositories;
using PharmaPOS.Domain.Entities;
using PharmaPOS.Domain.Enums;
using Lightweight_Digital_Inventory_Management___POS_System.ViewModels.Base;

namespace Lightweight_Digital_Inventory_Management___POS_System.ViewModels;

/// <summary>
/// 화면 전용 상태 필터 옵션. Domain의 EntityStatus에는 "All"이라는 개념이 없으므로
/// (그건 순수 데이터 상태가 아니라 화면 UI의 편의 옵션이다) 여기서만 정의한다.
/// </summary>
public enum ProductStatusFilterOption
{
    All,
    Active,
    Inactive
}

/// <summary>
/// 상품 목록 화면(SCR-PROD-011)의 ViewModel.
/// </summary>
public class ProductListViewModel : ViewModelBase
{
    private readonly IProductRepository _productRepository;
    private readonly IProductService _productService;
    private readonly IAntibioticMatchingService _matchingService;
    private readonly IStockInService _stockInService;
    private readonly string _facilityId;
    private readonly string _userId;

    /// <summary>
    /// 성분/ATC 조합별 판별 결과 캐시.
    /// 검색어를 한 글자 칠 때마다 목록을 다시 불러오는데, 그때마다 상품 수만큼
    /// 참조 테이블을 조회하면 타이핑이 느려진다.
    /// </summary>
    private readonly Dictionary<string, AwareGroup?> _awareGroupCache = new(StringComparer.OrdinalIgnoreCase);

    private string _searchTerm = string.Empty;
    private ProductStatusFilterOption _selectedStatusFilter = ProductStatusFilterOption.All;
    private ProductRow? _selectedRow;
    private string _message = string.Empty;

    private bool _isStockInPanelVisible;
    private string _batchNumber = string.Empty;
    private DateTime _expiryDate = DateTime.Today.AddYears(1);
    private DateTime _stockInDate = DateTime.Today;
    private string _stockInQuantity = string.Empty;
    private string _stockInMessage = string.Empty;

    public ObservableCollection<ProductRow> Products { get; } = new();

    public string SearchTerm
    {
        get => _searchTerm;
        set
        {
            if (SetProperty(ref _searchTerm, value))
            {
                _ = ReloadAsync();
            }
        }
    }

    public ProductStatusFilterOption SelectedStatusFilter
    {
        get => _selectedStatusFilter;
        set
        {
            if (SetProperty(ref _selectedStatusFilter, value))
            {
                _ = ReloadAsync();
            }
        }
    }

    public IReadOnlyList<ProductStatusFilterOption> AvailableStatusFilters { get; } =
        Enum.GetValues<ProductStatusFilterOption>();

    /// <summary>DataGrid가 선택하는 것은 줄(ProductRow)이다.</summary>
    public ProductRow? SelectedRow
    {
        get => _selectedRow;
        set
        {
            if (!SetProperty(ref _selectedRow, value))
            {
                return;
            }

            OnPropertyChanged(nameof(SelectedProduct));
            OnPropertyChanged(nameof(IsBoxedProductSelected));
            OnPropertyChanged(nameof(StockInQuantityLabel));
            OnPropertyChanged(nameof(StockInTotalPreview));

            if (value is not null)
            {
                // 상품을 고르면 아래 입고 패널이 열린다.
                // 다른 상품으로 옮길 때 입력값은 비운다 — A 상품에 적어둔 배치번호가
                // 그대로 남아 B 상품에 저장되면 재고가 엉뚱한 배치로 들어간다.
                ResetStockInForm();
                IsStockInPanelVisible = true;
            }
        }
    }

    public Product? SelectedProduct => _selectedRow?.Product;

    // ── 입고(Stock-IN) 패널 ──────────────────────────────────────────────────

    /// <summary>상품을 선택하기 전에는 접혀 있다.</summary>
    public bool IsStockInPanelVisible
    {
        get => _isStockInPanelVisible;
        set => SetProperty(ref _isStockInPanelVisible, value);
    }

    public string BatchNumber
    {
        get => _batchNumber;
        set => SetProperty(ref _batchNumber, value);
    }

    public DateTime ExpiryDate
    {
        get => _expiryDate;
        set => SetProperty(ref _expiryDate, value);
    }

    /// <summary>입고 날짜. 기본값은 오늘 (Screen §3절 "Date — 자동값").</summary>
    public DateTime StockInDate
    {
        get => _stockInDate;
        set => SetProperty(ref _stockInDate, value);
    }

    /// <summary>
    /// 입고 수량. 박스/낱개 상품이면 이 값은 "박스 개수"다 — 입고는 늘 박스째 들어오므로
    /// 낱개 총량을 암산해서 적게 하지 않는다.
    /// </summary>
    public string StockInQuantity
    {
        get => _stockInQuantity;
        set
        {
            if (SetProperty(ref _stockInQuantity, value))
            {
                OnPropertyChanged(nameof(StockInTotalPreview));
            }
        }
    }

    /// <summary>선택한 상품이 박스/낱개를 나눠 파는 상품인지.</summary>
    public bool IsBoxedProductSelected => SelectedProduct?.IsBoxedProduct == true;

    /// <summary>박스 상품이면 수량 입력칸이 무엇을 뜻하는지 라벨로 분명히 해 둔다.</summary>
    public string StockInQuantityLabel => IsBoxedProductSelected ? "Boxes" : "Quantity";

    /// <summary>"10 boxes × 30 = 300 units" — 저장 전에 실제로 얼마가 들어가는지 보여준다.</summary>
    public string StockInTotalPreview
    {
        get
        {
            if (SelectedProduct is not { } product || !product.IsBoxedProduct)
            {
                return string.Empty;
            }

            if (!int.TryParse(StockInQuantity, out var boxes) || boxes <= 0)
            {
                return $"{product.UnitsPerBox} units per box.";
            }

            return $"{boxes} box(es) × {product.UnitsPerBox} = {boxes * product.UnitsPerBox} units.";
        }
    }

    public string StockInMessage
    {
        get => _stockInMessage;
        set => SetProperty(ref _stockInMessage, value);
    }

    public RelayCommand SaveStockInCommand { get; }
    public RelayCommand CancelStockInCommand { get; }

    public string Message
    {
        get => _message;
        set => SetProperty(ref _message, value);
    }

    public RelayCommand AddProductCommand { get; }
    public RelayCommand EditCommand { get; }
    public RelayCommand DeactivateCommand { get; }
    public RelayCommand PrintInternalBarcodeCommand { get; }

    /// <summary>View가 구독해서 실제 화면 전환을 처리하는 이벤트들.</summary>
    public event Action? NavigateToAddProduct;
    public event Action<Product>? NavigateToEditProduct;
    public event Action<Product>? NavigateToPrintBarcode;

    public ProductListViewModel(
        IProductRepository productRepository,
        IProductService productService,
        IAntibioticMatchingService matchingService,
        IStockInService stockInService,
        string facilityId,
        string userId)
    {
        _productRepository = productRepository;
        _productService = productService;
        _matchingService = matchingService;
        _stockInService = stockInService;
        _facilityId = facilityId;
        _userId = userId;

        SaveStockInCommand = new RelayCommand(async _ => await ExecuteSaveStockInAsync());
        CancelStockInCommand = new RelayCommand(_ => ExecuteCancelStockIn());

        AddProductCommand = new RelayCommand(_ => NavigateToAddProduct?.Invoke());

        EditCommand = new RelayCommand(_ =>
        {
            if (SelectedProduct is null)
            {
                Message = "Please select a product.";
                return;
            }

            NavigateToEditProduct?.Invoke(SelectedProduct);
        });

        DeactivateCommand = new RelayCommand(async _ => await ExecuteDeactivateAsync());

        PrintInternalBarcodeCommand = new RelayCommand(_ =>
        {
            if (SelectedProduct is null)
            {
                Message = "Please select a product.";
                return;
            }

            NavigateToPrintBarcode?.Invoke(SelectedProduct);
        });

        _ = ReloadAsync();
    }

    /// <summary>
    /// 목록을 다시 불러온다. code-behind(View)가 화면 복귀 시 호출할 수 있도록 public으로 노출한다.
    /// </summary>
    public async Task ReloadAsync()
    {
        EntityStatus? statusFilter = SelectedStatusFilter switch
        {
            ProductStatusFilterOption.Active => EntityStatus.Active,
            ProductStatusFilterOption.Inactive => EntityStatus.Inactive,
            _ => null
        };

        IReadOnlyList<Product> results;

        try
        {
            results = await _productRepository.SearchAsync(SearchTerm, statusFilter);
        }
        catch (Exception)
        {
            Message = "Product list could not be loaded.";
            return;
        }

        Products.Clear();
        foreach (var product in results)
        {
            Products.Add(new ProductRow
            {
                Product = product,
                AwareGroup = await ResolveAwareGroupAsync(product)
            });
        }

        Message = results.Count == 0 ? "No products found." : string.Empty;
    }

    /// <summary>
    /// 상품이 어느 AWaRe 그룹으로 판별되는지 알아낸다.
    /// 판매 시점과 똑같은 매칭 서비스를 쓴다 — 목록에 보이는 색과 실제로 인쇄될 분류가
    /// 어긋나면 확인용으로 쓸 수가 없다.
    /// </summary>
    private async Task<AwareGroup?> ResolveAwareGroupAsync(Product product)
    {
        var key = $"{product.AtcCode}|{product.GenericName}";

        if (_awareGroupCache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var match = await _matchingService.MatchAsync(product.AtcCode, product.GenericName);

        // 국소 제제로 제외된 것도 분류 자체는 있으므로 색을 보여준다.
        // "안내지가 나가지 않는다"와 "항생제가 아니다"는 다른 이야기다.
        var group = match.Classification?.AwareGroup;

        _awareGroupCache[key] = group;
        return group;
    }

    /// <summary>
    /// 입고 저장. 검증과 저장은 StockInService가 하고, 여기서는 숫자 변환만 먼저 거른다
    /// (기존 입고 화면과 같은 흐름).
    /// </summary>
    private async Task ExecuteSaveStockInAsync()
    {
        StockInMessage = string.Empty;

        if (SelectedProduct is null)
        {
            StockInMessage = "Please select a product.";
            return;
        }

        if (!int.TryParse(StockInQuantity, out var quantity))
        {
            StockInMessage = IsBoxedProductSelected
                ? "Box quantity must be a whole number."
                : "Quantity must be a whole number.";
            return;
        }

        // 박스 상품이면 여기서 넘기는 quantity는 박스 개수다 (StockInService가 환산한다).
        var product = SelectedProduct;

        var result = await _stockInService.SaveStockInAsync(
            _facilityId, product.ProductId, _userId,
            BatchNumber, ExpiryDate, StockInDate, quantity);

        if (!result.IsSuccess)
        {
            StockInMessage = result.Message!;
            return;
        }

        // 저장에 성공하면 패널을 접는다. 열어둔 채로 두면 같은 값이 남아 있어
        // 실수로 한 번 더 저장하기 쉽다.
        Message = product.IsBoxedProduct
            ? $"Stock-in saved for {product.ProductName}: {quantity} box(es), {quantity * product.UnitsPerBox} units."
            : $"Stock-in saved for {product.ProductName}.";

        ExecuteCancelStockIn();
    }

    private void ExecuteCancelStockIn()
    {
        ResetStockInForm();
        IsStockInPanelVisible = false;
    }

    private void ResetStockInForm()
    {
        BatchNumber = string.Empty;
        ExpiryDate = DateTime.Today.AddYears(1);
        StockInDate = DateTime.Today;
        StockInQuantity = string.Empty;
        StockInMessage = string.Empty;
    }

    private async Task ExecuteDeactivateAsync()
    {
        if (SelectedProduct is null)
        {
            Message = "Please select a product.";
            return;
        }

        var result = await _productService.DeactivateProductAsync(SelectedProduct.ProductId);

        if (result.IsSuccess)
        {
            await ReloadAsync();
        }
        else
        {
            Message = result.Message!;
        }
    }
}