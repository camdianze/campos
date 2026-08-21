using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows;
using Microsoft.Win32;
using PharmaPOS.Application.Inventory;
using PharmaPOS.Domain.Enums;
using Lightweight_Digital_Inventory_Management___POS_System.ViewModels.Base;
using Lightweight_Digital_Inventory_Management___POS_System.Views;

namespace Lightweight_Digital_Inventory_Management___POS_System.ViewModels;

/// <summary>
/// 판매 내역 화면(SCR-SALES-007)의 ViewModel.
/// </summary>
public class SalesHistoryViewModel : ViewModelBase
{
    private readonly ISalesHistoryService _salesHistoryService;
    private readonly IReceiptPrintingService _receiptPrintingService;
    private readonly string _facilityId;

    /// <summary>환불 창을 만들 때 필요하다 — 시설과 "환불을 누른 사람"을 함께 넘겨야 한다.</summary>
    public string FacilityId => _facilityId;

    /// <summary>지금 로그인한 사용자. 원 판매자와 다를 수 있다.</summary>
    public string UserId { get; }

    private DateTime? _dateFrom;
    private DateTime? _dateTo;
    private string _searchTerm = string.Empty;
    private PaymentMethod? _selectedPaymentMethod;
    private SalesHistoryLineItem? _selectedLine;
    private string _message = string.Empty;

    public ObservableCollection<SalesHistoryLineItem> Results { get; } = new();

    public DateTime? DateFrom
    {
        get => _dateFrom;
        set => SetProperty(ref _dateFrom, value);
    }

    public DateTime? DateTo
    {
        get => _dateTo;
        set => SetProperty(ref _dateTo, value);
    }

    public string SearchTerm
    {
        get => _searchTerm;
        set => SetProperty(ref _searchTerm, value);
    }

    public PaymentMethod? SelectedPaymentMethod
    {
        get => _selectedPaymentMethod;
        set => SetProperty(ref _selectedPaymentMethod, value);
    }

    /// <summary>null="All" 옵션을 포함하기 위해 nullable 리스트로 구성한다.</summary>
    public IReadOnlyList<PaymentMethod?> AvailablePaymentMethods { get; }

    public SalesHistoryLineItem? SelectedLine
    {
        get => _selectedLine;
        set => SetProperty(ref _selectedLine, value);
    }

    public string Message
    {
        get => _message;
        set => SetProperty(ref _message, value);
    }

    public RelayCommand SearchCommand { get; }
    public RelayCommand ResetCommand { get; }
    public RelayCommand ViewDetailCommand { get; }
    public RelayCommand ReprintReceiptCommand { get; }
    public RelayCommand RefundCommand { get; }
    public RelayCommand ExportCommand { get; }
    public RelayCommand BackCommand { get; }

    public event Action? NavigateBack;

    /// <summary>환불 창을 띄워 달라는 요청. 창을 여는 일은 코드 비하인드가 한다.</summary>
    public event Action<SalesHistoryLineItem>? RequestRefundDialog;

    public SalesHistoryViewModel(
        ISalesHistoryService salesHistoryService,
        IReceiptPrintingService receiptPrintingService,
        string facilityId,
        string userId)
    {
        _salesHistoryService = salesHistoryService;
        _receiptPrintingService = receiptPrintingService;
        _facilityId = facilityId;
        UserId = userId;

        var methods = new List<PaymentMethod?> { null };
        methods.AddRange(Enum.GetValues<PaymentMethod>().Cast<PaymentMethod?>());
        AvailablePaymentMethods = methods;

        SearchCommand = new RelayCommand(async _ => await ExecuteSearchAsync());
        ResetCommand = new RelayCommand(_ => ExecuteReset());
        ViewDetailCommand = new RelayCommand(_ => ExecuteViewDetail());
        ReprintReceiptCommand = new RelayCommand(async _ => await ExecuteReprintReceiptAsync());
        RefundCommand = new RelayCommand(_ => ExecuteRefund());
        ExportCommand = new RelayCommand(_ => ExecuteExport());
        BackCommand = new RelayCommand(_ => NavigateBack?.Invoke());

        _ = ExecuteSearchAsync();
    }

    public async Task ExecuteSearchAsync()
    {
        Message = string.Empty;

        var result = await _salesHistoryService.SearchAsync(
            _facilityId, DateFrom, DateTo, SearchTerm, SelectedPaymentMethod);

        if (!result.IsSuccess)
        {
            Message = result.Message!;
            return;
        }

        Results.Clear();
        foreach (var item in result.Items!)
        {
            Results.Add(item);
        }

        Message = Results.Count == 0 ? "No sales records found." : string.Empty;
    }

    private void ExecuteReset()
    {
        DateFrom = null;
        DateTo = null;
        SearchTerm = string.Empty;
        SelectedPaymentMethod = null;
        _ = ExecuteSearchAsync();
    }

