using System.Windows.Media;
using PharmaPOS.Application.Authentication;
using PharmaPOS.Domain.Entities;
using Lightweight_Digital_Inventory_Management___POS_System.ViewModels.Base;

namespace Lightweight_Digital_Inventory_Management___POS_System.ViewModels;

/// <summary>
/// 비밀번호 변경 화면(SCR-AUTH-002)의 ViewModel.
/// </summary>
public class ChangePasswordViewModel : ViewModelBase
{
    private readonly IChangePasswordService _changePasswordService;
    private readonly User _currentUser;

    private string _message = string.Empty;
    private Brush _messageColor = Brushes.Red;

    public string Message
    {
        get => _message;
        set => SetProperty(ref _message, value);
    }

    public Brush MessageColor
    {
        get => _messageColor;
        set => SetProperty(ref _messageColor, value);
    }

    public RelayCommand CancelCommand { get; }

    public event Action? NavigateBackToLogin;

    public ChangePasswordViewModel(IChangePasswordService changePasswordService, User currentUser)
    {
        _changePasswordService = changePasswordService;
        _currentUser = currentUser;

        CancelCommand = new RelayCommand(_ => NavigateBackToLogin?.Invoke());
    }

    public async void ChangePassword(string currentPassword, string newPassword, string confirmNewPassword)
    {
        Message = string.Empty;

        var result = await _changePasswordService.ChangePasswordAsync(
            _currentUser, currentPassword, newPassword, confirmNewPassword);

        if (result.IsSuccess)
        {
            MessageColor = Brushes.Green;
            Message = "Password changed successfully. Please log in again.";
            NavigateBackToLogin?.Invoke();
        }
        else
        {
            MessageColor = Brushes.Red;
            Message = result.ErrorMessage!;
        }
    }
}