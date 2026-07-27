using System.Collections.ObjectModel;
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

    private string _searchTerm = string.Empty;
    private ProductStatusFilterOption _selectedStatusFilter = ProductStatusFilterOption.All;
    private Product? _selectedProduct;
    private string _message = string.Empty;

    public ObservableCollection<Product> Products { get; } = new();

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

    public Product? SelectedProduct
    {
        get => _selectedProduct;
        set => SetProperty(ref _selectedProduct, value);
    }

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

    public ProductListViewModel(IProductRepository productRepository, IProductService productService)
    {
        _productRepository = productRepository;
        _productService = productService;

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
            Products.Add(product);
        }

        Message = results.Count == 0 ? "No products found." : string.Empty;
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