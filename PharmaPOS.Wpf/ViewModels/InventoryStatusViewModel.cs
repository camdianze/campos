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
    public decimal SellingPrice { get; set; }
    public bool IsLowStock { get; set; }
    public bool HasExpiringSoon { get; set; }
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
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var ninetyDaysMs = 90L * 24 * 60 * 60 * 1000;

        GroupedItems.Clear();
        foreach (var group in results.GroupBy(i => i.ProductName))
        {
            var batches = group.ToList();
            var totalQty = batches.Sum(b => b.CurrentQuantity);
            var isLowStock = batches.Any(b => b.IsLowStock);
            var expiringSoon = batches.Any(b =>
                b.ExpiryDate > 0 && b.ExpiryDate - now <= ninetyDaysMs && b.ExpiryDate > now);

            var pg = new ProductInventoryGroup
            {
                ProductName = group.Key,
                TotalQuantity = totalQty,
                SellingPrice = batches.First().SellingPrice,
                IsLowStock = isLowStock,
                HasExpiringSoon = expiringSoon
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