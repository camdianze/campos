using System.Windows.Controls;
using Lightweight_Digital_Inventory_Management___POS_System.ViewModels;

namespace Lightweight_Digital_Inventory_Management___POS_System.Views;

public partial class PasswordRecoveryView : UserControl
{
    public PasswordRecoveryView()
    {
        InitializeComponent();
    }

    public void AttachViewModel(PasswordRecoveryViewModel viewModel)
    {
        viewModel.NavigateBackToLogin += OnNavigateBackToLogin;
        viewModel.PasswordResetSucceeded += OnPasswordResetSucceeded;
        DataContext = viewModel;
    }

    private void OnResetPasswordClick(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is PasswordRecoveryViewModel viewModel)
        {
            viewModel.ResetPassword(NewPasswordInput.Password, ConfirmNewPasswordInput.Password);
        }
    }

    private void OnNavigateBackToLogin()
    {
        var parentWindow = System.Windows.Window.GetWindow(this);
        if (parentWindow is MainWindow mainWindow)
        {
            mainWindow.Content = new LoginView();
        }
    }

    private void OnPasswordResetSucceeded()
    {
        var parentWindow = System.Windows.Window.GetWindow(this);
        if (parentWindow is MainWindow mainWindow)
        {
            mainWindow.Content = new LoginView();
        }
    }
}