using System.Collections.ObjectModel;
using System.Windows;
using PharmaPOS.Application.Inventory;
using PharmaPOS.Application.Repositories;
using Lightweight_Digital_Inventory_Management___POS_System.ViewModels.Base;
using Lightweight_Digital_Inventory_Management___POS_System.Views;

namespace Lightweight_Digital_Inventory_Management___POS_System.ViewModels;

// 배치별 그룹핑을 위한 모델
public class ProductInventoryGroup
{
    public string ProductName { get; set; } = string.Empty;
    public int TotalQuantity { get; set; }

    /// <summary>제품에 설정된 기준 재고. 화면에 "15 / 20"처럼 합계와 나란히 보여준다.</summary>
    public int SafetyStockLevel { get; set; }

    public decimal SellingPrice { get; set; }

    // 아래 세 개는 배치 상태의 합집합이다. 제품 행을 접어도 안에 어떤 문제가 있는지
    // 전부 보이고, 펼치면 어느 배치 때문인지 배치 행에서 구분된다.

    /// <summary>배치 중 기준 재고 미만인 게 하나라도 있는지.</summary>
    public bool IsLowStock { get; set; }

    /// <summary>배치 중 만료임박이 하나라도 있는지.</summary>
    public bool HasExpiringSoon { get; set; }

    /// <summary>배치 중 이미 만료된 게 하나라도 있는지.</summary>
    public bool HasExpired { get; set; }

    /// <summary>배치 중 가장 빠른 만료일(Unix ms). 없으면 null. 선입선출 판단용.</summary>
    public long? EarliestExpiryDate { get; set; }

    /// <summary>박스/낱개를 나눠 파는 상품인지. 아니면 아래 두 값은 화면에 나오지 않는다.</summary>
    public bool IsBoxedProduct { get; set; }

    /// <summary>배치 전체에서 아직 뜯지 않은 박스 수.</summary>
    public int BoxQuantity { get; set; }

    /// <summary>배치 전체에서 헐어 놓은 낱개 수.</summary>
    public int UnitQuantity { get; set; }

    public ObservableCollection<InventoryStatusItem> Batches { get; set; } = new();
}

public class InventoryStatusViewModel : ViewModelBase
{
    private readonly IInventoryRepository _inventoryRepository;
    private readonly IAdjustmentService _adjustmentService;
    private readonly IStockInService _stockInService;
    private readonly string _facilityId;
    private readonly string _userId;

    private string _searchTerm = string.Empty;
    private ExpiryFilterOption _selectedExpiryFilter = ExpiryFilterOption.All;
    private bool _lowStockOnly;
    private InventorySortOption _selectedSortOption = InventorySortOption.ProductName;
    private InventoryStatusItem? _selectedItem;
    private string _message = string.Empty;

    public ObservableCollection<InventoryStatusItem> Items { get; } = new();
    public ObservableCollection<ProductInventoryGroup> GroupedItems { get; } = new();

    public string SearchTerm
    {
        get => _searchTerm;
        set { if (SetProperty(ref _searchTerm, value)) _ = ReloadAsync(); }
    }

    public ExpiryFilterOption SelectedExpiryFilter
    {
        get => _selectedExpiryFilter;
        set { if (SetProperty(ref _selectedExpiryFilter, value)) _ = ReloadAsync(); }
    }

    public bool LowStockOnly
    {
        get => _lowStockOnly;
        set { if (SetProperty(ref _lowStockOnly, value)) _ = ReloadAsync(); }
    }

    public InventorySortOption SelectedSortOption
    {
        get => _selectedSortOption;
        set { if (SetProperty(ref _selectedSortOption, value)) _ = ReloadAsync(); }
    }

    public IReadOnlyList<ExpiryFilterOption> AvailableExpiryFilters { get; } = Enum.GetValues<ExpiryFilterOption>();
    public IReadOnlyList<InventorySortOption> AvailableSortOptions { get; } = Enum.GetValues<InventorySortOption>();

