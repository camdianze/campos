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

    /// <summary>취소했을 때. 아무것도 바뀌지 않았으므로 들어온 화면으로 되돌아간다.</summary>
    public event Action? NavigateBack;

    /// <summary>비밀번호가 실제로 바뀌었을 때. 기존 세션을 유지할 수 없어 재로그인이 필요하다.</summary>
    public event Action? NavigateBackToLogin;

    public ChangePasswordViewModel(IChangePasswordService changePasswordService, User currentUser)
    {
        _changePasswordService = changePasswordService;
        _currentUser = currentUser;

        CancelCommand = new RelayCommand(_ => NavigateBack?.Invoke());
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