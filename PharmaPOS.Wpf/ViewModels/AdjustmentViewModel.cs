using System.Collections.ObjectModel;
using System.Windows;
using PharmaPOS.Application.Inventory;
using PharmaPOS.Application.Repositories;
using PharmaPOS.Domain.Entities;
using PharmaPOS.Domain.Enums;
using Lightweight_Digital_Inventory_Management___POS_System.ViewModels.Base;
using Lightweight_Digital_Inventory_Management___POS_System.Views;

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
    private string _physicalBoxCount = "0";
    private string _physicalUnitCount = string.Empty;
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
                OnPropertyChanged(nameof(IsBoxedProductSelected));
                OnPropertyChanged(nameof(UnitsPerBox));
                OnPropertyChanged(nameof(PhysicalUnitCountLabel));
                OnPropertyChanged(nameof(SystemQuantityBreakdown));
                OnPropertyChanged(nameof(HasSelectedProduct));
                OnPropertyChanged(nameof(SelectedProductName));
                OnPropertyChanged(nameof(SelectedProductDetail));
                RecalculateDelta();
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
                OnPropertyChanged(nameof(SystemQuantityBreakdown));
                OnPropertyChanged(nameof(SelectedProductDetail));
                RecalculateDelta();
            }
        }
    }

    /// <summary>고른 상품이 있는지. 화면 위쪽의 상품 이름 띠를 보일지 결정한다.</summary>
    public bool HasSelectedProduct => SelectedProduct is not null;

    public string SelectedProductName => SelectedProduct?.ProductName ?? string.Empty;

    /// <summary>
    /// 이름 아래 한 줄. 성분명·규격에 고른 배치를 덧붙인다.
    /// 이름이 비슷한 약이 많아, 실사 값을 적기 전에 무엇을 세고 있는지 보여야 한다.
    /// </summary>
    public string SelectedProductDetail
    {
        get
        {
            if (SelectedProduct is not { } product)
            {
                return string.Empty;
            }

            var parts = new List<string>();

            if (!string.IsNullOrWhiteSpace(product.GenericName))
            {
                parts.Add(product.GenericName!);
            }

            if (!string.IsNullOrWhiteSpace(product.Strength))
            {
                parts.Add(product.Strength!);
            }

            parts.Add(SelectedBatch is { } batch
                ? $"Batch {batch.BatchNumber}"
                : "Select a batch");

            return string.Join(" · ", parts);
        }
    }

    /// <summary>Screen §3절 "System Quantity — 자동 표시".</summary>
    public int SystemQuantity => SelectedBatch?.CurrentQuantity ?? 0;

    /// <summary>박스/낱개를 나눠 세는 상품인지. 아니면 박스 입력칸을 숨긴다.</summary>
    public bool IsBoxedProductSelected => SelectedProduct?.IsBoxedProduct == true;

    /// <summary>상품의 박스당 낱개 수. 화면 라벨에 그대로 쓴다.</summary>
    public int UnitsPerBox => SelectedProduct?.UnitsPerBox ?? 1;

    /// <summary>
    /// 전산 재고의 박스/낱개 내역. 실사 값을 적기 전에 무엇과 비교하는지 보이지 않으면
    /// 박스 칸에 총량을 적는 식의 실수가 난다.
    /// </summary>
    public string SystemQuantityBreakdown
    {
        get
        {
            if (SelectedBatch is not { } batch || !IsBoxedProductSelected)
            {
                return string.Empty;
            }

            return $"System: {batch.BoxQuantity} box(es) of {UnitsPerBox} + {batch.UnitQuantity} loose unit(s).";
        }
    }

    /// <summary>실사한 박스 수. 박스/낱개 구분이 없는 상품은 0으로 둔다.</summary>
    public string PhysicalBoxCount
    {
        get => _physicalBoxCount;
        set
        {
            if (SetProperty(ref _physicalBoxCount, value))
            {
                RecalculateDelta();
            }
        }
    }

    /// <summary>
    /// 낱개 실사 칸의 라벨. 박스 칸이 함께 보일 때만 "Loose Units"로 갈라 부르고,
    /// 그렇지 않으면 종전 화면과 같은 "Physical Count"를 그대로 쓴다.
    /// </summary>
    public string PhysicalUnitCountLabel =>
        IsBoxedProductSelected ? "Physical Count — Loose Units" : "Physical Count";

    /// <summary>실사한 낱개 수. 박스/낱개 구분이 없는 상품은 여기에 전량을 적는다.</summary>
    public string PhysicalUnitCount
    {
        get => _physicalUnitCount;
        set
        {
            if (SetProperty(ref _physicalUnitCount, value))
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
        if (SelectedBatch is null || !TryParsePhysicalCount(out var physicalCount))
        {
            Delta = null;
            return;
        }

        Delta = physicalCount - SystemQuantity;
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

        physicalCount = BoxUnitMath.ToTotalUnits(boxCount, unitCount, UnitsPerBox);
        return true;
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

        if (!TryParsePhysicalCount(out _))
        {
            Message = "Please enter the physical count.";
            return;
        }

        // 위에서 파싱이 통과했으니 두 칸 모두 숫자다. 박스 칸은 비워 두면 0으로 본다.
        int.TryParse(PhysicalBoxCount, out var physicalBoxCount);
        int.TryParse(PhysicalUnitCount, out var physicalUnitCount);

        var result = await _adjustmentService.SaveAdjustmentAsync(
            _facilityId, SelectedProduct.ProductId, _userId,
            SelectedBatch.InventoryId, SelectedBatch.BatchNumber, SelectedBatch.ExpiryDate,
            SystemQuantity, physicalBoxCount, physicalUnitCount, UnitsPerBox,
            Reason, allowZeroDelta);

        if (result.IsSuccess)
        {
            ResetForm();
            Message = "Adjustment saved successfully.";
        }
        else if (result.RequiresConfirmation)
        {
            var confirm = AppDialog.Confirm("Confirm", result.Message!);
            if (confirm)
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
        PhysicalBoxCount = "0";
        PhysicalUnitCount = string.Empty;
        Reason = string.Empty;
        Delta = null;
    }
}