    /// <summary>
    /// TreeView에서 고른 배치 행. 제품 행을 고르면 null이 된다
    /// (제품은 배치의 묶음일 뿐이라 지우거나 상세를 볼 대상이 아니다).
    /// TreeView.SelectedItem은 읽기 전용이라 바인딩이 안 되고, 코드비하인드가 넣어 준다.
    /// </summary>
    public InventoryStatusItem? SelectedItem
    {
        get => _selectedItem;
        set
        {
            if (SetProperty(ref _selectedItem, value))
            {
                OnPropertyChanged(nameof(CanDeleteSelectedBatch));
                OnPropertyChanged(nameof(DeleteBatchHint));
                OnPropertyChanged(nameof(AdjustmentTargetSummary));
                OnPropertyChanged(nameof(IsBoxedBatchSelected));
                OnPropertyChanged(nameof(SystemQuantity));
                OnPropertyChanged(nameof(SystemQuantityBreakdown));
                OnPropertyChanged(nameof(PhysicalUnitCountLabel));

                // 다른 배치로 옮기면 실사 값을 비운다. A 배치를 세어 적어 둔 숫자가
                // 그대로 B 배치에 저장되면 조정 이력이 통째로 거짓이 된다.
                ResetAdjustmentForm();

                // 제품 행으로 옮기면 조정할 대상이 없어진다.
                if (value is null)
                {
                    IsAdjustmentPanelVisible = false;
                }

                OnSelectionChangedForStockIn();
            }
        }
    }

    private ProductInventoryGroup? _selectedGroup;

    /// <summary>
    /// TreeView에서 고른 제품 행. 배치 행을 고르면 null이 된다.
    /// 조정 화면으로 넘어갈 때 제품만이라도 미리 채워 주려고 들고 있는다.
    /// </summary>
    public ProductInventoryGroup? SelectedGroup
    {
        get => _selectedGroup;
        set
        {
            if (SetProperty(ref _selectedGroup, value))
            {
                OnSelectionChangedForStockIn();
            }
        }
    }

    /// <summary>
    /// 선택이 바뀌면 입고 패널은 접는다. 조정 패널과 달리 입고 패널에는 고른 배치에서
    /// 미리 채워 둔 배치번호·유효기한이 들어 있어, 열어 둔 채로 다른 줄을 고르면
    /// 표에서 파랗게 보이는 줄과 패널이 가리키는 배치가 어긋난다.
    /// </summary>
    private void OnSelectionChangedForStockIn()
    {
        OnPropertyChanged(nameof(StockInTargetSummary));
        OnPropertyChanged(nameof(IsBoxedStockInTarget));
        OnPropertyChanged(nameof(StockInQuantityLabel));
        OnPropertyChanged(nameof(StockInTotalPreview));

        if (IsStockInPanelVisible)
        {
            IsStockInPanelVisible = false;
            ResetStockInForm();
        }
    }

    /// <summary>다 팔려 빈 배치만 지울 수 있다. 버튼 활성화 조건이다.</summary>
    public bool CanDeleteSelectedBatch => SelectedItem is { CurrentQuantity: 0 };

    /// <summary>지울 수 없는 이유를 버튼 옆에 미리 알려 준다.</summary>
    public string DeleteBatchHint => SelectedItem switch
    {
        null => "Select a batch row to delete it.",
        { CurrentQuantity: 0 } item => $"Batch {item.BatchNumber} is empty and can be removed.",
        var item => $"Batch {item.BatchNumber} still has {item.CurrentQuantity} left. Use Adjustment instead."
    };

    public string Message
    {
        get => _message;
        set => SetProperty(ref _message, value);
    }

    public RelayCommand ViewDetailCommand { get; }
    public RelayCommand StockInCommand { get; }
    public RelayCommand AdjustmentCommand { get; }
    public RelayCommand DeleteBatchCommand { get; }

