using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Controls;
using CamPOS.Application.Inventory;
using CamPOS.Application.Usage;
using CamPOS.Wpf.Shell;
using CamPOS.Wpf.ViewModels;

namespace CamPOS.Wpf.Views;

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

    /// <summary>화면을 열었다고 적는다. 기다리지 않고, 던지지도 않는다.</summary>
    private static void Opened(string screen) =>
        _ = App.Services.GetRequiredService<IUsageRecorder>()
            .RecordAsync(screen, UsageOutcome.Shown);

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
                salesHistoryService, receiptPrintingService,
                App.Services.GetRequiredService<IUsageRecorder>(),
                _facilityId, _userId);

            Opened(UsageEventNames.SalesHistory);

            var view = new SalesHistoryView();
            view.AttachViewModel(vm);
            parentWindow.Content = view;
        }
        catch (Exception ex)
        {
            AppDialog.Show("Error", $"Error: {ex.Message}");
        }
    }

    private void OnStockHistoryClick(object sender, RoutedEventArgs e)
    {
        var parentWindow = Window.GetWindow(this) as MainWindow;
        if (parentWindow is null) return;

        try
        {
            var stockHistoryService = App.Services.GetRequiredService<IStockHistoryService>();

            var vm = new StockHistoryViewModel(stockHistoryService, _facilityId);
            vm.NavigateBack += () =>
            {
                parentWindow.Content = new HistoryView(_facilityId, _userId);
            };

            Opened(UsageEventNames.StockHistory);

            var view = new StockHistoryView();
            view.AttachViewModel(vm);
            parentWindow.Content = view;
        }
        catch (Exception ex)
        {
            AppDialog.Show("Error", $"Error: {ex.Message}");
        }
    }
}
