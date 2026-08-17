using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using PharmaPOS.Application.Authentication;
using PharmaPOS.Application.Inventory;
using PharmaPOS.Domain.Entities;
using Lightweight_Digital_Inventory_Management___POS_System.ViewModels;

namespace Lightweight_Digital_Inventory_Management___POS_System.Views;

public partial class UserManagementView : UserControl
{
    /// <summary>
    /// 마지막 우클릭이 실제 줄 위에서 일어났는지.
    /// 빈 곳을 우클릭해도 이전 선택이 남아 있어, 선택 여부만으로는 알 수 없다.
    /// </summary>
    private bool _rightClickHitRow;

    public UserManagementView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// 우클릭한 줄을 선택 상태로 만든다.
    /// DataGrid는 우클릭으로 선택이 바뀌지 않아, 그냥 두면 메뉴가 엉뚱한 계정에 작용한다.
    /// </summary>
    private void OnGridRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        var row = FindAncestor<DataGridRow>(e.OriginalSource as DependencyObject);

        _rightClickHitRow = row is not null;

        if (row is not null)
        {
            row.IsSelected = true;
        }
    }

    /// <summary>빈 곳에서 우클릭했으면 메뉴를 띄우지 않는다.</summary>
    private void OnGridContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (!_rightClickHitRow || DataContext is not UserManagementViewModel { HasSelection: true })
        {
            e.Handled = true;
        }
    }

    private static T? FindAncestor<T>(DependencyObject? source) where T : DependencyObject
    {
        while (source is not null and not T)
        {
            // 줄 안의 요소가 시각 트리에 없는 경우가 있어 논리 부모도 함께 본다.
            source = source is Visual or System.Windows.Media.Media3D.Visual3D
                ? VisualTreeHelper.GetParent(source)
                : LogicalTreeHelper.GetParent(source);
        }

        return source as T;
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

        // 성공하면 창이 그냥 닫힌다. 목록은 아무것도 달라지지 않으므로(비밀번호는 표에 없다)
        // 한 줄 남기지 않으면 바뀌었는지 아닌지 알 방법이 없다.
        if (dialog.ShowDialog() == true && DataContext is UserManagementViewModel viewModel)
        {
            viewModel.Message = $"Password for '{targetUser.Username}' has been reset.";
        }
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