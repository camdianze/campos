using Microsoft.Extensions.DependencyInjection;
using CamPOS.Application.Sync;
using PharmaPOS.Application.Licensing;
using System.Windows.Controls;
using CamPOS.Application.Authentication;
using CamPOS.Application.Inventory;
using CamPOS.Domain.Entities;
using CamPOS.Wpf.Shell;
using CamPOS.Wpf.ViewModels;
using CamPOS.Wpf.Services;

namespace CamPOS.Wpf.Views;

public partial class LoginView : UserControl
{
    public LoginView()
    {
        InitializeComponent();

        var loginViewModel = App.Services.GetRequiredService<LoginViewModel>();
        loginViewModel.LoginSucceeded += OnLoginSucceeded;
        loginViewModel.ForgotUsernameRequested += OnForgotUsernameRequested;
        loginViewModel.ForgotPasswordRequested += OnForgotPasswordRequested;

        DataContext = loginViewModel;

        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        // Remember me로 아이디가 이미 채워져 있으면 비밀번호 칸부터 입력하게 한다.
        if (DataContext is LoginViewModel { Username.Length: > 0 })
            PasswordInput.Focus();
        else
            UsernameInput.Focus();
    }

    private async void OnLoginSucceeded(User loggedInUser)
    {
        var alertService = App.Services.GetRequiredService<IAlertService>();
        var uiLanguage = App.Services.GetRequiredService<UiLanguageService>();
        // 라이선스 상태는 로그인 시점에 한 번 읽는다. 배지가 필요한지 여기서 정해진다.
        var licenseStatus = App.Services.GetRequiredService<ILicenseService>().GetStatus();
        // 동기화 상태도 로그인 시점에 한 번 읽는다. 배지가 필요한지 여기서 정해진다 —
        // 자동 전송이 조용히 멈춘 상태를 약국이 알 수 있는 유일한 자리다.
        var syncHealth = await App.Services.GetRequiredService<SyncService>()
            .GetHealthAsync(loggedInUser.FacilityId);

        var shellViewModel = new MainShellViewModel(
            loggedInUser, alertService, uiLanguage, licenseStatus, syncHealth);
        App.CurrentShellViewModel = shellViewModel;

        var shellView = new MainShellView { DataContext = shellViewModel };

        var parentWindow = System.Windows.Window.GetWindow(this);
        if (parentWindow is MainWindow mainWindow)
            mainWindow.Content = shellView;

        // 자동 전송의 시계를 돌린다. 화면과 무관하게 앱이 살아 있는 동안 돈다.
        App.Services.GetRequiredService<SyncScheduler>().Start(loggedInUser.FacilityId);
    }

    private void OnForgotPasswordRequested()
    {
        var passwordRecoveryService = App.Services.GetRequiredService<IPasswordRecoveryService>();
        var recoveryViewModel = new PasswordRecoveryViewModel(passwordRecoveryService);

        var recoveryView = new PasswordRecoveryView();
        recoveryView.AttachViewModel(recoveryViewModel);

        var parentWindow = System.Windows.Window.GetWindow(this);
        if (parentWindow is MainWindow mainWindow)
            mainWindow.Content = recoveryView;
    }

    private void OnForgotUsernameRequested()
    {
        var passwordRecoveryService = App.Services.GetRequiredService<IPasswordRecoveryService>();
        var findUsernameViewModel = new FindUsernameViewModel(passwordRecoveryService);

        var findUsernameView = new FindUsernameView();
        findUsernameView.AttachViewModel(findUsernameViewModel);

        var parentWindow = System.Windows.Window.GetWindow(this);
        if (parentWindow is MainWindow mainWindow)
            mainWindow.Content = findUsernameView;
    }
}