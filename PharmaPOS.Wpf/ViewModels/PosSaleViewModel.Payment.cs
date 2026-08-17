using Lightweight_Digital_Inventory_Management___POS_System.ViewModels.Base;
using PharmaPOS.Application.Counselling;
using PharmaPOS.Application.Inventory;
using Lightweight_Digital_Inventory_Management___POS_System.Views;
using PharmaPOS.Domain.Enums;

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
            await PrintReceiptAndCompleteAsync(result.ConfirmedLines);
        }
        else if (result.RequiresConfirmation)
        {
            if (AppDialog.Confirm("Confirm", result.Message!))
            {
                await ExecuteConfirmSaleAsync(acknowledgeWarning: true);
            }
        }
        else
        {
            Message = result.Message!;
        }
    }

    private async Task PrintReceiptAndCompleteAsync(IReadOnlyList<ConfirmedSaleLine> confirmedLines)
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

        await HandleAntibioticCounsellingAsync(confirmedLines);

        SaleCompleted?.Invoke();
    }

    /// <summary>
    /// 항생제 복약안내(AMR) 처리. 영수증과 마찬가지로 판매 확정과 완전히 분리된 단계이며,
    /// 여기서 무엇이 실패해도 이미 커밋된 판매를 되돌리지 않는다.
    /// 한 거래에 항생제가 여러 개면 상품별로 각각 출력한다.
    /// </summary>
    private async Task HandleAntibioticCounsellingAsync(IReadOnlyList<ConfirmedSaleLine> confirmedLines)
    {
        var candidates = await _counsellingService.PrepareAsync(confirmedLines);

        if (candidates.Count == 0)
        {
            return;
        }

        var failedCount = 0;

        foreach (var candidate in candidates)
        {
            // 항생제를 팔았다는 사실은 인쇄 설정과 무관하게 알린다. 용지가 나가느냐는
            // 그다음 문제다 — 프린터가 없어도 약사는 복약지도를 해야 하고, always로 두면
            // 종이만 조용히 나가서 계산대에서는 아무 일도 없었던 것처럼 보인다.
            var notice =
                "This product contains an antibiotic. Counsel the patient before handing it over."
                + $"\n\n{candidate.ProductName}"
                + $"\nWHO AWaRe group: {AwareGroupCodes.ToCode(candidate.AwareGroup)}";

            if (candidate.RequiresPrompt)
            {
                // ask: 같은 안내를 띄우면서 인쇄 여부까지 함께 묻는다.
                var printIt = AppDialog.Confirm(
                    "Antibiotic Counselling",
                    notice + "\n\nPrint the counselling sheet?",
                    confirmText: "Print",
                    cancelText: "Skip");

                if (!printIt)
                {
                    await _counsellingService.LogSkipAsync(
                        candidate, CounsellingService.SkipReasonPharmacist);
                    continue;
                }
            }
            else
            {
                // always: 안내만 확인받고 인쇄는 그대로 진행한다.
                AppDialog.Show("Antibiotic Counselling", notice);
            }

            var result = await _counsellingService.PrintAsync(candidate);

            if (!result.IsSuccess)
            {
                failedCount++;
            }
        }

        if (failedCount > 0)
        {
            Message = "Sale completed, but the antibiotic counselling sheet could not be printed.";
        }
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