using System.Collections.ObjectModel;
using System.Windows;
using PharmaPOS.Application.Counselling;
using PharmaPOS.Application.Inventory;
using PharmaPOS.Application.Repositories;
using PharmaPOS.Domain.Entities;
using PharmaPOS.Domain.Enums;
using Lightweight_Digital_Inventory_Management___POS_System.ViewModels.Base;

namespace Lightweight_Digital_Inventory_Management___POS_System.ViewModels;

/// <summary>
/// POS 판매 화면(SCR-POS-005)의 ViewModel.
/// </summary>
public partial class PosSaleViewModel : ViewModelBase
{
    private readonly IProductRepository _productRepository;
    private readonly IInventoryRepository _inventoryRepository;
    private readonly ISaleService _saleService;
    private readonly IReceiptPrintingService _receiptPrintingService;
    private readonly ICounsellingService _counsellingService;
    private readonly string _facilityId;
    private readonly string _userId;
    private readonly bool _isAdministrator;

    private string _searchTerm = string.Empty;
    private Product? _selectedProduct;
    private InventoryBatchOption? _selectedBatch;
    private string _quantity = "1";
    private string _unitPrice = string.Empty;
    private string _message = string.Empty;

    public ObservableCollection<Product> SearchResults { get; } = new();
    public ObservableCollection<InventoryBatchOption> Batches { get; } = new();
    public ObservableCollection<SaleLineItem> Cart { get; } = new();

    public string SearchTerm
    {
        get => _searchTerm;
        set => SetProperty(ref _searchTerm, value);
    }

    public Product? SelectedProduct
    {
        get => _selectedProduct;
        set
        {
            if (SetProperty(ref _selectedProduct, value))
            {
                _ = LoadBatchesAsync();
            }
        }
    }

    public InventoryBatchOption? SelectedBatch
    {
        get => _selectedBatch;
        set
        {
            if (SetProperty(ref _selectedBatch, value))
            {
                // 배치가 바뀌면(또는 처음 선택되면) 판매가를 Product Master 값으로 재설정한다.
                UnitPrice = SelectedProduct?.SellingPrice.ToString() ?? string.Empty;
            }
        }
    }

    public string Quantity
    {
        get => _quantity;
        set => SetProperty(ref _quantity, value);
    }

    public string UnitPrice
    {
        get => _unitPrice;
        set => SetProperty(ref _unitPrice, value);
    }

    /// <summary>
    /// Screen 판매가 편집 권한 정책(제품 오너 결정): Administrator만 판매가를 수정할 수 있고,
    /// Facility Staff는 읽기 전용으로 Product Master 값을 그대로 본다.
    /// </summary>
    public bool CanEditUnitPrice => _isAdministrator;

    public string Message
    {
        get => _message;
        set => SetProperty(ref _message, value);
    }

    public RelayCommand SearchCommand { get; }
    public RelayCommand AddToCartCommand { get; }
    public RelayCommand RemoveFromCartCommand { get; }

    public PosSaleViewModel(
        IProductRepository productRepository,
        IInventoryRepository inventoryRepository,
        ISaleService saleService,
        IReceiptPrintingService receiptPrintingService,
        ICounsellingService counsellingService,
        string facilityId,
        string userId,
        UserRole currentUserRole)
    {
        _productRepository = productRepository;
        _inventoryRepository = inventoryRepository;
        _saleService = saleService;
        _receiptPrintingService = receiptPrintingService;
        _counsellingService = counsellingService;
        _facilityId = facilityId;
        _userId = userId;
        _isAdministrator = currentUserRole == UserRole.Administrator;

        SearchCommand = new RelayCommand(async _ => await ExecuteSearchAsync());
        AddToCartCommand = new RelayCommand(_ => ExecuteAddToCart());
        RemoveFromCartCommand = new RelayCommand(item => ExecuteRemoveFromCart(item as SaleLineItem));

        InitializePaymentCommands();
    }

