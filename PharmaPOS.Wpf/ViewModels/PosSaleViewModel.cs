using System.Collections.ObjectModel;
using System.Windows;
using PharmaPOS.Application.Counselling;
using PharmaPOS.Application.Inventory;
using PharmaPOS.Application.Receipts;
using PharmaPOS.Application.Repositories;
using PharmaPOS.Domain.Entities;
using PharmaPOS.Domain.Enums;
using Lightweight_Digital_Inventory_Management___POS_System.ViewModels.Base;
using Lightweight_Digital_Inventory_Management___POS_System.Views;

// 엔티티 이름(Inventory)이 Application의 네임스페이스와 같아 그냥 쓰면 네임스페이스로 읽힌다.
using InventoryEntity = PharmaPOS.Domain.Entities.Inventory;


using Lightweight_Digital_Inventory_Management___POS_System.Services;

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
    private readonly UiLanguageService _uiLanguage;
    private readonly IInventoryRepository _inventoryRepository;
    private readonly ISaleService _saleService;
    private readonly IReceiptPrintingService _receiptPrintingService;
    private readonly IReceiptSettingsService _receiptSettingsService;
    private readonly ICounsellingService _counsellingService;
    private readonly string _facilityId;
    private readonly string _userId;

    /// <summary>영수증의 담당자 줄에 찍는다. receipt.show.staff가 꺼져 있으면 쓰이지 않는다.</summary>
    private readonly string _username;
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
            if (ApplySelectedProduct(value))
            {
                // 목록에서 손으로 고른 경로. 배치는 뒤따라 읽히고 화면이 알아서 갱신된다.
                _ = LoadBatchesAsync();
            }
        }
    }

    /// <summary>
    /// 선택 상태만 바꾸고 배치는 읽지 않는다. 배치 로드를 기다려야 하는 쪽(바코드 스캔)과
    /// 기다릴 필요가 없는 쪽(목록 클릭)이 같은 상태 변경을 공유하도록 갈라 둔 것이다.
    /// </summary>
    private bool ApplySelectedProduct(Product? value)
    {
        if (!SetProperty(ref _selectedProduct, value, nameof(SelectedProduct)))
        {
            return false;
        }

        // 낱개용 바코드(-EA)를 찍었으면 그 판매 단위를 그대로 이어받는다.
        // 이름으로 찾았거나 박스/낱개 구분이 없는 상품이면 각각 박스·낱개가 기본이다.
        _selectedSaleUnit = value?.IsBoxedProduct == true
            ? _scannedSaleUnit
            : SaleUnitOption.Each;

        OnPropertyChanged(nameof(SelectedSaleUnit));
        OnPropertyChanged(nameof(IsBoxedProductSelected));
        OnPropertyChanged(nameof(QuantityLabel));

        return true;
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
            ? _uiLanguage.Text("ui.pos.quantity_boxes", "Quantity (boxes)")
            : _uiLanguage.Text("ui.pos.quantity", "Quantity");

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
        IReceiptSettingsService receiptSettingsService,
        ICounsellingService counsellingService,
        string facilityId,
        string userId,
        string username,
        UserRole currentUserRole,
        UiLanguageService uiLanguage)
    {
        _uiLanguage = uiLanguage;

        // 이 화면에도 언어 토글이 있어 떠 있는 채로 언어가 바뀐다. 언어 서비스는 앱과
        // 수명이 같으니 약한 구독으로 걸어, 화면이 걷힐 때 이 ViewModel도 함께 걷히게 한다.
        WeakEventManager<UiLanguageService, EventArgs>.AddHandler(
            _uiLanguage, nameof(UiLanguageService.LanguageChanged), OnLanguageChanged);

        _productRepository = productRepository;
        _inventoryRepository = inventoryRepository;
        _saleService = saleService;
        _receiptPrintingService = receiptPrintingService;
        _receiptSettingsService = receiptSettingsService;
        _counsellingService = counsellingService;
        _facilityId = facilityId;
        _userId = userId;
        _username = username;
        _isAdministrator = currentUserRole == UserRole.Administrator;

        SearchCommand = new RelayCommand(async _ => await ExecuteSearchAsync());
        AddToCartCommand = new RelayCommand(_ => ExecuteAddToCart());
        RemoveFromCartCommand = new RelayCommand(item => ExecuteRemoveFromCart(item as SaleLineItem));

        InitializePaymentCommands();

        // 환율·반올림 단위를 미리 읽어 둔다. 계산대가 리엘을 받을 수 있는지가
        // 이 값으로 정해지므로, 첫 판매 전에 화면이 준비돼 있어야 한다.
        _ = LoadCurrencySettingsAsync();
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

        // 낱개 코드는 두 종류다. 낱개에 제조사가 코드를 인쇄해 둔 상품은 그 값이
        // 그대로 저장돼 있어 찍은 값으로 한 번에 걸린다. 그런 코드가 없는 상품은
        // 내부 바코드 + "-EA" 라벨을 뽑아 쓰는데, 그 값은 DB에 통째로 들어 있지
        // 않으므로 접미사를 떼고 한 번 더 찾아야 한다.
        var scannedTerm = SearchTerm.Trim();

        var results = await _productRepository.SearchAsync(scannedTerm, EntityStatus.Active);
        var scanned = results.FirstOrDefault(p => IsExactBarcodeMatch(p, scannedTerm));

        if (scanned is null
            && scannedTerm.EndsWith(Product.UnitBarcodeSuffix, StringComparison.OrdinalIgnoreCase))
        {
            results = await _productRepository.SearchAsync(
                scannedTerm[..^Product.UnitBarcodeSuffix.Length], EntityStatus.Active);

            scanned = results.FirstOrDefault(p => IsExactBarcodeMatch(p, scannedTerm));
        }

        // 어느 단위로 찍었는지는 접미사가 아니라 <b>어느 칸이 맞았는지</b>로 정한다.
        // 낱개에 인쇄된 제조사 코드는 -EA로 끝나지 않으므로, 접미사만 보고 정하면
        // 그런 상품의 낱개를 찍었을 때 박스가 한 통 팔린다.
        _scannedSaleUnit = scanned is not null && IsUnitBarcodeMatch(scanned, scannedTerm)
            ? SaleUnitOption.Each
            : SaleUnitOption.Box;

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

        // 찍은 값이 바코드와 정확히 맞으면 장바구니까지 한 번에 간다.
        // 스캐너를 쓰는 이유가 손을 떼지 않는 것인데, 오른쪽에서 상품을 누르고
        // Add to Cart까지 눌러야 하면 스캐너가 검색창 대용에 그친다.
        //
        // 바코드는 유일 인덱스가 걸려 있어 정확히 맞은 값이 두 상품을 가리킬 수 없다.
        // 그래서 결과가 여럿이어도(이름에 같은 숫자가 들어간 상품 등) 망설일 이유가 없다.
        // 이름으로 찾은 경우는 종전 그대로다 — 사람이 고른다.
        // 첫 결과만 보지 않는다. 검색은 이름·성분명까지 훑으므로, 바코드가 정확히
        // 맞는 상품이 목록의 둘째 줄에 올 수도 있다(위에서 이미 그렇게 찾았다).
        if (scanned is not null)
        {
            // 배치를 다 읽은 뒤에 담아야 한다. 선택만 해 두고 바로 담으면
            // 배치가 아직 비어 있어 "Please select a batch number."로 튕긴다.
            await SelectProductAndLoadBatchesAsync(scanned);

            ExecuteAddToCart();

            // 스캔 경로에서 담기가 거절되면 창으로 알린다. 이유는 Message에 이미 들어
            // 있지만 화면 왼쪽 아래 한 줄이라, 스캐너를 보고 있는 계산대에서는 보이지
            // 않는다 — "찍었는데 아무 일도 없다"로 읽히고 손님 앞에서 같은 바코드를
            // 다시 찍게 된다. 손으로 담는 경로는 그대로 둔다. 그때는 버튼을 눌렀으니
            // 아래 줄을 본다.
            if (!string.IsNullOrEmpty(Message))
            {
                AppDialog.Show("Not added to cart", Message);
            }

            return;
        }

        // 딱 하나면 바로 고른다. 이름으로 찾았더라도 결과가 하나뿐이면
        // 목록에서 한 번 더 누르는 건 의식일 뿐이다.
        // 여러 개면 고르지 않는다 — 계산대에서 엉뚱한 약이 잡히는 쪽이 훨씬 나쁘다.
        if (results.Count == 1)
        {
            SelectedProduct = results[0];
        }
    }

    /// <summary>
    /// 찍은 값이 이 상품의 바코드 자체인지. 이름이 걸린 것과 구분하기 위한 것이다.
    /// 박스 쪽 코드와 낱개 쪽 코드를 모두 본다 — 접미사를 떼기 전의 값이 들어온다.
    /// </summary>
    private static bool IsExactBarcodeMatch(Product product, string scannedTerm) =>
        IsBoxBarcodeMatch(product, scannedTerm) || IsUnitBarcodeMatch(product, scannedTerm);

    /// <summary>
    /// 저장 쪽도 이제 공백을 지우지만, 그 수정 전에 저장된 상품은 그대로 남아 있다.
    /// 값에 공백이 붙어 있어도 스캔은 통해야 하므로 비교할 때 한 번 더 지운다.
    /// </summary>
    private static bool IsBoxBarcodeMatch(Product product, string scannedTerm) =>
        string.Equals(product.Barcode?.Trim(), scannedTerm, StringComparison.OrdinalIgnoreCase)
        || string.Equals(product.InternalBarcode?.Trim(), scannedTerm, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// 찍은 값이 이 상품의 <b>낱개</b> 코드인지. 낱개에 인쇄된 제조사 코드이거나
    /// 내부 바코드 + "-EA"이며, 둘 중 어느 쪽인지는 Product.UnitBarcode가 정한다.
    /// </summary>
    private static bool IsUnitBarcodeMatch(Product product, string scannedTerm) =>
        string.Equals(product.UnitBarcode?.Trim(), scannedTerm, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// 상품을 고르고 배치까지 읽어 온다. 목록 클릭 경로와 달리 기다릴 수 있다.
    /// </summary>
    private async Task SelectProductAndLoadBatchesAsync(Product product)
    {
        if (ApplySelectedProduct(product))
        {
            await LoadBatchesAsync();
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

    /// <summary>
    /// 마지막으로 시작한 배치 조회의 번호. 조회가 겹치면(목록 선택과 스캔이 잇따를 때)
    /// 먼저 끝난 옛 조회가 새 목록을 덮어써 SelectedBatch가 목록에 없는 객체를 가리키게
    /// 되고, 그러면 드롭다운이 배치번호 대신 클래스 이름을 찍는다. 자기 번호가 아니면 버린다.
    /// </summary>
    private int _batchLoadSequence;

    private async Task LoadBatchesAsync()
    {
        var sequence = ++_batchLoadSequence;

        Batches.Clear();
        SelectedBatch = null;

        if (SelectedProduct is null)
        {
            return;
        }

        var allBatches = await _inventoryRepository.GetBatchesForProductAsync(SelectedProduct.ProductId, _facilityId);

        if (sequence != _batchLoadSequence)
        {
            // 그새 다른 조회가 시작됐다. 그쪽이 목록을 채운다.
            return;
        }

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
        var remaining = RemainingStock(SelectedBatch.Stock, SelectedBatch.InventoryId, product.UnitsPerBox, exclude: null);

        if (!CanTakeFromStock(remaining, quantity, isBoxSale, product.UnitsPerBox))
        {
            return;
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
            RefreshCartLine(existingLine);
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
    /// 배치의 재고에서 이미 장바구니에 담긴 같은 배치의 줄들을 뺀 나머지.
    /// 박스를 헐어야 하는지도 이 나머지를 기준으로 판단해야, 담아 둔 낱개까지
    /// 다시 쓸 수 있는 것처럼 계산되지 않는다.
    ///
    /// exclude는 지금 수량을 고치고 있는 줄이다. 그 줄의 옛 수량까지 빼 버리면
    /// 5를 6으로 고칠 때 재고가 11개 있어야 하는 것처럼 계산된다.
    /// </summary>
    private BoxUnitStock RemainingStock(
        BoxUnitStock stock, string inventoryId, int unitsPerBox, SaleLineItem? exclude)
    {
        foreach (var line in Cart.Where(c => c.InventoryId == inventoryId && !ReferenceEquals(c, exclude)))
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

    /// <summary>
    /// 남은 재고에서 quantity만큼 팔 수 있는지. 못 팔면 Message에 이유를 적고 false.
    /// 담을 때와 장바구니에서 수량을 고칠 때가 같은 규칙이어야 한다 — 담을 때는 막히던
    /// 수량이 고치기로는 들어가면 재고 검사는 있으나 마나다.
    /// </summary>
    private bool CanTakeFromStock(BoxUnitStock remaining, int quantity, bool isBoxSale, int unitsPerBox)
    {
        if (isBoxSale)
        {
            if (!BoxUnitMath.TryTakeBoxes(remaining, quantity, unitsPerBox, out _))
            {
                // 총량이 충분해도 이미 헐어 놓은 낱개뿐이면 박스로는 팔 수 없다.
                Message = remaining.TotalUnits >= quantity * unitsPerBox
                    ? $"Only {remaining.BoxQuantity} unopened box(es) left in this batch."
                    : "Stock-out quantity cannot exceed current inventory quantity.";
                return false;
            }

            return true;
        }

        if (remaining.TotalUnits < quantity)
        {
            Message = "Stock-out quantity cannot exceed current inventory quantity.";
            return false;
        }

        // 헐어 놓은 낱개가 모자라면 박스를 헐어야 한다. 실제로 여는 건 판매 확정
        // 시점이지만, 약사에게 묻는 건 지금이어야 한다 — 결제까지 가서 물으면
        // 이미 되돌리기 어렵다.
        var boxesToOpen = BoxUnitMath.BoxesToOpen(remaining, quantity, unitsPerBox);

        if (boxesToOpen > 0)
        {
            var openIt = AppDialog.Confirm(
                "Open a Box",
                $"Only {remaining.UnitQuantity} loose unit(s) left in this batch.\n" +
                $"Open {boxesToOpen} box(es) of {unitsPerBox} to sell {quantity}?",
                confirmText: "Open",
                cancelText: "Cancel");

            if (!openIt)
            {
                Message = "Sale cancelled — no box was opened.";
                return false;
            }
        }

        if (!BoxUnitMath.TryTakeUnits(remaining, quantity, unitsPerBox, out _))
        {
            Message = "Stock-out quantity cannot exceed current inventory quantity.";
            return false;
        }

        return true;
    }

    /// <summary>
    /// 장바구니 줄의 수량을 고친다. 0이면 그 줄을 뺀다(Remove와 같다).
    /// 재고 검사는 담을 때와 같은 규칙으로 하고, 통과하지 못하면 수량을 그대로 두고
    /// false를 돌려준다 — 화면은 그때 칸의 글자를 원래 수량으로 되돌린다.
    /// 재고는 담을 때 봤던 값이 아니라 지금 값을 다시 읽는다. 담고 나서 다른 창에서
    /// 팔렸을 수 있고, 어차피 확정 시점에 한 번 더 검사되지만 여기서 미리 걸러야
    /// 결제까지 가서 거절당하지 않는다.
    /// </summary>
    public async Task<bool> ChangeCartQuantityAsync(SaleLineItem line, int quantity)
    {
        Message = string.Empty;

        if (quantity == line.Quantity)
        {
            return true;
        }

        if (quantity < 0)
        {
            Message = "Quantity cannot be negative.";
            return false;
        }

        if (quantity == 0)
        {
            ExecuteRemoveFromCart(line);
            return true;
        }

        InventoryBatchOption? batch;

        try
        {
            var batches = await _inventoryRepository.GetBatchesForProductAsync(line.ProductId, _facilityId);
            batch = batches.FirstOrDefault(b => b.InventoryId == line.InventoryId);
        }
        catch (Exception)
        {
            Message = "Stock could not be checked. Please try again.";
            return false;
        }

        if (batch is null)
        {
            Message = "This batch is no longer in stock.";
            return false;
        }

        var remaining = RemainingStock(batch.Stock, line.InventoryId, line.UnitsPerBox, exclude: line);

        if (!CanTakeFromStock(remaining, quantity, line.IsBoxSale, line.UnitsPerBox))
        {
            return false;
        }

        line.Quantity = quantity;
        RefreshCartLine(line);
        RaiseTotalsChanged();
        return true;
    }

    /// <summary>
    /// ObservableCollection은 항목 내부 속성 변경까지는 자동 통지하지 않으므로,
    /// DataGrid 등의 화면 갱신을 위해 컬렉션에서 제거 후 같은 자리에 다시 넣는다.
    /// </summary>
    private void RefreshCartLine(SaleLineItem line)
    {
        var index = Cart.IndexOf(line);

        if (index < 0)
        {
            return;
        }

        Cart.RemoveAt(index);
        Cart.Insert(index, line);
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

    // ── 화면 언어 ────────────────────────────────────────────────────────────
    // 계산대 직원이 종일 보는 화면이라 주요 버튼만 번역한다.
    // 번역이 없는 키는 영어가 그대로 나온다 — 빈 버튼보다 영어 버튼이 낫다.
    //
    // QuantityLabel은 이미 "Quantity (boxes)"처럼 판매 단위를 알려 주는 다른 뜻으로
    // 쓰이고 있어 건드리지 않는다.

    public string AddToCartLabel => _uiLanguage.Text("ui.pos.add_to_cart", "＋  Add to Cart");

    public string ConfirmSaleLabel => _uiLanguage.Text("ui.pos.confirm_sale", "✓  Confirm Sale");

    public string CancelSaleLabel => _uiLanguage.Text("ui.pos.cancel_sale", "✕  Cancel Sale");

    public string RemoveLabel => _uiLanguage.Text("ui.pos.remove", "Remove");

    public string BackLabel => _uiLanguage.Text("ui.pos.back", "← Back");

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(QuantityLabel));
        OnPropertyChanged(nameof(AddToCartLabel));
        OnPropertyChanged(nameof(ConfirmSaleLabel));
        OnPropertyChanged(nameof(CancelSaleLabel));
        OnPropertyChanged(nameof(RemoveLabel));
        OnPropertyChanged(nameof(BackLabel));
    }
}