using Microsoft.Extensions.DependencyInjection;
using System.Windows.Controls;
using PharmaPOS.Application.Authentication;
using PharmaPOS.Application.Inventory;
using PharmaPOS.Domain.Entities;
using Lightweight_Digital_Inventory_Management___POS_System.Shell;
using Lightweight_Digital_Inventory_Management___POS_System.ViewModels;
using Lightweight_Digital_Inventory_Management___POS_System.Shell;
namespace Lightweight_Digital_Inventory_Management___POS_System.Views;

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
    }

    private void OnLoginSucceeded(User loggedInUser)
    {
        var alertService = App.Services.GetRequiredService<IAlertService>();
        var shellViewModel = new MainShellViewModel(loggedInUser, alertService);
        App.CurrentShellViewModel = shellViewModel;

        var shellView = new MainShellView { DataContext = shellViewModel };

        var parentWindow = System.Windows.Window.GetWindow(this);
        if (parentWindow is MainWindow mainWindow)
            mainWindow.Content = shellView;
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