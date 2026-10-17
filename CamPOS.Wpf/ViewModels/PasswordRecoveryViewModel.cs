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
    public RelayCommand BackCommand { get; }
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
        BackCommand = new RelayCommand(_ => ExecuteBack());
        CancelCommand = new RelayCommand(_ => NavigateBackToLogin?.Invoke());
    }

    /// <summary>
    /// 한 단계 되돌린다. 아이디를 잘못 쳤거나 인증 수단을 다시 고르려면 이 길밖에 없다 —
    /// 1단계에서 더 뒤로 가는 것은 Back이 아니라 Cancel(로그인 화면)이다.
    ///
    /// 지우는 범위가 단계마다 다르다. 3→2는 입력했던 답/코드만 비운다. 2→1은 아이디가
    /// 바뀔 수 있는 자리로 돌아가는 것이라, 앞 사람 아이디로 받아 둔 검증 토큰까지 버린다.
    /// </summary>
    private void ExecuteBack()
    {
        Message = string.Empty;
        SecurityAnswer = string.Empty;
        OtpCode = string.Empty;

        if (CurrentStep == 3)
        {
            // 토큰은 남겨 둔다. 이메일 OTP는 한 번 맞히면 그 코드가 소비되므로,
            // 여기서 토큰까지 버리면 용무 없이 되돌아본 사람도 코드를 다시 받아야 한다.
            // 3단계로 다시 가려면 어차피 이 화면의 Confirm을 통과해야 한다.
            CurrentStep = 2;
            return;
        }

        if (CurrentStep == 2)
        {
            _verifiedToken = null;
            CurrentStep = 1;
        }
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