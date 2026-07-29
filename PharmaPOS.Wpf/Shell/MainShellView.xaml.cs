using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Controls;
using PharmaPOS.Application.Authentication;
using PharmaPOS.Application.Inventory;
using PharmaPOS.Application.PasswordPolicy;
using PharmaPOS.Application.Repositories;
using PharmaPOS.Application.Security;
using Lightweight_Digital_Inventory_Management___POS_System.ViewModels;
using Lightweight_Digital_Inventory_Management___POS_System.Views;

namespace Lightweight_Digital_Inventory_Management___POS_System.Shell;

public partial class MainShellView : UserControl
{
    public MainShellView()
    {
        InitializeComponent();

        Loaded += (_, _) =>
        {
            if (DataContext is MainShellViewModel viewModel)
            {
                viewModel.LogoutRequested += OnLogoutRequested;
                viewModel.MyPageRequested += () => OnMyPageRequested(viewModel);
            }
        };
    }

    private void OnLogoutRequested()
    {
        var parentWindow = Window.GetWindow(this);
        if (parentWindow is MainWindow mainWindow)
        {
            App.CurrentShellViewModel = null;
            mainWindow.Content = new LoginView();
        }
    }

    // 비밀번호 변경과 복구 설정은 My Page 안으로 들어갔다. 셸은 My Page만 연다.
    private void OnMyPageRequested(MainShellViewModel shellViewModel)
    {
        var parentWindow = Window.GetWindow(this) as MainWindow;
        if (parentWindow is not null)
            parentWindow.Content = new MyPageView(shellViewModel.CurrentUser);
    }

    // ── 알림 팝업 ─────────────────────────────────────────────────────────

    private void OnAlertsButtonClick(object sender, RoutedEventArgs e)
    {
        AlertsPopup.IsOpen = !AlertsPopup.IsOpen;

        if (AlertsPopup.IsOpen && DataContext is MainShellViewModel vm)
            _ = vm.LoadAlertsAsync();
    }

    private void OnAlertsClick(object sender, RoutedEventArgs e)
    {
        AlertsPopup.IsOpen = false;

        if (DataContext is not MainShellViewModel shellViewModel) return;

        var alertService = App.Services.GetRequiredService<IAlertService>();
        var alertsViewModel = new AlertsViewModel(alertService, shellViewModel.CurrentUser.FacilityId);

        var alertsView = new AlertsView();
        alertsView.AttachViewModel(alertsViewModel);

        var parentWindow = Window.GetWindow(this) as MainWindow;
        if (parentWindow is not null)
            parentWindow.Content = alertsView;
    }

    // ── 네비게이션 ────────────────────────────────────────────────────────

    private void OnProductsClick(object sender, RoutedEventArgs e)
    {
        var parentWindow = Window.GetWindow(this) as MainWindow;
        if (parentWindow is not null)
            parentWindow.Content = new ProductListView();
    }

    private void OnStockInClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainShellViewModel shellViewModel) return;

        var productRepository = App.Services.GetRequiredService<IProductRepository>();
        var stockInService = App.Services.GetRequiredService<IStockInService>();

        var stockInViewModel = new StockInViewModel(
            productRepository, stockInService,
            shellViewModel.CurrentUser.FacilityId, shellViewModel.CurrentUser.UserId);

        var stockInView = new StockInView();
        stockInView.AttachViewModel(stockInViewModel);

        var parentWindow = Window.GetWindow(this) as MainWindow;
        if (parentWindow is not null)
            parentWindow.Content = stockInView;
    }

    private void OnInventoryClick(object sender, RoutedEventArgs e)
    {
        var parentWindow = Window.GetWindow(this) as MainWindow;
        if (parentWindow is not null)
            parentWindow.Content = new InventoryStatusView();
    }

    private void OnAdjustmentClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainShellViewModel shellViewModel) return;

        var productRepository = App.Services.GetRequiredService<IProductRepository>();
        var inventoryRepository = App.Services.GetRequiredService<IInventoryRepository>();
        var adjustmentService = App.Services.GetRequiredService<IAdjustmentService>();

        var adjustmentViewModel = new AdjustmentViewModel(
            productRepository, inventoryRepository, adjustmentService,
            shellViewModel.CurrentUser.FacilityId, shellViewModel.CurrentUser.UserId);

        var adjustmentView = new AdjustmentView();
        adjustmentView.AttachViewModel(adjustmentViewModel);

        var parentWindow = Window.GetWindow(this) as MainWindow;
        if (parentWindow is not null)
            parentWindow.Content = adjustmentView;
    }

    private void OnPosSaleClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainShellViewModel shellViewModel) return;

        var productRepository = App.Services.GetRequiredService<IProductRepository>();
        var inventoryRepository = App.Services.GetRequiredService<IInventoryRepository>();
        var saleService = App.Services.GetRequiredService<ISaleService>();
        var receiptPrintingService = App.Services.GetRequiredService<IReceiptPrintingService>();

        var posSaleViewModel = new PosSaleViewModel(
            productRepository, inventoryRepository, saleService, receiptPrintingService,
            shellViewModel.CurrentUser.FacilityId, shellViewModel.CurrentUser.UserId,
            shellViewModel.CurrentUser.Role);

        var posSaleView = new PosSaleView();
        posSaleView.AttachViewModel(posSaleViewModel);

        var parentWindow = Window.GetWindow(this) as MainWindow;
        if (parentWindow is not null)
            parentWindow.Content = posSaleView;
    }

    private void OnAdminDashboardClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainShellViewModel shellViewModel) return;

        var dashboardService = App.Services.GetRequiredService<IAdminDashboardService>();
        var dashboardViewModel = new AdminDashboardViewModel(dashboardService, shellViewModel.CurrentUser.FacilityId);

        var dashboardView = new AdminDashboardView();
        dashboardView.AttachViewModel(dashboardViewModel);

        var parentWindow = Window.GetWindow(this) as MainWindow;
        if (parentWindow is not null)
            parentWindow.Content = dashboardView;
    }

    private void OnHistoryClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainShellViewModel shellViewModel) return;

        var historyView = new HistoryView(
            shellViewModel.CurrentUser.FacilityId,
            shellViewModel.CurrentUser.UserId);

        var parentWindow = Window.GetWindow(this) as MainWindow;
        if (parentWindow is not null)
            parentWindow.Content = historyView;
    }
}