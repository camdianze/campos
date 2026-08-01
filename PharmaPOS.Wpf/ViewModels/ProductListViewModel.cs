using System.Collections.ObjectModel;
using PharmaPOS.Application.Counselling;
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
            if (SetProperty(ref _selectedRow, value))
            {
                OnPropertyChanged(nameof(SelectedProduct));
            }
        }
    }

    public Product? SelectedProduct => _selectedRow?.Product;

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
        IAntibioticMatchingService matchingService)
    {
        _productRepository = productRepository;
        _productService = productService;
        _matchingService = matchingService;

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