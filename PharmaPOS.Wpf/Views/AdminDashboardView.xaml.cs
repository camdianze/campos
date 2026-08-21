using Microsoft.Extensions.DependencyInjection;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using PharmaPOS.Application.Authentication;
using PharmaPOS.Application.Counselling;
using PharmaPOS.Application.Import;
using PharmaPOS.Application.Inventory;
using PharmaPOS.Application.Products;
using PharmaPOS.Application.Receipts;
using PharmaPOS.Application.Reports;
using PharmaPOS.Application.Repositories;
using Lightweight_Digital_Inventory_Management___POS_System.Shell;
using Lightweight_Digital_Inventory_Management___POS_System.ViewModels;

namespace Lightweight_Digital_Inventory_Management___POS_System.Views;

public partial class AdminDashboardView : UserControl
{
    private ReceiptSettingsViewModel? _receiptSettingsViewModel;

    public AdminDashboardView()
    {
        InitializeComponent();

        // 창을 그냥 닫아도 저장 안 된 설정을 잃는다. 브라우저의 beforeunload에 해당하는
        // 자리가 이것이다 — 화면 이동은 아래 각 핸들러가, 창 닫기는 여기가 막는다.
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public void AttachViewModel(AdminDashboardViewModel viewModel)
    {
        viewModel.NavigateToProductManagement += OnNavigateToProductManagement;
        viewModel.NavigateToUserManagement += OnNavigateToUserManagement;
        viewModel.NavigateToInventoryOverview += OnNavigateToInventoryOverview;
        viewModel.NavigateToSalesHistory += OnNavigateToSalesHistory;
        viewModel.NavigateToReports += OnNavigateToReports;
        viewModel.NavigateToBackupExport += OnNavigateToBackupExport;
        viewModel.NavigateBack += OnNavigateBackFromViewModel;
        DataContext = viewModel;

        AttachReceiptSettings();
    }

    /// <summary>
    /// 영수증 설정 구역은 이 화면 안의 독립된 구역이라 자기 ViewModel을 쓴다.
    /// 대시보드 ViewModel에 스무 개 넘는 설정 속성을 얹으면 지표 화면과 설정 화면이
    /// 한 클래스에 섞인다.
    /// </summary>
    private void AttachReceiptSettings()
    {
        if (App.CurrentShellViewModel is not { } shellViewModel)
        {
            return;
        }

        _receiptSettingsViewModel = new ReceiptSettingsViewModel(
            App.Services.GetRequiredService<IReceiptSettingsService>(),
            App.Services.GetRequiredService<ICounsellingLocaleProvider>(),
            shellViewModel.CurrentUser.Role,
            shellViewModel.CurrentUser.UserId);

        ReceiptSettingsSection.DataContext = _receiptSettingsViewModel;

        // 저장된 설정을 읽는 동안 화면이 멈추지 않도록 띄운 뒤에 채운다.
        _ = _receiptSettingsViewModel.LoadAsync();
    }

    // ── 저장 안 된 변경 지키기 ────────────────────────────────────────────

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (Window.GetWindow(this) is { } window)
        {
            window.Closing += OnWindowClosing;
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (Window.GetWindow(this) is { } window)
        {
            window.Closing -= OnWindowClosing;
        }
    }

    private void OnWindowClosing(object? sender, CancelEventArgs e)
    {
        if (!ConfirmLeavingUnsavedChanges())
        {
            e.Cancel = true;
        }
    }

    /// <summary>
    /// 저장하지 않은 영수증 설정이 있으면 물어본다. 나가도 된다고 하면 true.
    /// </summary>
    private bool ConfirmLeavingUnsavedChanges()
    {
        if (_receiptSettingsViewModel is not { IsDirty: true })
        {
            return true;
        }

        return AppDialog.Confirm(
            "Unsaved Changes",
            "The receipt settings have been changed but not saved.\n\nLeave without saving?",
            confirmText: "Leave",
            cancelText: "Stay");
    }

    // ── 네비게이션 ────────────────────────────────────────────────────────

    private void OnNavigateToBackupExport()
    {
        var parentWindow = System.Windows.Window.GetWindow(this) as MainWindow;
        if (parentWindow is null) return;
        if (!ConfirmLeavingUnsavedChanges()) return;

        var backupService = App.Services.GetRequiredService<IBackupService>();
        var initialImportService = App.Services.GetRequiredService<IInitialImportService>();

        // 재고 가져오기가 입고 원장을 남기므로 시설/사용자 ID가 필요하다.
        var currentUser = App.CurrentShellViewModel!.CurrentUser;

        var backupExportViewModel = new BackupExportViewModel(
            backupService, initialImportService, currentUser.FacilityId, currentUser.UserId);

        var backupExportView = new BackupExportView();
        backupExportView.AttachViewModel(backupExportViewModel);

        parentWindow.Content = backupExportView;
    }

    private void OnNavigateToSalesHistory()
    {
        var parentWindow = System.Windows.Window.GetWindow(this) as MainWindow;
        if (parentWindow is null) return;
        if (!ConfirmLeavingUnsavedChanges()) return;

        var salesHistoryService = App.Services.GetRequiredService<ISalesHistoryService>();
        var receiptPrintingService = App.Services.GetRequiredService<IReceiptPrintingService>();

        var salesHistoryViewModel = new SalesHistoryViewModel(
            salesHistoryService, receiptPrintingService,
            App.CurrentShellViewModel!.CurrentUser.FacilityId,
            App.CurrentShellViewModel.CurrentUser.UserId);

        var salesHistoryView = new SalesHistoryView();
        salesHistoryView.AttachViewModel(salesHistoryViewModel);

        parentWindow.Content = salesHistoryView;
    }

    private void OnNavigateToReports()
    {
        var parentWindow = System.Windows.Window.GetWindow(this) as MainWindow;
        if (parentWindow is null) return;
        if (!ConfirmLeavingUnsavedChanges()) return;

        var reportService = App.Services.GetRequiredService<IReportService>();

        var reportsViewModel = new ReportsViewModel(
            reportService,
            App.Services.GetRequiredService<ICounsellingSettingsService>(),
            App.CurrentShellViewModel!.CurrentUser.FacilityId);

        var reportsView = new ReportsView();
        reportsView.AttachViewModel(reportsViewModel);

        parentWindow.Content = reportsView;
    }

    private void OnNavigateToProductManagement()
    {
        var parentWindow = System.Windows.Window.GetWindow(this) as MainWindow;
        if (parentWindow is null) return;
        if (!ConfirmLeavingUnsavedChanges()) return;

        parentWindow.Content = ProductListView.Create();
    }

    private void OnNavigateToInventoryOverview()
    {
        var parentWindow = System.Windows.Window.GetWindow(this) as MainWindow;
        if (parentWindow is null) return;
        if (!ConfirmLeavingUnsavedChanges()) return;

        parentWindow.Content = new InventoryStatusView();
    }

    private void OnNavigateToUserManagement()
    {
        var parentWindow = System.Windows.Window.GetWindow(this) as MainWindow;
        if (parentWindow is null) return;
        if (!ConfirmLeavingUnsavedChanges()) return;

        var userRepository = App.Services.GetRequiredService<IUserRepository>();
        var userManagementService = App.Services.GetRequiredService<IUserManagementService>();

        var userManagementViewModel = new UserManagementViewModel(
            userRepository, userManagementService,
            App.CurrentShellViewModel!.CurrentUser.FacilityId,
            App.CurrentShellViewModel!.CurrentUser.UserId);

        var userManagementView = new UserManagementView();
        userManagementView.AttachViewModel(userManagementViewModel);

        parentWindow.Content = userManagementView;
    }

    private void OnNavigateBackFromViewModel()
    {
        OnBackClick(this, new System.Windows.RoutedEventArgs());
    }

    private void OnBackClick(object sender, System.Windows.RoutedEventArgs e)
    {
        var parentWindow = System.Windows.Window.GetWindow(this) as MainWindow;
        if (parentWindow is null) return;
        if (!ConfirmLeavingUnsavedChanges()) return;

        parentWindow.Content = new MainShellView { DataContext = App.CurrentShellViewModel };
    }
}