    public InventoryStatusViewModel(
        IInventoryRepository inventoryRepository,
        IAdjustmentService adjustmentService,
        IStockInService stockInService,
        string facilityId,
        string userId)
    {
        _inventoryRepository = inventoryRepository;
        _adjustmentService = adjustmentService;
        _stockInService = stockInService;
        _facilityId = facilityId;
        _userId = userId;

        ViewDetailCommand = new RelayCommand(_ => ExecuteViewDetail());
        StockInCommand = new RelayCommand(_ => ExecuteOpenStockInPanel());
        AdjustmentCommand = new RelayCommand(_ => ExecuteOpenAdjustmentPanel());
        DeleteBatchCommand = new RelayCommand(async _ => await ExecuteDeleteBatchAsync());

        SaveAdjustmentCommand = new RelayCommand(async _ => await ExecuteSaveAdjustmentAsync(allowZeroDelta: false));
        CancelAdjustmentCommand = new RelayCommand(_ => IsAdjustmentPanelVisible = false);

        SaveStockInCommand = new RelayCommand(async _ => await ExecuteSaveStockInAsync());
        CancelStockInCommand = new RelayCommand(_ => IsStockInPanelVisible = false);

        _ = ReloadAsync();
    }

    // ── 입고(Stock-IN) 패널 ──────────────────────────────────────────────────
    //
    // 재고 목록에서 상품을 고르고 Stock-in을 누르면 Products 화면으로 넘어가,
    // 방금 고른 상품을 거기서 다시 검색해야 했다. 조정과 같은 이유로 이 자리에서 끝낸다.
    //
    // 한 가지 남는 제약: 이 목록은 재고가 있는 배치만 보여주므로, 아직 한 번도 입고한 적 없는
    // 신상품은 여기 뜨지 않는다. 그건 종전대로 Products 화면의 입고 패널에서 해야 한다.

    private bool _isStockInPanelVisible;
    private string _stockInBatchNumber = string.Empty;
    private DateTime _stockInExpiryDate = DateTime.Today.AddYears(1);
    private DateTime _stockInDate = DateTime.Today;
    private string _stockInQuantity = string.Empty;
    private string _stockInMessage = string.Empty;

    /// <summary>
    /// 입고 대상 상품을 대표하는 배치 행. 배치 행을 골랐으면 그 배치, 제품 행을 골랐으면
    /// 그 제품의 아무 배치나 하나 — 필요한 건 상품 정보(ID·이름·박스당 개수)뿐이고,
    /// 그건 같은 제품의 어느 배치에서 꺼내도 같다.
    /// </summary>
    private InventoryStatusItem? StockInSource => SelectedItem ?? SelectedGroup?.Batches.FirstOrDefault();

    /// <summary>Stock-in 버튼을 누르기 전에는 접혀 있다.</summary>
    public bool IsStockInPanelVisible
    {
        get => _isStockInPanelVisible;
        set => SetProperty(ref _isStockInPanelVisible, value);
    }

    public string StockInTargetSummary => StockInSource?.ProductName ?? string.Empty;

    /// <summary>박스/낱개를 나눠 파는 상품인지. 수량 칸의 뜻이 달라진다.</summary>
    public bool IsBoxedStockInTarget => StockInSource?.IsBoxedProduct == true;

    /// <summary>새 배치를 적으면 새 배치가 생기고, 있는 배치를 그대로 두면 그 배치가 늘어난다.</summary>
    public string StockInBatchNumber
    {
        get => _stockInBatchNumber;
        set => SetProperty(ref _stockInBatchNumber, value);
    }

    public DateTime StockInExpiryDate
    {
        get => _stockInExpiryDate;
        set => SetProperty(ref _stockInExpiryDate, value);
    }

    /// <summary>입고 날짜. 기본값은 오늘.</summary>
    public DateTime StockInDate
    {
        get => _stockInDate;
        set => SetProperty(ref _stockInDate, value);
    }

    /// <summary>박스 상품이면 이 값은 "박스 개수"다. 입고는 늘 박스째 들어온다.</summary>
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

    public string StockInQuantityLabel => IsBoxedStockInTarget ? "Boxes" : "Quantity";

    /// <summary>"10 boxes × 30 = 300 units" — 저장 전에 실제로 얼마가 들어가는지 보여준다.</summary>
    public string StockInTotalPreview
    {
        get
        {
            if (StockInSource is not { IsBoxedProduct: true } source)
            {
                return string.Empty;
            }

            if (!int.TryParse(StockInQuantity, out var boxes) || boxes <= 0)
            {
                return $"{source.UnitsPerBox} units per box.";
            }

            return $"{boxes} box(es) × {source.UnitsPerBox} = {boxes * source.UnitsPerBox} units.";
        }
    }

