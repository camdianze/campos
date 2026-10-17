using System.Windows;
using CamPOS.Application.Authentication;
using CamPOS.Domain.Enums;

namespace CamPOS.Wpf.Views;

public partial class AddUserWindow : Window
{
    private readonly IUserManagementService _userManagementService;
    private readonly string _facilityId;

    public AddUserWindow(IUserManagementService userManagementService, string facilityId)
    {
        InitializeComponent();

        _userManagementService = userManagementService;
        _facilityId = facilityId;

        RoleComboBox.ItemsSource = Enum.GetValues<UserRole>();
    }

    private async void OnCreateClick(object sender, RoutedEventArgs e)
    {
        MessageText.Text = string.Empty;

        UserRole? selectedRole = RoleComboBox.SelectedItem as UserRole?;

        var result = await _userManagementService.CreateUserAsync(
            _facilityId,
            UsernameInput.Text,
            PasswordInput.Password,
            ConfirmPasswordInput.Password,
            selectedRole);

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