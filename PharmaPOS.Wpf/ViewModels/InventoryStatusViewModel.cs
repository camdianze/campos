using System.Collections.ObjectModel;
using System.Windows;
using PharmaPOS.Application.Inventory;
using PharmaPOS.Application.Repositories;
using Lightweight_Digital_Inventory_Management___POS_System.ViewModels.Base;

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

    public ObservableCollection<InventoryStatusItem> Batches { get; set; } = new();
}

public class InventoryStatusViewModel : ViewModelBase
{
    private readonly IInventoryRepository _inventoryRepository;

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

    public InventoryStatusItem? SelectedItem
    {
        get => _selectedItem;
        set => SetProperty(ref _selectedItem, value);
    }

    public string Message
    {
        get => _message;
        set => SetProperty(ref _message, value);
    }

    public RelayCommand ViewDetailCommand { get; }
    public RelayCommand StockInCommand { get; }
    public RelayCommand AdjustmentCommand { get; }

    public event Action? NavigateToStockIn;
    public event Action? NavigateToAdjustment;

    public InventoryStatusViewModel(IInventoryRepository inventoryRepository)
    {
        _inventoryRepository = inventoryRepository;

        ViewDetailCommand = new RelayCommand(_ => ExecuteViewDetail());
        StockInCommand = new RelayCommand(_ => NavigateToStockIn?.Invoke());
        AdjustmentCommand = new RelayCommand(_ => NavigateToAdjustment?.Invoke());

        _ = ReloadAsync();
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
                SellingPrice = batches.First().SellingPrice,
                IsLowStock = isLowStock,
                HasExpiringSoon = expiringSoon,
                HasExpired = hasExpired,
                EarliestExpiryDate = datedBatches.Count == 0 ? null : datedBatches.Min(b => b.ExpiryDate)
            };

            foreach (var b in batches)
                pg.Batches.Add(b);

            GroupedItems.Add(pg);
        }

        Message = results.Count == 0 ? "No inventory records found." : string.Empty;
    }

    private void ExecuteViewDetail()
    {
        if (SelectedItem is null) return;

        var expiry = DateTimeOffset.FromUnixTimeMilliseconds(SelectedItem.ExpiryDate).ToLocalTime();
        var updated = DateTimeOffset.FromUnixTimeMilliseconds(SelectedItem.UpdatedAt).ToLocalTime();

        var detail = $"""
            Product: {SelectedItem.ProductName}
            Batch Number: {SelectedItem.BatchNumber}
            Current Quantity: {SelectedItem.CurrentQuantity}
            Selling Price: {SelectedItem.SellingPrice}
            Expiry Date: {expiry:yyyy-MM-dd}
            Last Updated: {updated:yyyy-MM-dd HH:mm}
            Low Stock: {(SelectedItem.IsLowStock ? "Yes" : "No")}
            """;

        MessageBox.Show(detail, "Inventory Detail");
    }
}