using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Controls;
using PharmaPOS.Application.Authentication;
using PharmaPOS.Application.Counselling;
using PharmaPOS.Application.Inventory;
using PharmaPOS.Application.PasswordPolicy;
using PharmaPOS.Application.Products;
using PharmaPOS.Application.Reports;
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

    /// <summary>
    /// 상품 화면. 입고(Stock-IN)가 이 화면 안으로 들어와서, 예전 Stock-IN 진입점도
    /// 여기로 모인다. 입고 저장에 시설/사용자 ID가 필요해 ViewModel을 여기서 만든다.
    /// </summary>
    private void OnProductsClick(object sender, RoutedEventArgs e)
    {
        var productListView = ProductListView.Create();

        var parentWindow = Window.GetWindow(this) as MainWindow;
        if (parentWindow is not null)
            parentWindow.Content = productListView;
    }

    private void OnInventoryClick(object sender, RoutedEventArgs e)
    {
        var parentWindow = Window.GetWindow(this) as MainWindow;
        if (parentWindow is not null)
            parentWindow.Content = new InventoryStatusView();
    }

    /// <summary>
    /// 매출 리포트. 예전에는 이 자리가 재고 조정이었는데, 조정은 재고 화면에서
    /// 고른 배치 아래 패널로 들어가면서 여기서 따로 열 이유가 없어졌다.
    /// (관리자 대시보드에도 같은 화면으로 가는 입구가 있다.)
    /// </summary>
    private void OnReportsClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainShellViewModel shellViewModel) return;

        var reportService = App.Services.GetRequiredService<IReportService>();

        var reportsViewModel = new ReportsViewModel(
            reportService,
            App.Services.GetRequiredService<ICounsellingSettingsService>(),
            shellViewModel.CurrentUser.FacilityId);

        var reportsView = new ReportsView();
        reportsView.AttachViewModel(reportsViewModel);

        var parentWindow = Window.GetWindow(this) as MainWindow;
        if (parentWindow is not null)
            parentWindow.Content = reportsView;
    }

    /// <summary>
    /// 항생제 복약안내 설정. 시설 전체에 적용되는 설정이라 관리자 전용 줄에 둔다.
    /// </summary>
    private void OnCounsellingSettingsClick(object sender, RoutedEventArgs e)
    {
        var viewModel = new CounsellingSettingsViewModel(
            App.Services.GetRequiredService<ICounsellingSettingsService>(),
            App.Services.GetRequiredService<ICounsellingLocaleProvider>(),
            App.Services.GetRequiredService<ICounsellingLogRepository>());

        var view = new CounsellingSettingsView();
        view.AttachViewModel(viewModel);

        var parentWindow = Window.GetWindow(this) as MainWindow;
        if (parentWindow is not null)
            parentWindow.Content = view;
    }

    private void OnPosSaleClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainShellViewModel shellViewModel) return;

        var productRepository = App.Services.GetRequiredService<IProductRepository>();
        var inventoryRepository = App.Services.GetRequiredService<IInventoryRepository>();
        var saleService = App.Services.GetRequiredService<ISaleService>();
        var receiptPrintingService = App.Services.GetRequiredService<IReceiptPrintingService>();
        var counsellingService = App.Services.GetRequiredService<ICounsellingService>();

        var posSaleViewModel = new PosSaleViewModel(
            productRepository, inventoryRepository, saleService, receiptPrintingService,
            counsellingService,
            shellViewModel.CurrentUser.FacilityId, shellViewModel.CurrentUser.UserId,
            shellViewModel.CurrentUser.Username,
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