using System.Windows;
using System.Windows.Controls;
using Lightweight_Digital_Inventory_Management___POS_System.ViewModels;

namespace Lightweight_Digital_Inventory_Management___POS_System.Views;

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