    private void ExecuteRefund()
    {
        Message = string.Empty;

        if (SelectedLine is null)
        {
            Message = "Please select a sales record.";
            return;
        }

        // 환불 행은 그 자체가 되돌린 기록이라 다시 되돌릴 게 없다.
        // 되돌리려면 원 판매 줄을 골라야 한다.
        if (SelectedLine.IsRefund)
        {
            Message = "Please select a sale, not a refund.";
            return;
        }

        RequestRefundDialog?.Invoke(SelectedLine);
    }

    private async void ExecuteViewDetail()
    {
        if (SelectedLine is null)
        {
            Message = "Please select a sales record.";
            return;
        }

        if (SelectedLine.IsRefund)
        {
            Message = "Please select a sale, not a refund.";
            return;
        }

        var group = await _salesHistoryService.GetTransactionGroupAsync(_facilityId, SelectedLine);

        var detail = new StringBuilder();
        detail.AppendLine($"Sold by: {SelectedLine.Username}");
        detail.AppendLine($"Payment: {SelectedLine.PaymentMethod}");
        detail.AppendLine("--------------------");

        decimal total = 0;
        decimal refunded = 0;

        foreach (var line in group)
        {
            detail.AppendLine($"{line.ProductName} x{line.Quantity} @ {line.UnitPrice} = {line.LineTotal}");
            total += line.LineTotal;

            if (line.RefundedQuantity > 0)
            {
                detail.AppendLine($"  refunded x{line.RefundedQuantity}");
                refunded += line.UnitPrice * line.RefundedQuantity;
            }
        }

        detail.AppendLine("--------------------");
        detail.AppendLine($"Total: {total}");

        // 환불이 있었다면 실제로 남은 금액까지 보여 준다 — 판매 금액만 보고
        // 서랍을 맞추면 돌려준 돈만큼 어긋난다.
        if (refunded > 0)
        {
            detail.AppendLine($"Refunded: -{refunded}");
            detail.AppendLine($"Net: {total - refunded}");
        }

        AppDialog.Show("Sale Detail", detail.ToString(), monospace: true);
    }

    private async Task ExecuteReprintReceiptAsync()
    {
        if (SelectedLine is null)
        {
            Message = "Please select a sales record.";
            return;
        }

        if (SelectedLine.IsRefund)
        {
            Message = "Please select a sale, not a refund.";
            return;
        }

        var group = await _salesHistoryService.GetTransactionGroupAsync(_facilityId, SelectedLine);

        var cartItems = group.Select(line => new SaleLineItem
        {
            ProductId = line.ProductId,
            ProductName = line.ProductName,
            InventoryId = string.Empty,
            BatchNumber = line.BatchNumber,
            ExpiryDate = 0,
            Quantity = line.Quantity,
            UnitPrice = line.UnitPrice,
            CostPrice = 0
        }).ToList();

        var totalAmount = cartItems.Sum(i => i.LineTotal);

        // 원래 판매의 시각과 사용자를 그대로 넘긴다. 그래야 처음 발급된 영수증 번호를
        // 다시 찾아 같은 번호로 나온다 — 재출력마다 번호가 새로 나가면 환불 대조가 깨진다.
        var printResult = await _receiptPrintingService.PrintReceiptAsync(new ReceiptPrintRequest
        {
            Lines = cartItems,
            TotalAmount = totalAmount,
            TransactionTime = SelectedLine.TransactionTime,
            UserId = SelectedLine.UserId,
            Username = SelectedLine.Username,
            PaymentMethod = SelectedLine.PaymentMethod
        });

        Message = printResult.IsSuccess
            ? string.Empty
            : "Receipt could not be reprinted.";
    }

    private void ExecuteExport()
    {
        if (Results.Count == 0)
        {
            Message = "No data to export.";
            return;
        }

        var dialog = new SaveFileDialog
        {
            Filter = "CSV files (*.csv)|*.csv",
            FileName = $"sales_history_{DateTime.Now:yyyyMMdd}.csv"
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            var builder = new StringBuilder();
            // 환불 행이 섞여 있으므로 Type 컬럼이 없으면 음수 수량의 정체를 알 수 없다.
            builder.AppendLine("Type,ProductName,BatchNumber,Quantity,UnitPrice,LineTotal,PaymentMethod,Username,TransactionTime");

            foreach (var item in Results)
            {
                var time = DateTimeOffset.FromUnixTimeMilliseconds(item.TransactionTime).ToLocalTime();
                var type = item.IsRefund ? "Refund" : "Sale";
                builder.AppendLine(
                    $"{type},{item.ProductName},{item.BatchNumber},{item.Quantity},{item.UnitPrice}," +
                    $"{item.LineTotal},{item.PaymentMethod},{item.Username},{time:yyyy-MM-dd HH:mm}");
            }

            File.WriteAllText(dialog.FileName, builder.ToString(), Encoding.UTF8);
            Message = "Export completed successfully.";
        }
        catch (Exception)
        {
            Message = "Export failed. Please try again.";
        }
    }
}