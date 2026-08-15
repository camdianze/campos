using System.Collections.ObjectModel;
using System.Windows;
using PharmaPOS.Application.Counselling;
using PharmaPOS.Application.Inventory;
using PharmaPOS.Application.Repositories;
using PharmaPOS.Domain.Entities;
using PharmaPOS.Domain.Enums;
using Lightweight_Digital_Inventory_Management___POS_System.ViewModels.Base;
using Lightweight_Digital_Inventory_Management___POS_System.Views;

// 엔티티 이름(Inventory)이 Application의 네임스페이스와 같아 그냥 쓰면 네임스페이스로 읽힌다.
using InventoryEntity = PharmaPOS.Domain.Entities.Inventory;

namespace Lightweight_Digital_Inventory_Management___POS_System.ViewModels;

/// <summary>
/// 한 줄을 박스로 파는지 낱개로 파는지. 화면 전용 선택값이라 Domain에는 두지 않는다.
/// 박스/낱개 구분이 없는 상품(units_per_box = 1)은 언제나 Each로 취급한다.
/// </summary>
public enum SaleUnitOption
{
    Box,
    Each
}

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
    private SaleUnitOption _selectedSaleUnit = SaleUnitOption.Box;

    /// <summary>
    /// 방금 스캔한 바코드가 가리킨 판매 단위. 검색과 상품 선택은 별개의 동작이라
    /// (결과 목록에서 골라야 상품이 정해진다) 그 사이를 이 값으로 잇는다.
    /// </summary>
    private SaleUnitOption _scannedSaleUnit = SaleUnitOption.Box;

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
            if (!SetProperty(ref _selectedProduct, value))
            {
                return;
            }

            // 낱개용 바코드(-EA)를 찍었으면 그 판매 단위를 그대로 이어받는다.
            // 이름으로 찾았거나 박스/낱개 구분이 없는 상품이면 각각 박스·낱개가 기본이다.
            _selectedSaleUnit = value?.IsBoxedProduct == true
                ? _scannedSaleUnit
                : SaleUnitOption.Each;

            OnPropertyChanged(nameof(SelectedSaleUnit));
            OnPropertyChanged(nameof(IsBoxedProductSelected));
            OnPropertyChanged(nameof(QuantityLabel));

            _ = LoadBatchesAsync();
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
                ResetUnitPriceFromProduct();
            }
        }
    }

    /// <summary>
    /// 박스로 팔지 낱개로 팔지. 바코드를 찍으면 자동으로 정해지지만,
    /// 이름으로 찾은 경우엔 손으로 바꿀 수 있어야 한다.
    /// </summary>
    public SaleUnitOption SelectedSaleUnit
    {
        get => _selectedSaleUnit;
        set
        {
            if (SetProperty(ref _selectedSaleUnit, value))
            {
                OnPropertyChanged(nameof(QuantityLabel));
                // 단위가 바뀌면 가격도 그 단위 가격으로 다시 잡아 준다.
                ResetUnitPriceFromProduct();
            }
        }
    }

    public IReadOnlyList<SaleUnitOption> AvailableSaleUnits { get; } = Enum.GetValues<SaleUnitOption>();

    /// <summary>박스/낱개 선택칸을 보여줄지. 구분이 없는 상품에는 고를 것이 없다.</summary>
    public bool IsBoxedProductSelected => SelectedProduct?.IsBoxedProduct == true;

    /// <summary>박스로 팔 때는 수량이 박스 개수라는 걸 라벨에 드러낸다.</summary>
    public string QuantityLabel =>
        IsBoxedProductSelected && SelectedSaleUnit == SaleUnitOption.Box
            ? "Quantity (boxes)"
            : "Quantity";

    /// <summary>지금 고른 판매 단위 기준의 상품 판매가.</summary>
    private decimal? CurrentSaleUnitPrice()
    {
        if (SelectedProduct is not { } product)
        {
            return null;
        }

        // 박스가가 기본값이고, 헐어 파는 낱개만 따로 정한 가격을 쓴다.
        return IsBoxSaleSelected(product) ? product.SellingPrice : product.EffectiveUnitSellingPrice;
    }

    private bool IsBoxSaleSelected(Product product) =>
        product.IsBoxedProduct && SelectedSaleUnit == SaleUnitOption.Box;

    private void ResetUnitPriceFromProduct()
    {
        UnitPrice = CurrentSaleUnitPrice()?.ToString() ?? string.Empty;
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

        // 낱개용 바코드는 내부 바코드 뒤에 -EA가 붙은 형태다. DB에는 접미사 없이
        // 저장돼 있으므로 떼어내고 찾되, 어느 단위로 찍었는지는 기억해 둔다.
        var scannedTerm = SearchTerm.Trim();

        var isUnitBarcode = scannedTerm.EndsWith(Product.UnitBarcodeSuffix, StringComparison.OrdinalIgnoreCase);

        _scannedSaleUnit = isUnitBarcode ? SaleUnitOption.Each : SaleUnitOption.Box;

        var lookupTerm = isUnitBarcode
            ? scannedTerm[..^Product.UnitBarcodeSuffix.Length]
            : scannedTerm;

        var results = await _productRepository.SearchAsync(lookupTerm, EntityStatus.Active);

        SearchResults.Clear();
        foreach (var product in results)
        {
            SearchResults.Add(product);
        }

        if (results.Count == 0)
        {
            Message = "Product not found.";
            return;
        }

        // 딱 하나면 바로 고른다. 바코드를 찍은 경우가 대부분이고,
        // 그때 목록에서 한 번 더 누르게 하면 스캐너를 쓰는 의미가 없다.
        // 여러 개면 고르지 않는다 — 계산대에서 엉뚱한 약이 잡히는 쪽이 훨씬 나쁘다.
        if (results.Count == 1)
        {
            SelectedProduct = results[0];
        }
    }

    /// <summary>
    /// 재고 화면에서 고른 상품을 그대로 들고 판매 화면을 연다.
    /// 바코드가 안 읽히는 상품을 팔 때 쓰는 경로다 — 이름을 다시 치게 하면
    /// 손님을 세워 둔 채로 검색을 하게 된다.
    ///
    /// 검색 결과 목록에도 넣어 두는 이유: 화면의 목록과 선택이 붙어 있어서,
    /// 목록에 없는 상품을 고르면 선택이 곧바로 풀린다.
    /// </summary>
    public async Task PreselectProductAsync(string productId)
    {
        Product? product;

        try
        {
            product = await _productRepository.GetByIdAsync(productId);
        }
        catch (Exception)
        {
            Message = "Product could not be loaded.";
            return;
        }

        if (product is null)
        {
            Message = "Product not found.";
            return;
        }

        // 바코드로 들어온 게 아니므로 판매 단위는 이름으로 찾았을 때와 같게 둔다.
        _scannedSaleUnit = SaleUnitOption.Box;

        SearchTerm = product.ProductName;

        SearchResults.Clear();
        SearchResults.Add(product);

        // 화면에서 직접 누른 것과 같은 경로다. 배치 로드와 가격 표시가 여기서 이어진다.
        SelectedProduct = product;
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
        //
        // expiry_date = 0은 "유효기간 모름"이다(초기 재고 임포트). 만료로 보면 팔 수가 없고,
        // 가장 이른 날짜로 보면 유효기한이 멀쩡한 배치를 제치고 먼저 나가므로 맨 뒤에 둔다.
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        var defaultBatch = Batches
            .Where(b => b.CurrentQuantity > 0 && !IsExpired(b.ExpiryDate, now))
            .OrderBy(b => b.ExpiryDate == InventoryEntity.NoExpiryDate ? 1 : 0)
            .ThenBy(b => b.ExpiryDate)
            .FirstOrDefault();

        SelectedBatch = defaultBatch;
    }

    /// <summary>
    /// 만료 판정. 유효기간을 모르는 배치(0)는 만료가 아니다 — 모르는 날짜를 1970-01-01로 읽어
    /// 초기 재고 전량을 못 팔게 만드는 쪽이 훨씬 나쁘다.
    /// </summary>
    private static bool IsExpired(long expiryDate, long now) =>
        expiryDate != InventoryEntity.NoExpiryDate && expiryDate <= now;

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
        if (IsExpired(SelectedBatch.ExpiryDate, now))
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

        var product = SelectedProduct;
        var isBoxSale = IsBoxSaleSelected(product);

        // 이미 장바구니에 담긴 같은 배치의 줄들을 먼저 빼고 남는 재고를 기준으로 판단한다.
        // 배치의 현재 수량만 보면, 이미 담아 둔 만큼을 두 번 팔 수 있게 된다.
        var remaining = RemainingStockForSelectedBatch();

        if (isBoxSale)
        {
            if (!BoxUnitMath.TryTakeBoxes(remaining, quantity, product.UnitsPerBox, out _))
            {
                // 총량이 충분해도 이미 헐어 놓은 낱개뿐이면 박스로는 팔 수 없다.
                Message = remaining.TotalUnits >= quantity * product.UnitsPerBox
                    ? $"Only {remaining.BoxQuantity} unopened box(es) left in this batch."
                    : "Stock-out quantity cannot exceed current inventory quantity.";
                return;
            }
        }
        else
        {
            if (remaining.TotalUnits < quantity)
            {
                Message = "Stock-out quantity cannot exceed current inventory quantity.";
                return;
            }

            // 헐어 놓은 낱개가 모자라면 박스를 헐어야 한다. 실제로 여는 건 판매 확정
            // 시점이지만, 약사에게 묻는 건 지금이어야 한다 — 결제까지 가서 물으면
            // 이미 되돌리기 어렵다.
            var boxesToOpen = BoxUnitMath.BoxesToOpen(remaining, quantity, product.UnitsPerBox);

            if (boxesToOpen > 0)
            {
                var openIt = AppDialog.Confirm(
                    "Open a Box",
                    $"Only {remaining.UnitQuantity} loose unit(s) left in this batch.\n" +
                    $"Open {boxesToOpen} box(es) of {product.UnitsPerBox} to sell {quantity}?",
                    confirmText: "Open",
                    cancelText: "Cancel");

                if (!openIt)
                {
                    Message = "Sale cancelled — no box was opened.";
                    return;
                }
            }

            if (!BoxUnitMath.TryTakeUnits(remaining, quantity, product.UnitsPerBox, out _))
            {
                Message = "Stock-out quantity cannot exceed current inventory quantity.";
                return;
            }
        }

        // 이미 장바구니에 같은 상품+배치가 있으면, 새 항목을 추가하는 대신 수량을 합산한다.
        // (Screen §5절 "상품 중복 추가" 예외 처리)
        // 판매 단위가 다르면 가격도 다르므로 박스 줄과 낱개 줄은 합치지 않는다.
        var existingLine = Cart.FirstOrDefault(
            c => c.ProductId == product.ProductId
                 && c.BatchNumber == SelectedBatch.BatchNumber
                 && c.IsBoxSale == isBoxSale);

        if (existingLine is not null)
        {
            existingLine.Quantity += quantity;
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
                ProductId = product.ProductId,
                ProductName = product.ProductName,
                // 항생제 복약안내 매칭에 쓴다. 판매 확정 뒤 상품을 다시 조회하지 않도록
                // 장바구니에 담을 때 함께 실어 둔다.
                GenericName = product.GenericName,
                AtcCode = product.AtcCode,
                InventoryId = SelectedBatch.InventoryId,
                BatchNumber = SelectedBatch.BatchNumber,
                ExpiryDate = SelectedBatch.ExpiryDate,
                Quantity = quantity,
                UnitPrice = unitPrice,
                // 원가도 판매 단위에 맞춰야 "원가보다 싸게 판다" 경고가 제대로 걸린다.
                CostPrice = isBoxSale ? product.CostPrice : product.UnitCostPrice,
                IsBoxSale = isBoxSale,
                UnitsPerBox = product.UnitsPerBox
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

    /// <summary>
    /// 선택한 배치의 재고에서 이미 장바구니에 담긴 같은 배치의 줄들을 뺀 나머지.
    /// 박스를 헐어야 하는지도 이 나머지를 기준으로 판단해야, 담아 둔 낱개까지
    /// 다시 쓸 수 있는 것처럼 계산되지 않는다.
    /// </summary>
    private BoxUnitStock RemainingStockForSelectedBatch()
    {
        var stock = SelectedBatch!.Stock;
        var unitsPerBox = SelectedProduct!.UnitsPerBox;

        foreach (var line in Cart.Where(c => c.InventoryId == SelectedBatch.InventoryId))
        {
            var taken = line.IsBoxSale
                ? BoxUnitMath.TryTakeBoxes(stock, line.Quantity, unitsPerBox, out var next)
                : BoxUnitMath.TryTakeUnits(stock, line.Quantity, unitsPerBox, out next);

            if (taken)
            {
                stock = next;
            }
        }

        return stock;
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