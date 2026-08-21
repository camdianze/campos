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
    private string _message = string.Empty;

    public string ProductName => _selectedProduct.ProductName;

    public string InternalBarcode
    {
        get => _internalBarcode;
        private set
        {
            if (SetProperty(ref _internalBarcode, value))
            {
                // Generate로 방금 만든 값이 아래 안내문에도 반영돼야 한다.
                OnPropertyChanged(nameof(LabelPlanHint));
            }
        }
    }

    public string LabelQuantity
    {
        get => _labelQuantity;
        set => SetProperty(ref _labelQuantity, value);
    }

    /// <summary>
    /// 무엇이 몇 장 나갈지 누르기 전에 알려 준다.
    ///
    /// 두 가지를 미리 보여야 한다. 하나는 <b>어느 값이 찍히는지</b> — 유통사 바코드가 있으면
    /// 내부 바코드가 아니라 그쪽이 나가는데, 화면 위쪽에는 내부 바코드가 적혀 있어 어긋나 보인다.
    /// 다른 하나는 소분 상품이면 <b>장수가 두 배</b>라는 점이다.
    /// </summary>
    public string LabelPlanHint
    {
        get
        {
            // 내부 바코드는 화면에서 방금 만들었을 수 있으므로 상품 객체가 아니라 이 화면의 값을 본다.
            var hasManufacturerBarcode = !string.IsNullOrWhiteSpace(_selectedProduct.Barcode);

            var code = hasManufacturerBarcode ? _selectedProduct.Barcode! : InternalBarcode;

            if (string.IsNullOrWhiteSpace(code))
            {
                return "No barcode yet — press Generate first.";
            }

            var source = hasManufacturerBarcode ? "manufacturer barcode" : "internal barcode";

            var plan = $"Prints {code} ({source}), one label per copy.";

            // 낱개 바코드는 내부 바코드에서 나온다. 화면에서 방금 만든 경우까지 반영하려면 여기서 붙인다.
            if (!_selectedProduct.IsBoxedProduct || string.IsNullOrWhiteSpace(InternalBarcode))
            {
                return plan;
            }

            return plan
                   + $" A second label per copy carries {InternalBarcode}{Product.UnitBarcodeSuffix} "
                   + $"for a single {_selectedProduct.Unit}.";
        }
    }

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
            _selectedProduct.ProductId, quantity);

        Message = result.Message!;
    }
}