    public string StockInMessage
    {
        get => _stockInMessage;
        set => SetProperty(ref _stockInMessage, value);
    }

    public RelayCommand SaveStockInCommand { get; }
    public RelayCommand CancelStockInCommand { get; }

    private void ExecuteOpenStockInPanel()
    {
        Message = string.Empty;

        if (StockInSource is null)
        {
            Message = "Please select a product or batch to stock in.";
            IsStockInPanelVisible = false;
            return;
        }

        // 두 패널을 같이 열어 두면 아래에 Save가 둘이라 어느 쪽인지 헷갈린다.
        IsAdjustmentPanelVisible = false;
        ResetStockInForm();

        // 배치 행에서 열면 "이 배치로 더 넣기"가 기본이고, 제품 행에서 열면
        // 배치 칸을 비워 둬 새 배치를 적게 한다.
        if (SelectedItem is { } batch)
        {
            StockInBatchNumber = batch.BatchNumber;

            if (batch.ExpiryDate > 0)
            {
                StockInExpiryDate = DateTimeOffset.FromUnixTimeMilliseconds(batch.ExpiryDate).LocalDateTime.Date;
            }
        }

        IsStockInPanelVisible = true;
    }

    /// <summary>
    /// 입고 저장. 검증과 저장은 StockInService가 하고, 여기서는 숫자 변환만 먼저 거른다
    /// (Products 화면의 입고 패널과 같은 흐름).
    /// </summary>
    private async Task ExecuteSaveStockInAsync()
    {
        StockInMessage = string.Empty;

        if (StockInSource is not { } source)
        {
            StockInMessage = "Please select a product.";
            return;
        }

        if (!int.TryParse(StockInQuantity, out var quantity))
        {
            StockInMessage = IsBoxedStockInTarget
                ? "Box quantity must be a whole number."
                : "Quantity must be a whole number.";
            return;
        }

        var result = await _stockInService.SaveStockInAsync(
            _facilityId, source.ProductId, _userId,
            StockInBatchNumber, StockInExpiryDate, StockInDate, quantity);

        if (!result.IsSuccess)
        {
            StockInMessage = result.Message!;
            return;
        }

        var summary = source.IsBoxedProduct
            ? $"Stock-in saved for {source.ProductName}: {quantity} box(es), {quantity * source.UnitsPerBox} units."
            : $"Stock-in saved for {source.ProductName}.";

        IsStockInPanelVisible = false;
        ResetStockInForm();

        // 조정과 마찬가지로, 목록을 다시 읽은 뒤에 메시지를 넣는다.
        await ReloadAsync();
        Message = summary;
    }

    private void ResetStockInForm()
    {
        StockInBatchNumber = string.Empty;
        StockInExpiryDate = DateTime.Today.AddYears(1);
        StockInDate = DateTime.Today;
        StockInQuantity = string.Empty;
        StockInMessage = string.Empty;
    }

    // ── 조정(Adjustment) 패널 ────────────────────────────────────────────────
    //
    // 조정은 원래 별도 화면이었고, 거기서는 조정하려는 상품을 검색으로 다시 찾아야 했다.
    // 재고 목록에서 이미 고른 배치를 눈앞에 두고 다시 찾는 셈이라, 상품 화면의 입고 패널과
    // 같은 방식으로 목록 아래에 펼치도록 옮겼다. 조정 대상은 언제나 "고른 배치"다.
    // (Products 화면의 Stock-IN 패널과 짝을 이루는 구조다.)

    private bool _isAdjustmentPanelVisible;
    private string _physicalBoxCount = "0";
    private string _physicalUnitCount = string.Empty;
    private string _adjustmentReason = string.Empty;
    private string _adjustmentMessage = string.Empty;
    private int? _delta;

    /// <summary>Adjustment 버튼을 누르기 전에는 접혀 있다.</summary>
    public bool IsAdjustmentPanelVisible
    {
        get => _isAdjustmentPanelVisible;
        set => SetProperty(ref _isAdjustmentPanelVisible, value);
    }

