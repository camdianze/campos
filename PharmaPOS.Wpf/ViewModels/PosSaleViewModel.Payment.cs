using Lightweight_Digital_Inventory_Management___POS_System.ViewModels.Base;
using PharmaPOS.Application.Counselling;
using PharmaPOS.Application.Inventory;
using PharmaPOS.Application.Receipts;
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
    private string _cashTenderedRiel = string.Empty;
    private string _notes = string.Empty;

    // 통화 설정(환율·반올림 단위·리엘 표시 여부). 화면을 열 때 한 번 읽는다 —
    // 판매마다 다시 읽으면 계산대가 느려지고, 환율이 판매 도중 바뀌면 받은 돈과
    // 거스름돈이 서로 다른 환율로 계산된다.
    private decimal _exchangeRate;
    private int _rielRounding = 100;
    private bool _showRiel;

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

    /// <summary>
    /// 열거형 전체가 아니라 계산대에서 받는 것만 보여준다.
    /// 무엇을 받는지는 영업 방침이라 Application 쪽(PaymentMethods)이 정한다.
    /// </summary>
    public IReadOnlyList<PaymentMethod> AvailablePaymentMethods { get; } = PaymentMethods.OfferedAtTill;

    /// <summary>Cash 선택 시에만 Cash Tendered 입력칸을 보여준다 (Screen §3.2절).</summary>
    public bool IsCashPayment => SelectedPaymentMethod == PaymentMethod.Cash;

    public string CashTendered
    {
        get => _cashTendered;
        set
        {
            if (SetProperty(ref _cashTendered, value))
            {
                RaiseTenderChanged();
            }
        }
    }

    /// <summary>
    /// 손님이 리엘로 낸 금액. 달러 칸과 함께 쓴다 — 캄보디아에서는 두 통화를 섞어
    /// 내는 것이 보통이라, 둘 중 하나만 받으면 직원이 암산을 해야 한다.
    /// </summary>
    public string CashTenderedRiel
    {
        get => _cashTenderedRiel;
        set
        {
            if (SetProperty(ref _cashTenderedRiel, value))
            {
                RaiseTenderChanged();
            }
        }
    }

    /// <summary>
    /// 리엘 칸과 환산 표시를 보여줄지. 환율이 없으면 환산할 방법이 없고,
    /// 리엘을 쓰지 않기로 한 약국(currency.showRiel = false)에는 방해만 된다.
    /// 영수증이 리엘 줄을 낼지 정하는 규칙과 같은 값을 쓴다.
    /// </summary>
    public bool IsRielAccepted => _showRiel && _exchangeRate > 0;

    /// <summary>화면을 열 때 통화 설정을 읽는다. 못 읽으면 리엘 칸 없이 종전대로 동작한다.</summary>
    public async Task LoadCurrencySettingsAsync()
    {
        try
        {
            var settings = await _receiptSettingsService.GetAsync();

            _exchangeRate = settings.ExchangeRate;
            _rielRounding = settings.RielRounding;
            _showRiel = settings.ShowRiel;
        }
        catch (Exception)
        {
            _showRiel = false;
        }

        OnPropertyChanged(nameof(IsRielAccepted));
        OnPropertyChanged(nameof(ExchangeRateHint));
        RaiseTotalsChanged();
    }

    public string ExchangeRateHint => IsRielAccepted
        ? $"1 USD = {_exchangeRate.ToString("N0", System.Globalization.CultureInfo.InvariantCulture)} "
          + RielConverter.RielSymbol
        : string.Empty;

    public string Notes
    {
        get => _notes;
        set => SetProperty(ref _notes, value);
    }

    /// <summary>Sale Cart 내 상품별 금액 합계 (Screen §4.4절).</summary>
    public decimal TotalAmount => Cart.Sum(item => item.LineTotal);

    /// <summary>합계의 리엘 환산액. 손님이 리엘로 낼 때 얼마를 받아야 하는지다.</summary>
    public string TotalInRielDisplay => IsRielAccepted
        ? RielConverter.Format(TotalAmount, _exchangeRate, _rielRounding)
        : string.Empty;

    /// <summary>
    /// 지금까지 받은 현금. 달러 칸과 리엘 칸이 모두 비어 있으면 null이다.
    /// 계산은 Application의 <see cref="MixedCashTender"/>가 한다 — 환율과 반올림이
    /// 섞인 돈 계산이라 테스트가 걸리는 자리에 있어야 한다.
    /// </summary>
    private MixedCashTender? CurrentTender
    {
        get
        {
            var hasUsd = decimal.TryParse(CashTendered, out var usd);
            var hasRiel = long.TryParse(CashTenderedRiel, out var riel);

            if (!hasUsd && !hasRiel)
            {
                return null;
            }

            return MixedCashTender.Calculate(
                TotalAmount,
                hasUsd ? usd : 0m,
                hasRiel ? riel : 0L,
                _exchangeRate,
                _rielRounding);
        }
    }

    /// <summary>Cash Tendered − Total Amount (Screen §4.4절). 계산 불가하면 null.</summary>
    public decimal? ChangeDue => CurrentTender?.ChangeUsd;

    /// <summary>
    /// 실제로 건네줄 거스름돈. 달러 동전이 돌지 않아 센트 단위 거스름은 리엘로만
    /// 줄 수 있으므로, 계산대가 보는 값은 이쪽이다.
    /// </summary>
    public string ChangeDueRielDisplay
    {
        get
        {
            if (!IsRielAccepted || CurrentTender is not { } tender || !tender.IsEnough)
            {
                return string.Empty;
            }

            return RielConverter.Format(tender.ChangeRiel);
        }
    }

    /// <summary>
    /// 두 칸을 합쳐 얼마를 받았는지. 달러와 리엘을 섞어 받으면 직원이 합을 암산하게
    /// 되는데, 그 암산이 틀리면 그날 시재가 맞지 않는다.
    /// </summary>
    public string TenderedSummary
    {
        get
        {
            if (CurrentTender is not { } tender || tender.RielTendered == 0)
            {
                return string.Empty;
            }

            var total = tender.TotalTenderedUsd.ToString(
                "N2", System.Globalization.CultureInfo.InvariantCulture);

            return $"received $= {total}";
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
        OnPropertyChanged(nameof(TotalInRielDisplay));
        RaiseTenderChanged();
    }

    private void RaiseTenderChanged()
    {
        OnPropertyChanged(nameof(ChangeDue));
        OnPropertyChanged(nameof(ChangeDueRielDisplay));
        OnPropertyChanged(nameof(TenderedSummary));
    }

    private async Task ExecuteConfirmSaleAsync(bool acknowledgeWarning)
    {
        Message = string.Empty;

        decimal? cashTenderedValue = null;

        if (IsCashPayment)
        {
            // 둘 중 한 칸만 채워도 된다. 달러만 받는 판매도, 리엘만 받는 판매도 흔하다.
            if (string.IsNullOrWhiteSpace(CashTendered) && string.IsNullOrWhiteSpace(CashTenderedRiel))
            {
                Message = "Please enter the cash tendered.";
                return;
            }

            if (!string.IsNullOrWhiteSpace(CashTendered) && !decimal.TryParse(CashTendered, out _))
            {
                Message = "Cash tendered in USD must be a number.";
                return;
            }

            if (!string.IsNullOrWhiteSpace(CashTenderedRiel) && !long.TryParse(CashTenderedRiel, out _))
            {
                Message = "Cash tendered in riel must be a whole number.";
                return;
            }

            if (CurrentTender is not { } tender)
            {
                Message = "Please enter the cash tendered.";
                return;
            }

            // 모자란 채로 확정되면 판매는 기록되는데 돈은 덜 받은 상태가 된다.
            // 원장에는 흔적이 남지 않으므로 여기서 막는다.
            if (!tender.IsEnough)
            {
                var shortfall = tender.ShortfallUsd.ToString(
                    "N2", System.Globalization.CultureInfo.InvariantCulture);

                Message = $"Cash tendered is ${shortfall} short of the total.";
                return;
            }

            // 원장과 영수증에는 두 통화를 합친 달러 금액이 들어간다. 판매 한 건의
            // 받은 돈은 한 값이어야 하고, 이 앱의 금액은 전부 달러 기준이다.
            cashTenderedValue = tender.TotalTenderedUsd;
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
        var paymentMethod = SelectedPaymentMethod?.ToString();
        decimal? cashTenderedSnapshot = IsCashPayment ? CurrentTender?.TotalTenderedUsd : null;

        // 영수증 번호는 (거래 시각, 사용자)로 판매를 식별한다. 판매 내역에서 재출력할 때
        // 같은 번호가 다시 나오게 하려면 방금 저장된 그 시각을 그대로 써야 한다.
        var transactionTime = confirmedLines.Count > 0
            ? confirmedLines[0].TransactionTime
            : DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // 판매 확정 성공 후 화면을 먼저 초기화한다 (Screen §4.1절 17~18단계:
        // 판매 완료 메시지 표시 후 초기 상태로 이동).
        ResetSaleForm();
        Message = "Sale completed successfully.";

        // 영수증 출력은 판매 확정과 완전히 분리된 단계이며,
        // 실패해도 판매 자체는 이미 완료된 상태이다 (Screen §5절 원칙).
        // 프린터가 없거나 드라이버가 죽어도 여기서 예외가 올라오지 않는다 — ThermalTextPrinter가
        // 안에서 삼키고 실패만 돌려준다. 계산대가 종이 때문에 멈추면 안 된다.
        //
        // 종이를 낼지는 설정이 정한다. Never면 묻지도 않고 넘어가고, Ask면 한 번 묻는다.
        // 어느 쪽이든 영수증 번호는 판매 확정 때 이미 발급됐다 — 재출력·환불 대조는
        // 종이가 나갔는지와 무관하게 되어야 한다.
        if (await ShouldPrintReceiptAsync())
        {
            var printResult = await _receiptPrintingService.PrintReceiptAsync(new ReceiptPrintRequest
            {
                Lines = cartSnapshot,
                TotalAmount = totalAmount,
                TransactionTime = transactionTime,
                UserId = _userId,
                Username = _username,
                PaymentMethod = paymentMethod,
                CashTendered = cashTenderedSnapshot,
                ChangeDue = changeDue
            });

            if (!printResult.IsSuccess)
            {
                // 판매가 끝났다는 말이 먼저 와야 한다. 계산대에서는 이 줄만 보고 판단한다.
                Message = "Sale completed. The receipt did not print — check the printer.";
            }
        }

        await HandleAntibioticCounsellingAsync(confirmedLines);

        SaleCompleted?.Invoke();
    }

    /// <summary>
    /// 설정의 영수증 인쇄 방식에 따라 종이를 낼지 정한다.
    /// 설정을 못 읽으면 종전 동작(언제나 인쇄)으로 간다 — 설정 하나 때문에 영수증이
    /// 조용히 사라지는 쪽이 더 나쁘다.
    /// </summary>
    private async Task<bool> ShouldPrintReceiptAsync()
    {
        ReceiptPrintMode mode;

        try
        {
            mode = (await _receiptSettingsService.GetAsync()).PrintMode;
        }
        catch (Exception)
        {
            mode = ReceiptPrintMode.Always;
        }

        return mode switch
        {
            ReceiptPrintMode.Never => false,
            ReceiptPrintMode.Ask => AppDialog.Confirm(
                "Receipt", "Print a receipt for this sale?",
                confirmText: "Print", cancelText: "Skip"),
            _ => true
        };
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
        CashTenderedRiel = string.Empty;
        Notes = string.Empty;
        RaiseTotalsChanged();
    }
}