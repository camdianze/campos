using Microsoft.Extensions.DependencyInjection;
using System.Windows.Controls;
using PharmaPOS.Application.Inventory;
using PharmaPOS.Application.Repositories;
using Lightweight_Digital_Inventory_Management___POS_System.Shell;
using Lightweight_Digital_Inventory_Management___POS_System.ViewModels;

namespace Lightweight_Digital_Inventory_Management___POS_System.Views;

public partial class InventoryStatusView : UserControl
{
    public InventoryStatusView()
    {
        InitializeComponent();

        var viewModel = App.Services.GetRequiredService<InventoryStatusViewModel>();
        viewModel.NavigateToStockIn += OnNavigateToStockIn;
        viewModel.NavigateToAdjustment += OnNavigateToAdjustment;

        DataContext = viewModel;
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

    private void OnNavigateToStockIn()
    {
        if (App.CurrentShellViewModel is null)
        {
            return;
        }

        var productRepository = App.Services.GetRequiredService<IProductRepository>();
        var stockInService = App.Services.GetRequiredService<IStockInService>();

        var stockInViewModel = new StockInViewModel(
            productRepository, stockInService,
            App.CurrentShellViewModel.CurrentUser.FacilityId,
            App.CurrentShellViewModel.CurrentUser.UserId);

        var stockInView = new StockInView();
        stockInView.AttachViewModel(stockInViewModel);

        var parentWindow = System.Windows.Window.GetWindow(this) as MainWindow;
        if (parentWindow is not null)
        {
            parentWindow.Content = stockInView;
        }
    }

    private void OnNavigateToAdjustment()
    {
        if (App.CurrentShellViewModel is null)
        {
            return;
        }

        var productRepository = App.Services.GetRequiredService<IProductRepository>();
        var inventoryRepository = App.Services.GetRequiredService<IInventoryRepository>();
        var adjustmentService = App.Services.GetRequiredService<IAdjustmentService>();

        var adjustmentViewModel = new AdjustmentViewModel(
            productRepository, inventoryRepository, adjustmentService,
            App.CurrentShellViewModel.CurrentUser.FacilityId,
            App.CurrentShellViewModel.CurrentUser.UserId);

        var adjustmentView = new AdjustmentView();
        adjustmentView.AttachViewModel(adjustmentViewModel);

        var parentWindow = System.Windows.Window.GetWindow(this) as MainWindow;
        if (parentWindow is not null)
        {
            parentWindow.Content = adjustmentView;
        }
    }
}