using System.Collections.ObjectModel;
using System.Windows;
using PharmaPOS.Application.Inventory;
using PharmaPOS.Application.Repositories;
using PharmaPOS.Domain.Entities;
using PharmaPOS.Domain.Enums;
using Lightweight_Digital_Inventory_Management___POS_System.ViewModels.Base;

namespace Lightweight_Digital_Inventory_Management___POS_System.ViewModels;

/// <summary>
/// 재고 조정 화면(SCR-ADJ-010)의 ViewModel.
/// </summary>
public class AdjustmentViewModel : ViewModelBase
{
    private readonly IProductRepository _productRepository;
    private readonly IInventoryRepository _inventoryRepository;
    private readonly IAdjustmentService _adjustmentService;
    private readonly string _facilityId;
    private readonly string _userId;

    private string _searchTerm = string.Empty;
    private Product? _selectedProduct;
    private InventoryBatchOption? _selectedBatch;
    private string _physicalCount = string.Empty;
    private string _reason = string.Empty;
    private string _message = string.Empty;

    public ObservableCollection<Product> SearchResults { get; } = new();
    public ObservableCollection<InventoryBatchOption> Batches { get; } = new();

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
                OnPropertyChanged(nameof(SystemQuantity));
                RecalculateDelta();
            }
        }
    }

    /// <summary>Screen §3절 "System Quantity — 자동 표시".</summary>
    public int SystemQuantity => SelectedBatch?.CurrentQuantity ?? 0;

    public string PhysicalCount
    {
        get => _physicalCount;
        set
        {
            if (SetProperty(ref _physicalCount, value))
            {
                RecalculateDelta();
            }
        }
    }

    private int? _delta;
    public int? Delta
    {
        get => _delta;
        private set => SetProperty(ref _delta, value);
    }

    public string Reason
    {
        get => _reason;
        set => SetProperty(ref _reason, value);
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

    public AdjustmentViewModel(
        IProductRepository productRepository,
        IInventoryRepository inventoryRepository,
        IAdjustmentService adjustmentService,
        string facilityId,
        string userId)
    {
        _productRepository = productRepository;
        _inventoryRepository = inventoryRepository;
        _adjustmentService = adjustmentService;
        _facilityId = facilityId;
        _userId = userId;

        SearchCommand = new RelayCommand(async _ => await ExecuteSearchAsync());
        SaveCommand = new RelayCommand(async _ => await ExecuteSaveAsync(allowZeroDelta: false));
        CancelCommand = new RelayCommand(_ => NavigateBack?.Invoke());
    }

    /// <summary>
    /// F-04: USB HID 스캐너는 Enter 키를 전송하는 키보드로 동작하므로,
    /// 이 트리거만으로 스캐너 입력도 자동으로 처리된다.
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

    private async Task LoadBatchesAsync()
    {
        Batches.Clear();
        SelectedBatch = null;

        if (SelectedProduct is null)
        {
            return;
        }

        var batches = await _inventoryRepository.GetBatchesForProductAsync(SelectedProduct.ProductId, _facilityId);

        foreach (var batch in batches)
        {
            Batches.Add(batch);
        }
    }

    private void RecalculateDelta()
    {
        if (SelectedBatch is null || !int.TryParse(PhysicalCount, out var physicalCount))
        {
            Delta = null;
            return;
        }

        Delta = physicalCount - SystemQuantity;
    }

    private async Task ExecuteSaveAsync(bool allowZeroDelta)
    {
        Message = string.Empty;

        if (SelectedProduct is null)
        {
            Message = "Please select a product.";
            return;
        }

        if (SelectedBatch is null)
        {
            Message = "Please select a batch number.";
            return;
        }

        if (!int.TryParse(PhysicalCount, out var physicalCount))
        {
            Message = "Please enter the physical count.";
            return;
        }

        var result = await _adjustmentService.SaveAdjustmentAsync(
            _facilityId, SelectedProduct.ProductId, _userId,
            SelectedBatch.InventoryId, SelectedBatch.BatchNumber, SelectedBatch.ExpiryDate,
            SystemQuantity, physicalCount, Reason, allowZeroDelta);

        if (result.IsSuccess)
        {
            ResetForm();
            Message = "Adjustment saved successfully.";
        }
        else if (result.RequiresConfirmation)
        {
            var confirm = MessageBox.Show(result.Message, "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirm == MessageBoxResult.Yes)
            {
                await ExecuteSaveAsync(allowZeroDelta: true);
            }
        }
        else if (result.IsConcurrencyConflict)
        {
            Message = result.Message!;
            await LoadBatchesAsync();
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
        Batches.Clear();
        SelectedBatch = null;
        PhysicalCount = string.Empty;
        Reason = string.Empty;
        Delta = null;
    }
}