    /// <summary>
    /// F-04: USB HID 스캐너는 Enter 키를 전송하는 키보드로 동작하므로,
    /// 이 트리거만으로 스캐너 입력도 자동으로 처리된다.
    /// </summary>
    public async Task ExecuteSearchAsync()
    {
        Message = string.Empty;

        if (string.IsNullOrWhiteSpace(SearchTerm))
        {
            Message = "Please scan a barcode or enter a product name.";
            return;
        }

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

    private async Task LoadBatchesAsync()
    {
        Batches.Clear();
        SelectedBatch = null;

        if (SelectedProduct is null)
        {
            return;
        }

        var allBatches = await _inventoryRepository.GetBatchesForProductAsync(SelectedProduct.ProductId, _facilityId);

        foreach (var batch in allBatches)
        {
            Batches.Add(batch);
        }

        if (Batches.Count == 0)
        {
            Message = "No available stock for this product.";
            return;
        }

        // Screen §3.3절 기본 배치 선택 정책:
        // 1. current_quantity > 0
        // 2. expiry_date가 오늘 이후
        // 3. expiry_date가 가장 빠른 배치 우선
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        var defaultBatch = Batches
            .Where(b => b.CurrentQuantity > 0 && b.ExpiryDate > now)
            .OrderBy(b => b.ExpiryDate)
            .FirstOrDefault();

        SelectedBatch = defaultBatch;
    }

    private void ExecuteAddToCart()
    {
        Message = string.Empty;

        if (SelectedProduct is null)
        {
            Message = "Product not found.";
            return;
        }

        if (SelectedBatch is null)
        {
            Message = "Please select a batch number.";
            return;
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (SelectedBatch.ExpiryDate <= now)
        {
            Message = "This batch is expired and cannot be sold.";
            return;
        }

        if (string.IsNullOrWhiteSpace(Quantity))
        {
            Message = "Please enter the quantity.";
            return;
        }

        if (!int.TryParse(Quantity, out var quantity))
        {
            Message = "Quantity must be a whole number.";
            return;
        }

        if (quantity <= 0)
        {
            Message = "Quantity must be greater than zero.";
            return;
        }

        if (!decimal.TryParse(UnitPrice, out var unitPrice))
        {
            Message = "Selling price must be greater than zero.";
            return;
        }

        // 이미 장바구니에 같은 상품+배치가 있으면, 새 항목을 추가하는 대신 수량을 합산한다.
        // (Screen §5절 "상품 중복 추가" 예외 처리)
        var existingLine = Cart.FirstOrDefault(
            c => c.ProductId == SelectedProduct.ProductId && c.BatchNumber == SelectedBatch.BatchNumber);

        var totalRequestedQuantity = quantity + (existingLine?.Quantity ?? 0);

        if (totalRequestedQuantity > SelectedBatch.CurrentQuantity)
        {
            Message = "Stock-out quantity cannot exceed current inventory quantity.";
            return;
        }

        if (existingLine is not null)
        {
            existingLine.Quantity = totalRequestedQuantity;
            // ObservableCollection은 항목 내부 속성 변경까지는 자동 통지하지 않으므로,
            // DataGrid 등의 화면 갱신을 위해 컬렉션에서 제거 후 다시 추가한다.
            var index = Cart.IndexOf(existingLine);
            Cart.RemoveAt(index);
            Cart.Insert(index, existingLine);
        }
        else
        {
            Cart.Add(new SaleLineItem
            {
                ProductId = SelectedProduct.ProductId,
                ProductName = SelectedProduct.ProductName,
                // 항생제 복약안내 매칭에 쓴다. 판매 확정 뒤 상품을 다시 조회하지 않도록
                // 장바구니에 담을 때 함께 실어 둔다.
                GenericName = SelectedProduct.GenericName,
                AtcCode = SelectedProduct.AtcCode,
                InventoryId = SelectedBatch.InventoryId,
                BatchNumber = SelectedBatch.BatchNumber,
                ExpiryDate = SelectedBatch.ExpiryDate,
                Quantity = quantity,
                UnitPrice = unitPrice,
                CostPrice = SelectedProduct.CostPrice
            });
        }

        RaiseTotalsChanged();

        // 다음 상품을 바로 스캔할 수 있도록 검색 관련 입력을 초기화한다.
        SearchTerm = string.Empty;
        SearchResults.Clear();
        SelectedProduct = null;
        Batches.Clear();
        SelectedBatch = null;
        Quantity = "1";
        UnitPrice = string.Empty;
    }

    private void ExecuteRemoveFromCart(SaleLineItem? item)
    {
        if (item is null)
        {
            return;
        }

        Cart.Remove(item);
        RaiseTotalsChanged();
    }
}