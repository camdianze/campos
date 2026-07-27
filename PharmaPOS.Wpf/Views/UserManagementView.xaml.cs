using Microsoft.Extensions.DependencyInjection;
using System.Windows.Controls;
using PharmaPOS.Application.Authentication;
using PharmaPOS.Application.Inventory;
using PharmaPOS.Domain.Entities;
using Lightweight_Digital_Inventory_Management___POS_System.ViewModels;

namespace Lightweight_Digital_Inventory_Management___POS_System.Views;

public partial class UserManagementView : UserControl
{
    public UserManagementView()
    {
        InitializeComponent();
    }

    public void AttachViewModel(UserManagementViewModel viewModel)
    {
        viewModel.RequestAddUserDialog += OnRequestAddUserDialog;
        viewModel.RequestResetPasswordDialog += OnRequestResetPasswordDialog;
        viewModel.NavigateBack += OnNavigateBack;
        DataContext = viewModel;
    }

    private async void OnRequestAddUserDialog()
    {
        if (DataContext is not UserManagementViewModel viewModel)
        {
            return;
        }

        var userManagementService = App.Services.GetRequiredService<IUserManagementService>();
        var dialog = new AddUserWindow(userManagementService, App.CurrentShellViewModel!.CurrentUser.FacilityId)
        {
            Owner = System.Windows.Window.GetWindow(this)
        };

        if (dialog.ShowDialog() == true)
        {
            await viewModel.ReloadAsync();
        }
    }

    private void OnRequestResetPasswordDialog(User targetUser)
    {
        var userManagementService = App.Services.GetRequiredService<IUserManagementService>();
        var dialog = new ResetPasswordWindow(userManagementService, targetUser)
        {
            Owner = System.Windows.Window.GetWindow(this)
        };

        dialog.ShowDialog();
    }

    private void OnNavigateBack()
    {
        var parentWindow = System.Windows.Window.GetWindow(this) as MainWindow;
        if (parentWindow is null)
        {
            return;
        }

        var dashboardService = App.Services.GetRequiredService<IAdminDashboardService>();
        var dashboardViewModel = new AdminDashboardViewModel(dashboardService, App.CurrentShellViewModel!.CurrentUser.FacilityId);

        var dashboardView = new AdminDashboardView();
        dashboardView.AttachViewModel(dashboardViewModel);

        parentWindow.Content = dashboardView;
    }
}