    /// <summary>패널 머리에 띄우는 "무엇을 세고 있는가".</summary>
    public string AdjustmentTargetSummary => SelectedItem is { } item
        ? $"{item.ProductName} · Batch {item.BatchNumber}"
        : string.Empty;

    /// <summary>박스/낱개를 나눠 세는 상품인지. 아니면 박스 입력칸을 숨긴다.</summary>
    public bool IsBoxedBatchSelected => SelectedItem?.IsBoxedProduct == true;

    /// <summary>전산 재고. 실사 값과 비교할 기준이다.</summary>
    public int SystemQuantity => SelectedItem?.CurrentQuantity ?? 0;

    /// <summary>
    /// 전산 재고의 박스/낱개 내역. 무엇과 비교하는지 보이지 않으면
    /// 박스 칸에 총량을 적는 식의 실수가 난다.
    /// </summary>
    public string SystemQuantityBreakdown => SelectedItem is { IsBoxedProduct: true } item
        ? $"System: {item.BoxQuantity} box(es) of {item.UnitsPerBox} + {item.UnitQuantity} loose unit(s)."
        : string.Empty;

    /// <summary>박스 칸이 함께 보일 때만 "Loose Units"로 갈라 부른다.</summary>
    public string PhysicalUnitCountLabel =>
        IsBoxedBatchSelected ? "Physical Count — Loose Units" : "Physical Count";

    /// <summary>실사한 박스 수. 박스/낱개 구분이 없는 상품은 0으로 둔다.</summary>
    public string PhysicalBoxCount
    {
        get => _physicalBoxCount;
        set { if (SetProperty(ref _physicalBoxCount, value)) RecalculateDelta(); }
    }

    /// <summary>실사한 낱개 수. 박스/낱개 구분이 없는 상품은 여기에 전량을 적는다.</summary>
    public string PhysicalUnitCount
    {
        get => _physicalUnitCount;
        set { if (SetProperty(ref _physicalUnitCount, value)) RecalculateDelta(); }
    }

    /// <summary>실사 − 전산. 아직 실사를 적지 않았으면 null.</summary>
    public int? Delta
    {
        get => _delta;
        private set => SetProperty(ref _delta, value);
    }

    public string AdjustmentReason
    {
        get => _adjustmentReason;
        set => SetProperty(ref _adjustmentReason, value);
    }

    /// <summary>패널 안에서만 쓰는 메시지. 목록 아래 Message와 섞이지 않게 따로 둔다.</summary>
    public string AdjustmentMessage
    {
        get => _adjustmentMessage;
        set => SetProperty(ref _adjustmentMessage, value);
    }

    public RelayCommand SaveAdjustmentCommand { get; }
    public RelayCommand CancelAdjustmentCommand { get; }

    private void ExecuteOpenAdjustmentPanel()
    {
        Message = string.Empty;

        // 조정 단위는 언제나 배치다. 제품 행에는 유효기한도 재고 수량도 하나로 정해지지 않는다.
        if (SelectedItem is null)
        {
            Message = SelectedGroup is null
                ? "Please select a batch row to adjust."
                : "Please expand the product and select the batch you want to adjust.";
            IsAdjustmentPanelVisible = false;
            return;
        }

        IsStockInPanelVisible = false;
        ResetAdjustmentForm();
        IsAdjustmentPanelVisible = true;
    }

    private void RecalculateDelta()
    {
        Delta = SelectedItem is not null && TryParsePhysicalCount(out var physicalCount)
            ? physicalCount - SystemQuantity
            : null;
    }

    /// <summary>
    /// 실사 입력 두 칸을 낱개 총량으로 합친다. 박스 칸은 비워 둘 수 있게 0으로 봐 준다 —
    /// 헐어 놓은 낱개만 세는 경우가 흔하다. 낱개 칸이 비어 있으면 실사를 아직 적지 않은 것이다.
    /// </summary>
    private bool TryParsePhysicalCount(out int physicalCount)
    {
        physicalCount = 0;

        var boxCount = 0;

        if (!string.IsNullOrWhiteSpace(PhysicalBoxCount) && !int.TryParse(PhysicalBoxCount, out boxCount))
        {
            return false;
        }

        if (!int.TryParse(PhysicalUnitCount, out var unitCount))
        {
            return false;
        }

        physicalCount = BoxUnitMath.ToTotalUnits(
            boxCount, unitCount, SelectedItem?.UnitsPerBox ?? 1);
        return true;
    }

