using Microsoft.Extensions.DependencyInjection;
using System.Windows.Controls;
using PharmaPOS.Application.Authentication;
using PharmaPOS.Application.Import;
using PharmaPOS.Application.Inventory;
using PharmaPOS.Application.Products;
using PharmaPOS.Application.Reports;
using PharmaPOS.Application.Repositories;
using Lightweight_Digital_Inventory_Management___POS_System.Shell;
using Lightweight_Digital_Inventory_Management___POS_System.ViewModels;

namespace Lightweight_Digital_Inventory_Management___POS_System.Views;

public partial class AdminDashboardView : UserControl
{
    public AdminDashboardView()
    {
        InitializeComponent();
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
    }

    private void OnNavigateToBackupExport()
    {
        var parentWindow = System.Windows.Window.GetWindow(this) as MainWindow;
        if (parentWindow is null) return;

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

        var reportService = App.Services.GetRequiredService<IReportService>();

        var reportsViewModel = new ReportsViewModel(
            reportService, App.CurrentShellViewModel!.CurrentUser.FacilityId);

        var reportsView = new ReportsView();
        reportsView.AttachViewModel(reportsViewModel);

        parentWindow.Content = reportsView;
    }

    private void OnNavigateToProductManagement()
    {
        var parentWindow = System.Windows.Window.GetWindow(this) as MainWindow;
        if (parentWindow is not null)
            parentWindow.Content = ProductListView.Create();
    }

    private void OnNavigateToInventoryOverview()
    {
        var parentWindow = System.Windows.Window.GetWindow(this) as MainWindow;
        if (parentWindow is not null)
            parentWindow.Content = new InventoryStatusView();
    }

    private void OnNavigateToUserManagement()
    {
        var parentWindow = System.Windows.Window.GetWindow(this) as MainWindow;
        if (parentWindow is null) return;

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
        if (parentWindow is not null)
            parentWindow.Content = new MainShellView { DataContext = App.CurrentShellViewModel };
    }
}