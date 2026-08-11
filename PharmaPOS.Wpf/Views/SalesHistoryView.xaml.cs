using Microsoft.Extensions.DependencyInjection;
using System.Windows.Controls;
using PharmaPOS.Application.Inventory;
using Lightweight_Digital_Inventory_Management___POS_System.Shell;
using Lightweight_Digital_Inventory_Management___POS_System.ViewModels;

namespace Lightweight_Digital_Inventory_Management___POS_System.Views;

public partial class SalesHistoryView : UserControl
{
    public SalesHistoryView()
    {
        InitializeComponent();
    }

    public void AttachViewModel(SalesHistoryViewModel viewModel)
    {
        viewModel.NavigateBack += OnBackClickFromViewModel;
        viewModel.RequestRefundDialog += OnRequestRefundDialog;
        DataContext = viewModel;
    }

    private async void OnRequestRefundDialog(SalesHistoryLineItem selectedLine)
    {
        if (DataContext is not SalesHistoryViewModel viewModel)
        {
            return;
        }

        var refundService = App.Services.GetRequiredService<IRefundService>();

        var dialog = new RefundWindow(
            refundService, viewModel.FacilityId, viewModel.UserId, selectedLine)
        {
            Owner = System.Windows.Window.GetWindow(this)
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        // 목록을 다시 읽어야 방금 생긴 환불 행과 바뀐 상태가 보인다.
        await viewModel.ExecuteSearchAsync();
        viewModel.Message = $"Refunded {dialog.RefundedAmount}.";
    }

    private void OnBackClickFromViewModel()
    {
        OnBackClick(this, new System.Windows.RoutedEventArgs());
    }

    private void OnBackClick(object sender, System.Windows.RoutedEventArgs e)
    {
        var parentWindow = System.Windows.Window.GetWindow(this) as MainWindow;
        if (parentWindow is not null)
        {
            parentWindow.Content = new MainShellView
            {
                DataContext = App.CurrentShellViewModel
            };
        }
    }
}