    private async Task ExecuteSaveAdjustmentAsync(bool allowZeroDelta)
    {
        AdjustmentMessage = string.Empty;

        if (SelectedItem is not { } item)
        {
            AdjustmentMessage = "Please select a batch.";
            return;
        }

        if (!TryParsePhysicalCount(out _))
        {
            AdjustmentMessage = "Please enter the physical count.";
            return;
        }

        // 위에서 파싱이 통과했으니 두 칸 모두 숫자다. 박스 칸은 비워 두면 0으로 본다.
        int.TryParse(PhysicalBoxCount, out var physicalBoxCount);
        int.TryParse(PhysicalUnitCount, out var physicalUnitCount);

        var result = await _adjustmentService.SaveAdjustmentAsync(
            _facilityId, item.ProductId, _userId,
            item.InventoryId, item.BatchNumber, item.ExpiryDate,
            item.CurrentQuantity, physicalBoxCount, physicalUnitCount, item.UnitsPerBox,
            AdjustmentReason, allowZeroDelta);

        if (result.IsSuccess)
        {
            IsAdjustmentPanelVisible = false;
            ResetAdjustmentForm();

            // 목록을 다시 읽어야 조정된 수량이 보인다. 성공 메시지는 그 뒤에 넣는다 —
            // ReloadAsync가 Message를 자기 값으로 덮어쓰기 때문이다.
            await ReloadAsync();
            Message = "Adjustment saved successfully.";
            return;
        }

        if (result.RequiresConfirmation)
        {
            if (AppDialog.Confirm("Confirm", result.Message!))
            {
                await ExecuteSaveAdjustmentAsync(allowZeroDelta: true);
            }

            return;
        }

        AdjustmentMessage = result.Message!;

        if (result.IsConcurrencyConflict)
        {
            await ReloadAsync();
        }
    }

    private void ResetAdjustmentForm()
    {
        PhysicalBoxCount = "0";
        PhysicalUnitCount = string.Empty;
        AdjustmentReason = string.Empty;
        AdjustmentMessage = string.Empty;
        Delta = null;
    }

    public async Task ReloadAsync()
    {
        IReadOnlyList<InventoryStatusItem> results;

        try
        {
            results = await _inventoryRepository.GetInventoryStatusAsync(
                SearchTerm, SelectedExpiryFilter, LowStockOnly, SelectedSortOption);
        }
        catch (Exception)
        {
            Message = "Inventory data could not be loaded.";
            return;
        }

        // 기존 Items 유지 (View Detail 등에 사용)
        Items.Clear();
        foreach (var item in results)
            Items.Add(item);

        // 약품별 그룹핑
        GroupedItems.Clear();
        foreach (var group in results.GroupBy(i => i.ProductName))
        {
            var batches = group.ToList();
            var totalQty = batches.Sum(b => b.CurrentQuantity);

            // 기준 재고는 제품 속성이라 배치가 몇 개든 값이 같다. 판매가와 같은 방식으로 꺼낸다.
            var safetyStockLevel = batches.First().SafetyStockLevel;

            // 세 상태 모두 배치의 합집합이다. 접힌 행에 안쪽 문제가 빠짐없이 드러나야 한다.
            // 판단 규칙은 InventoryStatusItem이 들고 있는 걸 그대로 쓴다. 여기서 다시
            // 계산하면 제품 행 배지와 배치 행 배지가 어긋날 수 있다.
            var isLowStock = batches.Any(b => b.IsLowStock);
            var expiringSoon = batches.Any(b => b.IsExpiringSoon);
            var hasExpired = batches.Any(b => b.IsExpired);

            // 만료일이 없는(0) 배치는 제외하고 가장 이른 날짜를 고른다.
            var datedBatches = batches.Where(b => b.ExpiryDate > 0).ToList();

            var pg = new ProductInventoryGroup
            {
                ProductName = group.Key,
                TotalQuantity = totalQty,
                SafetyStockLevel = safetyStockLevel,
                // 재고 수량이 낱개 기준이므로 단가도 낱개로 맞춰 보여준다.
                SellingPrice = batches.First().DisplayUnitPrice,
                IsLowStock = isLowStock,
                HasExpiringSoon = expiringSoon,
                HasExpired = hasExpired,
                EarliestExpiryDate = datedBatches.Count == 0 ? null : datedBatches.Min(b => b.ExpiryDate),

                // 박스당 낱개 수는 제품 속성이라 배치가 몇 개든 같다. 기준 재고와 같은 방식으로 꺼낸다.
                IsBoxedProduct = batches.First().IsBoxedProduct,
                BoxQuantity = batches.Sum(b => b.BoxQuantity),
                UnitQuantity = batches.Sum(b => b.UnitQuantity)
            };

            foreach (var b in batches)
                pg.Batches.Add(b);

            GroupedItems.Add(pg);
        }

        Message = results.Count == 0 ? "No inventory records found." : string.Empty;
    }

