using PharmaPOS.Application.Authentication;
using Lightweight_Digital_Inventory_Management___POS_System.ViewModels.Base;

namespace Lightweight_Digital_Inventory_Management___POS_System.ViewModels;

/// <summary>
/// 로그인 화면의 "Forgot Password?"에서 진입하는 복구 화면의 ViewModel.
/// 3단계: 1) 아이디 입력, 2) 보안질문 또는 이메일 OTP로 검증, 3) 새 비밀번호 설정.
/// </summary>
public class PasswordRecoveryViewModel : ViewModelBase
{
    private readonly IPasswordRecoveryService _passwordRecoveryService;

    private string _username = string.Empty;
    private RecoveryMethodInfo? _availableMethods;
    private bool _isEmailMethodChosen;
    private string _securityAnswer = string.Empty;
    private string _otpCode = string.Empty;
    private string? _verifiedToken;
    private string _message = string.Empty;

    private int _currentStep = 1;
    public int CurrentStep
    {
        get => _currentStep;
        private set
        {
            if (SetProperty(ref _currentStep, value))
            {
                OnPropertyChanged(nameof(IsStep1));
                OnPropertyChanged(nameof(IsStep2));
                OnPropertyChanged(nameof(IsStep3));
            }
        }
    }

    public bool IsStep1 => CurrentStep == 1;
    public bool IsStep2 => CurrentStep == 2;
    public bool IsStep3 => CurrentStep == 3;

    public string Username { get => _username; set => SetProperty(ref _username, value); }

    public RecoveryMethodInfo? AvailableMethods { get => _availableMethods; private set => SetProperty(ref _availableMethods, value); }

    public bool IsEmailMethodChosen
    {
        get => _isEmailMethodChosen;
        set
        {
            if (SetProperty(ref _isEmailMethodChosen, value))
            {
                OnPropertyChanged(nameof(IsSecurityQuestionMethodChosen));
            }
        }
    }

    public bool IsSecurityQuestionMethodChosen => !IsEmailMethodChosen;

    public string SecurityAnswer { get => _securityAnswer; set => SetProperty(ref _securityAnswer, value); }
    public string OtpCode { get => _otpCode; set => SetProperty(ref _otpCode, value); }

    public string Message { get => _message; set => SetProperty(ref _message, value); }

    public RelayCommand FindAccountCommand { get; }
    public RelayCommand ChooseSecurityQuestionCommand { get; }
    public RelayCommand ChooseEmailCommand { get; }
    public RelayCommand VerifySecurityAnswerCommand { get; }
    public RelayCommand SendOtpCommand { get; }
    public RelayCommand VerifyOtpCommand { get; }
    public RelayCommand CancelCommand { get; }

    public event Action? NavigateBackToLogin;
    public event Action? PasswordResetSucceeded;

    public PasswordRecoveryViewModel(IPasswordRecoveryService passwordRecoveryService)
    {
        _passwordRecoveryService = passwordRecoveryService;

        FindAccountCommand = new RelayCommand(async _ => await ExecuteFindAccountAsync());
        ChooseSecurityQuestionCommand = new RelayCommand(_ => IsEmailMethodChosen = false);
        ChooseEmailCommand = new RelayCommand(_ => IsEmailMethodChosen = true);
        VerifySecurityAnswerCommand = new RelayCommand(async _ => await ExecuteVerifySecurityAnswerAsync());
        SendOtpCommand = new RelayCommand(async _ => await ExecuteSendOtpAsync());
        VerifyOtpCommand = new RelayCommand(async _ => await ExecuteVerifyOtpAsync());
        CancelCommand = new RelayCommand(_ => NavigateBackToLogin?.Invoke());
    }

    private async Task ExecuteFindAccountAsync()
    {
        Message = string.Empty;

        if (string.IsNullOrWhiteSpace(Username))
        {
            Message = "Please enter your username.";
            return;
        }

        AvailableMethods = await _passwordRecoveryService.GetAvailableRecoveryMethodsAsync(Username);

        if (AvailableMethods.NoRecoveryMethodAvailable)
        {
            Message = "No recovery method is available for this account. Please contact your administrator.";
            return;
        }

        IsEmailMethodChosen = !AvailableMethods.HasSecurityQuestion && AvailableMethods.IsEmailUsable;

        CurrentStep = 2;
    }

    private async Task ExecuteVerifySecurityAnswerAsync()
    {
        Message = string.Empty;

        var result = await _passwordRecoveryService.VerifySecurityAnswerAsync(Username, SecurityAnswer);

        if (result.IsSuccess)
        {
            _verifiedToken = result.VerifiedToken;
            CurrentStep = 3;
        }
        else
        {
            Message = result.Message!;
        }
    }

    private async Task ExecuteSendOtpAsync()
    {
        Message = string.Empty;

        var result = await _passwordRecoveryService.SendEmailOtpAsync(Username);

        Message = result.IsSuccess ? "A recovery code has been sent to your email." : result.Message!;
    }

    private async Task ExecuteVerifyOtpAsync()
    {
        Message = string.Empty;

        var result = await _passwordRecoveryService.VerifyEmailOtpAsync(Username, OtpCode);

        if (result.IsSuccess)
        {
            _verifiedToken = result.VerifiedToken;
            CurrentStep = 3;
        }
        else
        {
            Message = result.Message!;
        }
    }

    public async void ResetPassword(string newPassword, string confirmNewPassword)
    {
        Message = string.Empty;

        if (_verifiedToken is null)
        {
            Message = "Please verify your identity first.";
            return;
        }

        var result = await _passwordRecoveryService.ResetPasswordAsync(Username, _verifiedToken, newPassword, confirmNewPassword);

        if (result.IsSuccess)
        {
            PasswordResetSucceeded?.Invoke();
        }
        else
        {
            Message = result.Message!;
        }
    }
}