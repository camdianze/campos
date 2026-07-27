using System.Collections.ObjectModel;
using PharmaPOS.Application.Inventory;
using PharmaPOS.Application.Repositories;
using PharmaPOS.Domain.Entities;
using PharmaPOS.Domain.Enums;
using Lightweight_Digital_Inventory_Management___POS_System.ViewModels.Base;

namespace Lightweight_Digital_Inventory_Management___POS_System.ViewModels;

/// <summary>
/// 입고 등록 화면(SCR-STOCKIN-009)의 ViewModel.
/// </summary>
public class StockInViewModel : ViewModelBase
{
    private readonly IProductRepository _productRepository;
    private readonly IStockInService _stockInService;
    private readonly string _facilityId;
    private readonly string _userId;

    private string _searchTerm = string.Empty;
    private Product? _selectedProduct;
    private string _batchNumber = string.Empty;
    private DateTime _expiryDate = DateTime.Today.AddYears(1);
    private DateTime _stockInDate = DateTime.Today;
    private string _quantity = string.Empty;
    private string _message = string.Empty;

    public ObservableCollection<Product> SearchResults { get; } = new();

    public string SearchTerm
    {
        get => _searchTerm;
        set => SetProperty(ref _searchTerm, value);
    }

    public Product? SelectedProduct
    {
        get => _selectedProduct;
        set => SetProperty(ref _selectedProduct, value);
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

    /// <summary>기본값: 오늘 날짜 (Screen §3절 "Date — 자동값").</summary>
    public DateTime StockInDate
    {
        get => _stockInDate;
        set => SetProperty(ref _stockInDate, value);
    }

    public string Quantity
    {
        get => _quantity;
        set => SetProperty(ref _quantity, value);
    }

    public string Message
    {
        get => _message;
        set => SetProperty(ref _message, value);
    }

    public RelayCommand SearchCommand { get; }
    public RelayCommand SaveCommand { get; }
    public RelayCommand CancelCommand { get; }

    public event Action? NavigateBack;

    public StockInViewModel(
        IProductRepository productRepository,
        IStockInService stockInService,
        string facilityId,
        string userId)
    {
        _productRepository = productRepository;
        _stockInService = stockInService;
        _facilityId = facilityId;
        _userId = userId;

        SearchCommand = new RelayCommand(async _ => await ExecuteSearchAsync());
        SaveCommand = new RelayCommand(async _ => await ExecuteSaveAsync());
        CancelCommand = new RelayCommand(_ => NavigateBack?.Invoke());
    }

    /// <summary>
    /// 검색창에서 Enter 입력 시 호출된다 (F-04: USB HID 스캐너는 Enter 키를 전송하는
    /// 키보드로 동작하므로, 이 트리거만으로 스캐너 입력도 자동으로 처리된다).
    /// </summary>
    public async Task ExecuteSearchAsync()
    {
        Message = string.Empty;

        var results = await _productRepository.SearchAsync(SearchTerm, EntityStatus.Active);

        SearchResults.Clear();
        foreach (var product in results)
        {
            SearchResults.Add(product);
        }

        if (results.Count == 0)
        {
            Message = "Product not found.";
        }
    }

    private async Task ExecuteSaveAsync()
    {
        Message = string.Empty;

        if (SelectedProduct is null)
        {
            Message = "Please select a product.";
            return;
        }

        if (!int.TryParse(Quantity, out var quantity))
        {
            Message = "Quantity must be a whole number.";
            return;
        }

        var result = await _stockInService.SaveStockInAsync(
            _facilityId, SelectedProduct.ProductId, _userId,
            BatchNumber, ExpiryDate, StockInDate, quantity);

        if (result.IsSuccess)
        {
            ResetForm();
            Message = "Stock-in saved successfully.";
        }
        else
        {
            Message = result.Message!;
        }
    }

    private void ResetForm()
    {
        SearchTerm = string.Empty;
        SearchResults.Clear();
        SelectedProduct = null;
        BatchNumber = string.Empty;
        ExpiryDate = DateTime.Today.AddYears(1);
        StockInDate = DateTime.Today;
        Quantity = string.Empty;
    }
}