    /// <summary>
    /// 다 팔린 배치 행을 목록에서 치운다. 판매 이력은 Stock_Transaction에 그대로 남는다.
    /// </summary>
    private async Task ExecuteDeleteBatchAsync()
    {
        Message = string.Empty;

        if (SelectedItem is not { } item)
        {
            Message = "Please select a batch to delete.";
            return;
        }

        // 재고가 남은 배치를 지우면 아무 기록 없이 재고가 사라진다. 그건 조정으로 해야 한다.
        if (item.CurrentQuantity != 0)
        {
            Message = $"Batch {item.BatchNumber} still has {item.CurrentQuantity} in stock. "
                      + "Use Adjustment to write it off first.";
            return;
        }

        var confirmed = AppDialog.Confirm(
            "Delete Batch",
            $"Remove batch {item.BatchNumber} of {item.ProductName} from the inventory list?\n\n"
            + "It is empty, and past sales records are kept.",
            confirmText: "Delete",
            cancelText: "Cancel");

        if (!confirmed)
        {
            return;
        }

        bool deleted;

        try
        {
            deleted = await _inventoryRepository.DeleteEmptyBatchAsync(item.InventoryId);
        }
        catch (Exception)
        {
            Message = "The batch could not be deleted.";
            return;
        }

        SelectedItem = null;

        // ReloadAsync가 끝에서 Message를 비우므로, 알릴 말은 그 뒤에 넣는다.
        await ReloadAsync();

        // 지워지지 않았다면 확인과 삭제 사이에 입고가 들어온 것이다. 목록은 이미 새로
        // 불러왔으니 사용자가 실제 상태를 보고 다시 판단하면 된다.
        Message = deleted
            ? $"Batch {item.BatchNumber} was removed."
            : "The batch was not empty any more and was kept.";
    }

    private void ExecuteViewDetail()
    {
        if (SelectedItem is null) return;

        var expiry = DateTimeOffset.FromUnixTimeMilliseconds(SelectedItem.ExpiryDate).ToLocalTime();
        var updated = DateTimeOffset.FromUnixTimeMilliseconds(SelectedItem.UpdatedAt).ToLocalTime();

        // 박스/낱개 상품만 내역을 한 줄 더 붙인다. 낱개 단위 상품에는 나눌 것이 없다.
        var boxBreakdown = SelectedItem.IsBoxedProduct
            ? $"\nBoxes: {SelectedItem.BoxQuantity} ({SelectedItem.UnitsPerBox} per box)"
              + $"\nLoose Units: {SelectedItem.UnitQuantity}"
              + $"\nBox Price: {SelectedItem.SellingPrice}"
            : string.Empty;

        var detail = $"""
            Product: {SelectedItem.ProductName}
            Batch Number: {SelectedItem.BatchNumber}
            Current Quantity: {SelectedItem.CurrentQuantity}{boxBreakdown}
            Unit Price: {SelectedItem.DisplayUnitPrice}
            Expiry Date: {expiry:yyyy-MM-dd}
            Last Updated: {updated:yyyy-MM-dd HH:mm}
            Low Stock: {(SelectedItem.IsLowStock ? "Yes" : "No")}
            """;

        AppDialog.Show("Inventory Detail", detail, monospace: true);
    }
}