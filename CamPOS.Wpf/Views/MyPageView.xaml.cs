using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Controls;
using PharmaPOS.Application.Authentication;
using PharmaPOS.Domain.Entities;
using Lightweight_Digital_Inventory_Management___POS_System.Shell;
using Lightweight_Digital_Inventory_Management___POS_System.ViewModels;

namespace Lightweight_Digital_Inventory_Management___POS_System.Views;

/// <summary>
/// 계정 관련 기능을 모아둔 화면. 셸 상단의 My Page 버튼에서 들어온다.
/// 화면 자체에 로직이 없어 ViewModel 없이 HistoryView와 같은 방식으로 만들었다.
/// </summary>
public partial class MyPageView : UserControl
{
    private readonly User _currentUser;

    public MyPageView(User currentUser)
    {
        InitializeComponent();

        _currentUser = currentUser;

        UsernameText.Text = currentUser.Username;
        RoleText.Text     = currentUser.Role.ToString();
        InitialText.Text  = string.IsNullOrWhiteSpace(currentUser.Username)
            ? "?"
            : currentUser.Username[..1].ToUpperInvariant();
    }

    private void OnBackClick(object sender, RoutedEventArgs e)
    {
        var parentWindow = Window.GetWindow(this) as MainWindow;
        if (parentWindow is null) return;

        if (App.CurrentShellViewModel is MainShellViewModel shellViewModel)
            parentWindow.Content = new MainShellView { DataContext = shellViewModel };
    }

    private void OnChangePasswordClick(object sender, RoutedEventArgs e)
    {
        var parentWindow = Window.GetWindow(this) as MainWindow;
        if (parentWindow is null) return;

        var changePasswordService = App.Services.GetRequiredService<IChangePasswordService>();
        var viewModel = new ChangePasswordViewModel(changePasswordService, _currentUser);

        // 취소는 My Page로 돌아온다. 예전에는 취소해도 로그인 화면으로 튕겨 로그아웃되는 꼴이었다.
        viewModel.NavigateBack += () =>
        {
            parentWindow.Content = new MyPageView(_currentUser);
        };

        // 비밀번호가 실제로 바뀌면 세션을 유지할 수 없으므로 로그인 화면으로 보낸다.
        viewModel.NavigateBackToLogin += () =>
        {
            App.CurrentShellViewModel = null;
            parentWindow.Content = new LoginView();
        };

        parentWindow.Content = new ChangePasswordView { DataContext = viewModel };
    }

    private void OnRecoverySettingsClick(object sender, RoutedEventArgs e)
    {
        var parentWindow = Window.GetWindow(this) as MainWindow;
        if (parentWindow is null) return;

        var recoverySettingsService = App.Services.GetRequiredService<IRecoverySettingsService>();
        var viewModel = new RecoverySettingsViewModel(
            recoverySettingsService, _currentUser.UserId, _currentUser.Username);

        var view = new RecoverySettingsView();
        view.AttachViewModel(viewModel, () => parentWindow.Content = new MyPageView(_currentUser));

        parentWindow.Content = view;
    }
}
