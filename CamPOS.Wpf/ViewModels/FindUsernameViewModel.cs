using PharmaPOS.Application.Authentication;
using Lightweight_Digital_Inventory_Management___POS_System.ViewModels.Base;

namespace Lightweight_Digital_Inventory_Management___POS_System.ViewModels;

/// <summary>
/// 아이디 찾기 화면의 ViewModel.
/// 보안 원칙: 이메일 등록 여부와 무관하게 항상 동일한 안내 메시지만 보여준다.
/// </summary>
public class FindUsernameViewModel : ViewModelBase
{
    private readonly IPasswordRecoveryService _passwordRecoveryService;

    private string _recoveryEmail = string.Empty;
    private string _message = string.Empty;

    public string RecoveryEmail { get => _recoveryEmail; set => SetProperty(ref _recoveryEmail, value); }
    public string Message { get => _message; set => SetProperty(ref _message, value); }

    public RelayCommand FindUsernameCommand { get; }
    public RelayCommand CancelCommand { get; }

    public event Action? NavigateBackToLogin;

    public FindUsernameViewModel(IPasswordRecoveryService passwordRecoveryService)
    {
        _passwordRecoveryService = passwordRecoveryService;

        FindUsernameCommand = new RelayCommand(async _ => await ExecuteFindUsernameAsync());
        CancelCommand = new RelayCommand(_ => NavigateBackToLogin?.Invoke());
    }

    private async Task ExecuteFindUsernameAsync()
    {
        if (string.IsNullOrWhiteSpace(RecoveryEmail))
        {
            Message = "Please enter your email address.";
            return;
        }

        // 이 호출은 이메일 등록 여부와 무관하게 항상 Success를 반환한다 (보안 원칙).
        await _passwordRecoveryService.SendUsernameByEmailAsync(RecoveryEmail);

        Message = "If this email is registered, we've sent your username to it. Please check your inbox.";
    }
}