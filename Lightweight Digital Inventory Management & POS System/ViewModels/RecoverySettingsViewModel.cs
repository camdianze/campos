using PharmaPOS.Application.Authentication;
using PharmaPOS.Domain.Enums;
using Lightweight_Digital_Inventory_Management___POS_System.ViewModels.Base;

namespace Lightweight_Digital_Inventory_Management___POS_System.ViewModels;

/// <summary>
/// Account Recovery Settings 화면의 ViewModel.
/// 로그인한 관리자 본인이 비밀번호 복구 수단(보안질문/이메일)을 등록/수정한다.
/// </summary>
public class RecoverySettingsViewModel : ViewModelBase
{
    private readonly IRecoverySettingsService _recoverySettingsService;
    private readonly string _userId;
    private readonly string _username;

    private string? _selectedSecurityQuestion;
    private string _securityAnswer = string.Empty;
    private string _recoveryEmail = string.Empty;
    private EmailProvider? _selectedEmailProvider;
    private string _emailAppPassword = string.Empty;
    private string _smtpHost = string.Empty;
    private string _smtpPort = string.Empty;
    private string _message = string.Empty;

    public IReadOnlyList<string> AvailableSecurityQuestions { get; } = new[]
    {
        "What was the name of your first pet?",
        "What is your mother's maiden name?",
        "What was the name of your first school?",
        "What city were you born in?"
    };

    public string? SelectedSecurityQuestion { get => _selectedSecurityQuestion; set => SetProperty(ref _selectedSecurityQuestion, value); }
    public string SecurityAnswer { get => _securityAnswer; set => SetProperty(ref _securityAnswer, value); }

    public string RecoveryEmail { get => _recoveryEmail; set => SetProperty(ref _recoveryEmail, value); }

    public IReadOnlyList<EmailProvider> AvailableEmailProviders { get; } = Enum.GetValues<EmailProvider>();

    public EmailProvider? SelectedEmailProvider
    {
        get => _selectedEmailProvider;
        set { if (SetProperty(ref _selectedEmailProvider, value)) OnPropertyChanged(nameof(IsOtherProvider)); }
    }

    /// <summary>Other 선택 시에만 SMTP 직접 입력칸을 보여준다.</summary>
    public bool IsOtherProvider => SelectedEmailProvider == EmailProvider.Other;

    public string EmailAppPassword { get => _emailAppPassword; set => SetProperty(ref _emailAppPassword, value); }
    public string SmtpHost { get => _smtpHost; set => SetProperty(ref _smtpHost, value); }
    public string SmtpPort { get => _smtpPort; set => SetProperty(ref _smtpPort, value); }

    public string Message { get => _message; set => SetProperty(ref _message, value); }

    public RelayCommand SaveCommand { get; }
    public RelayCommand BackCommand { get; }

    public event Action? NavigateBack;

    public RecoverySettingsViewModel(IRecoverySettingsService recoverySettingsService, string userId, string username)
    {
        _recoverySettingsService = recoverySettingsService;
        _userId = userId;
        _username = username;

        SaveCommand = new RelayCommand(async _ => await ExecuteSaveAsync());
        BackCommand = new RelayCommand(_ => NavigateBack?.Invoke());
    }

    private async Task ExecuteSaveAsync()
    {
        Message = string.Empty;

        int? smtpPortValue = null;
        if (IsOtherProvider && !string.IsNullOrWhiteSpace(SmtpPort))
        {
            if (!int.TryParse(SmtpPort, out var parsedPort))
            {
                Message = "Please enter a valid SMTP port number.";
                return;
            }
            smtpPortValue = parsedPort;
        }

        var result = await _recoverySettingsService.SaveRecoverySettingsAsync(
            _userId, _username,
            SelectedSecurityQuestion, string.IsNullOrWhiteSpace(SecurityAnswer) ? null : SecurityAnswer,
            string.IsNullOrWhiteSpace(RecoveryEmail) ? null : RecoveryEmail, SelectedEmailProvider,
            string.IsNullOrWhiteSpace(EmailAppPassword) ? null : EmailAppPassword,
            string.IsNullOrWhiteSpace(SmtpHost) ? null : SmtpHost, smtpPortValue);

        Message = result.IsSuccess ? "Recovery settings saved successfully." : result.Message!;
    }
}