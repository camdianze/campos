using Microsoft.Extensions.DependencyInjection;
using System.Windows.Controls;
using PharmaPOS.Application.Counselling;
using PharmaPOS.Application.Inventory;
using PharmaPOS.Application.Products;
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

        // 입고 전용 화면은 없어지고 Products 화면 하단 패널로 들어갔다.
        // 재고 화면의 "Stock-IN" 이동도 그쪽으로 보낸다.
        var viewModel = new ProductListViewModel(
            App.Services.GetRequiredService<IProductRepository>(),
            App.Services.GetRequiredService<IProductService>(),
            App.Services.GetRequiredService<IAntibioticMatchingService>(),
            App.Services.GetRequiredService<IStockInService>(),
            App.CurrentShellViewModel.CurrentUser.FacilityId,
            App.CurrentShellViewModel.CurrentUser.UserId);

        var productListView = new ProductListView();
        productListView.AttachViewModel(viewModel);

        var parentWindow = System.Windows.Window.GetWindow(this) as MainWindow;
        if (parentWindow is not null)
        {
            parentWindow.Content = productListView;
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