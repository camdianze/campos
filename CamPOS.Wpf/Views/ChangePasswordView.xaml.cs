using System.Windows;
using System.Windows.Controls;
using CamPOS.Wpf.ViewModels;

namespace CamPOS.Wpf.Views;

public partial class ChangePasswordView : UserControl
{
    public ChangePasswordView()
    {
        InitializeComponent();
    }

    private void OnChangePasswordClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is ChangePasswordViewModel viewModel)
        {
            viewModel.ChangePassword(
                CurrentPasswordInput.Password,
                NewPasswordInput.Password,
                ConfirmNewPasswordInput.Password);
        }
    }
}