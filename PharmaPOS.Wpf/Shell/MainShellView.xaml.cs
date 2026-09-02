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

using Lightweight_Digital_Inventory_Management___POS_System.Services;

namespace Lightweight_Digital_Inventory_Management___POS_System.Shell;

public partial class MainShellView : UserControl
{
    public MainShellView()
    {
        InitializeComponent();

        InitializeLanguageToggle();

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
            shellViewModel.CurrentUser.Role,
            App.Services.GetRequiredService<UiLanguageService>());

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

    // ── 화면 언어 ────────────────────────────────────────────────────────────

    /// <summary>
    /// 토글을 현재 언어에 맞춰 놓는다. 크메르어 파일이 아예 없으면 고를 것이
    /// 없으므로 토글을 통째로 감춘다 — 눌러도 아무 일이 없는 버튼이 더 나쁘다.
    /// </summary>
    private void InitializeLanguageToggle()
    {
        var uiLanguage = App.Services.GetRequiredService<UiLanguageService>();

        if (!uiLanguage.IsKhmerAvailable)
        {
            LanguageToggle.Visibility = System.Windows.Visibility.Collapsed;
            return;
        }

        // 여기서 붙이는 IsChecked는 사용자가 누른 것이 아니므로 저장을 부르지 않는다.
        _suppressLanguageChange = true;
        EnglishOption.IsChecked = !uiLanguage.IsKhmer;
        KhmerOption.IsChecked = uiLanguage.IsKhmer;
        _suppressLanguageChange = false;
    }

    /// <summary>
    /// 처음부터 켜 둔다. 필드 초기화는 생성자 본문보다 먼저 돌기 때문에,
    /// InitializeComponent()가 라디오를 붙이며 쏘는 Checked까지 이 플래그가 덮는다.
    /// 그러지 않으면 화면을 드나들 때마다 저장된 언어가 영어로 덮인다.
    /// </summary>
    private bool _suppressLanguageChange = true;

    private async void OnLanguageChecked(object sender, System.Windows.RoutedEventArgs e)
    {
        if (_suppressLanguageChange)
        {
            return;
        }

        var uiLanguage = App.Services.GetRequiredService<UiLanguageService>();

        await uiLanguage.SetLanguageAsync(
            ReferenceEquals(sender, KhmerOption) ? UiLanguageService.Khmer : UiLanguageService.English);
    }
}