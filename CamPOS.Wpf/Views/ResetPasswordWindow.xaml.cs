using System.Windows;
using PharmaPOS.Application.Authentication;
using PharmaPOS.Domain.Entities;

namespace Lightweight_Digital_Inventory_Management___POS_System.Views;

public partial class ResetPasswordWindow : Window
{
    private readonly IUserManagementService _userManagementService;
    private readonly User _targetUser;

    public ResetPasswordWindow(IUserManagementService userManagementService, User targetUser)
    {
        InitializeComponent();

        _userManagementService = userManagementService;
        _targetUser = targetUser;

        TargetUsernameText.Text = $"Reset password for: {targetUser.Username}";
    }

    private async void OnResetClick(object sender, RoutedEventArgs e)
    {
        MessageText.Text = string.Empty;

        var result = await _userManagementService.ResetPasswordAsync(
            _targetUser.UserId, _targetUser.Username, NewPasswordInput.Password, ConfirmNewPasswordInput.Password);

        if (result.IsSuccess)
        {
            DialogResult = true;
            Close();
        }
        else
        {
            MessageText.Text = result.Message;
        }
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}