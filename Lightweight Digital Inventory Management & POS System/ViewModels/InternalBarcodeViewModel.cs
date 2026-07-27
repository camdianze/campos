using PharmaPOS.Application.Products;
using PharmaPOS.Domain.Entities;
using Lightweight_Digital_Inventory_Management___POS_System.ViewModels.Base;

namespace Lightweight_Digital_Inventory_Management___POS_System.ViewModels;

/// <summary>
/// 내부 바코드 생성/출력 화면(SCR-BARCODE-013)의 ViewModel.
/// </summary>
public class InternalBarcodeViewModel : ViewModelBase
{
    private readonly IInternalBarcodeService _internalBarcodeService;

    private readonly Product _selectedProduct;

    private string _internalBarcode;
    private string _labelQuantity = "1";
    private string? _selectedPrinter;
    private string _message = string.Empty;

    public string ProductName => _selectedProduct.ProductName;

    public string InternalBarcode
    {
        get => _internalBarcode;
        private set => SetProperty(ref _internalBarcode, value);
    }

    public string LabelQuantity
    {
        get => _labelQuantity;
        set => SetProperty(ref _labelQuantity, value);
    }

    public string? SelectedPrinter
    {
        get => _selectedPrinter;
        set => SetProperty(ref _selectedPrinter, value);
    }

    /// <summary>
    /// 연결된 프린터 목록. 실제 프린터 탐색 기능은 아직 없으므로(하드웨어 미정),
    /// 지금은 화면 확인용 더미 목록을 제공한다.
    /// </summary>
    public IReadOnlyList<string> AvailablePrinters { get; } = new[]
    {
        "Label Printer 1 (Placeholder)",
        "Label Printer 2 (Placeholder)"
    };

    public string Message
    {
        get => _message;
        set => SetProperty(ref _message, value);
    }

    public RelayCommand GenerateBarcodeCommand { get; }
    public RelayCommand PrintLabelCommand { get; }
    public RelayCommand BackCommand { get; }

    public event Action? NavigateBack;

    public InternalBarcodeViewModel(IInternalBarcodeService internalBarcodeService, Product selectedProduct)
    {
        _internalBarcodeService = internalBarcodeService;
        _selectedProduct = selectedProduct;
        _internalBarcode = selectedProduct.InternalBarcode ?? string.Empty;

        GenerateBarcodeCommand = new RelayCommand(async _ => await ExecuteGenerateAsync());
        PrintLabelCommand = new RelayCommand(async _ => await ExecutePrintAsync());
        BackCommand = new RelayCommand(_ => NavigateBack?.Invoke());
    }

    private async Task ExecuteGenerateAsync()
    {
        Message = string.Empty;

        if (!string.IsNullOrWhiteSpace(InternalBarcode))
        {
            Message = "Internal barcode already exists.";
            return;
        }

        var result = await _internalBarcodeService.GenerateOrGetInternalBarcodeAsync(_selectedProduct.ProductId);

        if (result.IsSuccess)
        {
            InternalBarcode = result.InternalBarcode!;
        }
        else
        {
            Message = result.Message!;
        }
    }

    private async Task ExecutePrintAsync()
    {
        Message = string.Empty;

        if (!int.TryParse(LabelQuantity, out var quantity))
        {
            Message = "Please enter the label quantity.";
            return;
        }

        var result = await _internalBarcodeService.PrintLabelAsync(
            _selectedProduct.ProductId, quantity, SelectedPrinter);

        Message = result.Message!;
    }
}