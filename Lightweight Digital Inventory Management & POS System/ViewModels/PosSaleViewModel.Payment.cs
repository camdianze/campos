using Lightweight_Digital_Inventory_Management___POS_System.ViewModels.Base;
using PharmaPOS.Domain.Enums;
using System.Windows;

namespace Lightweight_Digital_Inventory_Management___POS_System.ViewModels;

/// <summary>
/// PosSaleViewModel의 나머지 절반: 결제(Payment) 및 판매 확정(Confirm Sale) 관련 로직.
/// 파일이 길어져서 partial class로 두 파일에 나누어 관리한다.
/// </summary>
public partial class PosSaleViewModel
{
    private PaymentMethod? _selectedPaymentMethod;
    private string _cashTendered = string.Empty;
    private string _notes = string.Empty;

    public PaymentMethod? SelectedPaymentMethod
    {
        get => _selectedPaymentMethod;
        set
        {
            if (SetProperty(ref _selectedPaymentMethod, value))
            {
                OnPropertyChanged(nameof(IsCashPayment));
            }
        }
    }

    public IReadOnlyList<PaymentMethod> AvailablePaymentMethods { get; } = Enum.GetValues<PaymentMethod>();

    /// <summary>Cash 선택 시에만 Cash Tendered 입력칸을 보여준다 (Screen §3.2절).</summary>
    public bool IsCashPayment => SelectedPaymentMethod == PaymentMethod.Cash;

    public string CashTendered
    {
        get => _cashTendered;
        set
        {
            if (SetProperty(ref _cashTendered, value))
            {
                OnPropertyChanged(nameof(ChangeDue));
            }
        }
    }

    public string Notes
    {
        get => _notes;
        set => SetProperty(ref _notes, value);
    }

    /// <summary>Sale Cart 내 상품별 금액 합계 (Screen §4.4절).</summary>
    public decimal TotalAmount => Cart.Sum(item => item.LineTotal);

    /// <summary>Cash Tendered − Total Amount (Screen §4.4절). 계산 불가하면 null.</summary>
    public decimal? ChangeDue
    {
        get
        {
            if (!decimal.TryParse(CashTendered, out var tendered))
            {
                return null;
            }

            return tendered - TotalAmount;
        }
    }

    public RelayCommand ConfirmSaleCommand { get; private set; } = null!;
    public RelayCommand CancelSaleCommand { get; private set; } = null!;

    /// <summary>판매 완료 시 발생. View가 구독해서 화면을 초기화하거나 이동한다.</summary>
    public event Action? SaleCompleted;

    /// <summary>Cancel Sale 클릭 시 발생.</summary>
    public event Action? SaleCancelled;

    /// <summary>
    /// Step 75의 생성자 마지막에서 호출해서 이 파일의 커맨드를 초기화한다.
    /// (partial class 특성상, 생성자는 한쪽 파일에만 존재해야 하므로 이렇게 연결한다.)
    /// </summary>
    private void InitializePaymentCommands()
    {
        ConfirmSaleCommand = new RelayCommand(async _ => await ExecuteConfirmSaleAsync(acknowledgeWarning: false));
        CancelSaleCommand = new RelayCommand(_ => ExecuteCancelSale());
    }

    /// <summary>
    /// Cart의 내용이 바뀔 때마다(Add/Remove) 호출해서 계산 속성들의 화면 갱신을 알린다.
    /// </summary>
    private void RaiseTotalsChanged()
    {
        OnPropertyChanged(nameof(TotalAmount));
        OnPropertyChanged(nameof(ChangeDue));
    }

    private async Task ExecuteConfirmSaleAsync(bool acknowledgeWarning)
    {
        Message = string.Empty;

        decimal? cashTenderedValue = null;
        if (IsCashPayment)
        {
            if (string.IsNullOrWhiteSpace(CashTendered))
            {
                Message = "Please enter the cash tendered.";
                return;
            }

            if (!decimal.TryParse(CashTendered, out var parsed))
            {
                Message = "Please enter the cash tendered.";
                return;
            }

            cashTenderedValue = parsed;
        }

        var result = await _saleService.ConfirmSaleAsync(
            _facilityId, _userId, Cart.ToList(), SelectedPaymentMethod, cashTenderedValue, Notes, acknowledgeWarning);

        if (result.IsSuccess)
        {
            await PrintReceiptAndCompleteAsync();
        }
        else if (result.RequiresConfirmation)
        {
            var confirm = MessageBox.Show(result.Message, "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (confirm == MessageBoxResult.Yes)
            {
                await ExecuteConfirmSaleAsync(acknowledgeWarning: true);
            }
        }
        else
        {
            Message = result.Message!;
        }
    }

    private async Task PrintReceiptAndCompleteAsync()
    {
        // 화면 초기화(ResetSaleForm) 전에, 영수증에 필요한 값들을 먼저 스냅샷으로 저장한다.
        var cartSnapshot = Cart.ToList();
        var totalAmount = TotalAmount;
        var changeDue = ChangeDue;
        decimal? cashTenderedSnapshot = IsCashPayment && decimal.TryParse(CashTendered, out var tendered)
            ? tendered
            : null;

        // 판매 확정 성공 후 화면을 먼저 초기화한다 (Screen §4.1절 17~18단계:
        // 판매 완료 메시지 표시 후 초기 상태로 이동).
        ResetSaleForm();
        Message = "Sale completed successfully.";

        // 영수증 출력은 판매 확정과 완전히 분리된 단계이며,
        // 실패해도 판매 자체는 이미 완료된 상태이다 (Screen §5절 원칙).
        var printResult = await _receiptPrintingService.PrintReceiptAsync(
            cartSnapshot, totalAmount, cashTenderedSnapshot, changeDue);

        if (!printResult.IsSuccess)
        {
            Message = "Sale completed, but receipt could not be printed.";
        }

        SaleCompleted?.Invoke();
    }
    private void ExecuteCancelSale()
    {
        ResetSaleForm();
        SaleCancelled?.Invoke();
    }

    private void ResetSaleForm()
    {
        Cart.Clear();
        SearchTerm = string.Empty;
        SearchResults.Clear();
        SelectedProduct = null;
        Batches.Clear();
        SelectedBatch = null;
        Quantity = "1";
        UnitPrice = string.Empty;
        SelectedPaymentMethod = null;
        CashTendered = string.Empty;
        Notes = string.Empty;
        RaiseTotalsChanged();
    }
}