using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows;
using Microsoft.Win32;
using PharmaPOS.Application.Inventory;
using PharmaPOS.Domain.Enums;
using Lightweight_Digital_Inventory_Management___POS_System.ViewModels.Base;

namespace Lightweight_Digital_Inventory_Management___POS_System.ViewModels;

/// <summary>
/// 판매 내역 화면(SCR-SALES-007)의 ViewModel.
/// </summary>
public class SalesHistoryViewModel : ViewModelBase
{
    private readonly ISalesHistoryService _salesHistoryService;
    private readonly IReceiptPrintingService _receiptPrintingService;
    private readonly string _facilityId;

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
    public RelayCommand ExportCommand { get; }
    public RelayCommand BackCommand { get; }

    public event Action? NavigateBack;

    public SalesHistoryViewModel(
        ISalesHistoryService salesHistoryService,
        IReceiptPrintingService receiptPrintingService,
        string facilityId)
    {
        _salesHistoryService = salesHistoryService;
        _receiptPrintingService = receiptPrintingService;
        _facilityId = facilityId;

        var methods = new List<PaymentMethod?> { null };
        methods.AddRange(Enum.GetValues<PaymentMethod>().Cast<PaymentMethod?>());
        AvailablePaymentMethods = methods;

        SearchCommand = new RelayCommand(async _ => await ExecuteSearchAsync());
        ResetCommand = new RelayCommand(_ => ExecuteReset());
        ViewDetailCommand = new RelayCommand(_ => ExecuteViewDetail());
        ReprintReceiptCommand = new RelayCommand(async _ => await ExecuteReprintReceiptAsync());
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

    private async void ExecuteViewDetail()
    {
        if (SelectedLine is null)
        {
            Message = "Please select a sales record.";
            return;
        }

        var group = await _salesHistoryService.GetTransactionGroupAsync(_facilityId, SelectedLine);

        var detail = new StringBuilder();
        detail.AppendLine($"Sold by: {SelectedLine.Username}");
        detail.AppendLine($"Payment: {SelectedLine.PaymentMethod}");
        detail.AppendLine("--------------------");

        decimal total = 0;
        foreach (var line in group)
        {
            detail.AppendLine($"{line.ProductName} x{line.Quantity} @ {line.UnitPrice} = {line.LineTotal}");
            total += line.LineTotal;
        }

        detail.AppendLine("--------------------");
        detail.AppendLine($"Total: {total}");

        MessageBox.Show(detail.ToString(), "Sale Detail");
    }

    private async Task ExecuteReprintReceiptAsync()
    {
        if (SelectedLine is null)
        {
            Message = "Please select a sales record.";
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

        var printResult = await _receiptPrintingService.PrintReceiptAsync(cartItems, totalAmount, null, null);

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
            builder.AppendLine("ProductName,BatchNumber,Quantity,UnitPrice,LineTotal,PaymentMethod,Username,TransactionTime");

            foreach (var item in Results)
            {
                var time = DateTimeOffset.FromUnixTimeMilliseconds(item.TransactionTime).ToLocalTime();
                builder.AppendLine(
                    $"{item.ProductName},{item.BatchNumber},{item.Quantity},{item.UnitPrice}," +
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