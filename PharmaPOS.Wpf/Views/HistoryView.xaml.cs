using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Controls;
using PharmaPOS.Application.Inventory;
using Lightweight_Digital_Inventory_Management___POS_System.Shell;
using Lightweight_Digital_Inventory_Management___POS_System.ViewModels;

namespace Lightweight_Digital_Inventory_Management___POS_System.Views;

public partial class HistoryView : UserControl
{
    private readonly string _facilityId;
    private readonly string _userId;

    public HistoryView(string facilityId, string userId)
    {
        InitializeComponent();
        _facilityId = facilityId;
        _userId     = userId;
    }

    private void OnBackClick(object sender, RoutedEventArgs e)
    {
        var parentWindow = Window.GetWindow(this) as MainWindow;
        if (parentWindow is null) return;

        if (App.CurrentShellViewModel is MainShellViewModel shellVm)
            parentWindow.Content = new MainShellView { DataContext = shellVm };
    }

    private void OnSaleHistoryClick(object sender, RoutedEventArgs e)
    {
        var parentWindow = Window.GetWindow(this) as MainWindow;
        if (parentWindow is null) return;

        try
        {
            var salesHistoryService    = App.Services.GetRequiredService<ISalesHistoryService>();
            var receiptPrintingService = App.Services.GetRequiredService<IReceiptPrintingService>();

            var vm = new SalesHistoryViewModel(
                salesHistoryService, receiptPrintingService, _facilityId, _userId);

            var view = new SalesHistoryView();
            view.AttachViewModel(vm);
            parentWindow.Content = view;
        }
        catch (Exception ex)
        {
            AppDialog.Show("Error", $"Error: {ex.Message}");
        }
    }

    private void OnAdjustmentHistoryClick(object sender, RoutedEventArgs e)
    {
        var parentWindow = Window.GetWindow(this) as MainWindow;
        if (parentWindow is null) return;

        try
        {
            var vm = new AdjustmentHistoryViewModel(_facilityId);
            vm.NavigateBack += () =>
            {
                parentWindow.Content = new HistoryView(_facilityId, _userId);
            };

            var view = new AdjustmentHistoryView { DataContext = vm };
            parentWindow.Content = view;
        }
        catch (Exception ex)
        {
            AppDialog.Show("Error", $"Error: {ex.Message}");
        }
    }
}