using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using PharmaPOS.Application.Inventory;
using Lightweight_Digital_Inventory_Management___POS_System.ViewModels;

namespace Lightweight_Digital_Inventory_Management___POS_System.Views;

/// <summary>
/// 판매 내역에서 고른 거래 하나를 환불하는 창.
///
/// 이 창은 AddUserWindow처럼 코드 비하인드가 직접 서비스를 부른다 — 화면 하나에
/// 딸린 짧은 흐름이라 ViewModel과 DI 등록을 따로 두면 오히려 따라가기 어렵다.
/// </summary>
public partial class RefundWindow : Window
{
    private readonly IRefundService _refundService;
    private readonly string _facilityId;
    private readonly string _userId;
    private readonly SalesHistoryLineItem _selectedLine;

    private readonly ObservableCollection<RefundLineRow> _rows = new();

    /// <summary>환불에 성공했을 때 실제로 돌려준 금액.</summary>
    public decimal RefundedAmount { get; private set; }

    public RefundWindow(
        IRefundService refundService,
        string facilityId,
        string userId,
        SalesHistoryLineItem selectedLine)
    {
        InitializeComponent();

        _refundService = refundService;
        _facilityId = facilityId;
        _userId = userId;
        _selectedLine = selectedLine;

        LinesGrid.ItemsSource = _rows;

        var soldAt = DateTimeOffset.FromUnixTimeMilliseconds(selectedLine.TransactionTime).ToLocalTime();
        SaleSummaryText.Text =
            $"Sold on {soldAt:yyyy-MM-dd HH:mm} by {selectedLine.Username}  ·  {selectedLine.PaymentMethod}";

        UpdateTotal();

        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        IReadOnlyList<RefundableLine> lines;

        try
        {
            lines = await _refundService.GetRefundableLinesAsync(
                _facilityId, _selectedLine.TransactionTime, _selectedLine.UserId);
        }
        catch (Exception)
        {
            MessageText.Text = "The sale could not be loaded. Please try again.";
            RefundButton.IsEnabled = false;
            return;
        }

        foreach (var line in lines)
        {
            var row = new RefundLineRow { Line = line };
            row.PropertyChanged += OnRowChanged;
            _rows.Add(row);
        }

        if (_rows.Count == 0)
        {
            MessageText.Text = "The sale could not be found.";
            RefundButton.IsEnabled = false;
            return;
        }

        if (_rows.All(r => r.RemainingQuantity == 0))
        {
            MessageText.Text = "This sale has already been fully refunded.";
            RefundButton.IsEnabled = false;
            return;
        }

        // 한 줄짜리 판매(대부분의 경우)는 전량 환불이 기본값이면 클릭 한 번이 줄어든다.
        if (_rows.Count == 1)
        {
            _rows[0].FillRemaining();
        }
    }

    private void OnRowChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(RefundLineRow.RefundQuantity))
        {
            UpdateTotal();
        }
    }

    private void UpdateTotal()
    {
        TotalText.Text = $"Total refund: {_rows.Sum(r => r.Amount)}";
    }

    private void OnRefundWholeSaleClick(object sender, RoutedEventArgs e)
    {
        foreach (var row in _rows)
        {
            row.FillRemaining();
        }
    }

    private async void OnRefundClick(object sender, RoutedEventArgs e)
    {
        MessageText.Text = string.Empty;

        var requests = _rows
            .Where(r => r.RefundQuantity > 0)
            .Select(r => new RefundLineRequest
            {
                TransactionId = r.Line.TransactionId,
                Quantity = r.RefundQuantity
            })
            .ToList();

        if (requests.Count == 0)
        {
            MessageText.Text = "Please enter the quantity to refund.";
            return;
        }

        var returnToStock = ReturnToStockCheckBox.IsChecked == true;
        var total = _rows.Sum(r => r.Amount);

        // 서랍에서 현금이 나가는 일이라 한 번 더 묻는다.
        var stockNote = returnToStock
            ? "The items will be returned to stock."
            : "The items will NOT be returned to stock.";

        if (!AppDialog.Confirm("Confirm Refund", $"Refund {total}?\n{stockNote}", "Refund", "Cancel"))
        {
            return;
        }

        RefundButton.IsEnabled = false;

        var result = await _refundService.RefundAsync(
            _facilityId,
            _userId,
            _selectedLine.TransactionTime,
            _selectedLine.UserId,
            requests,
            ReasonInput.Text,
            returnToStock);

        if (!result.IsSuccess)
        {
            MessageText.Text = result.Message;
            RefundButton.IsEnabled = true;
            return;
        }

        RefundedAmount = result.RefundedAmount;
        DialogResult = true;
        Close();
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
