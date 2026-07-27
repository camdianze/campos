using PharmaPOS.Application.PasswordPolicy;
using PharmaPOS.Application.Repositories;
using PharmaPOS.Application.Security;

namespace PharmaPOS.Application.Authentication;

/// <summary>IPasswordRecoveryService의 구현체.</summary>
public class PasswordRecoveryService : IPasswordRecoveryService
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IPasswordPolicyValidator _passwordPolicyValidator;
    private readonly IRecoveryDataProtector _dataProtector;
    private readonly IEmailSendingService _emailSendingService;

    private static readonly Dictionary<string, (string CodeHash, DateTime ExpiresAt)> _pendingOtps = new();
    private static readonly Dictionary<string, string> _verifiedTokens = new();

    public PasswordRecoveryService(
        IUserRepository userRepository, IPasswordHasher passwordHasher,
        IPasswordPolicyValidator passwordPolicyValidator, IRecoveryDataProtector dataProtector,
        IEmailSendingService emailSendingService)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _passwordPolicyValidator = passwordPolicyValidator;
        _dataProtector = dataProtector;
        _emailSendingService = emailSendingService;
    }

    public async Task<RecoveryMethodInfo> GetAvailableRecoveryMethodsAsync(string username)
    {
        var user = await _userRepository.GetByUsernameAsync(username);

        if (user is null)
        {
            return new RecoveryMethodInfo();
        }

        var hasEmail = !string.IsNullOrWhiteSpace(user.RecoveryEmail) && user.EmailProvider is not null;
        var isInternetAvailable = hasEmail && await _emailSendingService.IsInternetAvailableAsync();

        return new RecoveryMethodInfo
        {
            HasSecurityQuestion = !string.IsNullOrWhiteSpace(user.SecurityQuestion),
            SecurityQuestion = user.SecurityQuestion,
            HasEmail = hasEmail,
            IsEmailUsable = isInternetAvailable
        };
    }

    public async Task<PasswordRecoveryResult> VerifySecurityAnswerAsync(string username, string answer)
    {
        var user = await _userRepository.GetByUsernameAsync(username);

        if (user is null || string.IsNullOrWhiteSpace(user.SecurityAnswerHash))
        {
            return PasswordRecoveryResult.Failure("Recovery could not be verified.");
        }

        var normalizedAnswer = answer.Trim().ToLowerInvariant();
        var isCorrect = _passwordHasher.Verify(normalizedAnswer, user.SecurityAnswerHash);

        if (!isCorrect)
        {
            return PasswordRecoveryResult.Failure("Recovery could not be verified.");
        }

        var token = Guid.NewGuid().ToString();
        _verifiedTokens[username] = token;
        return PasswordRecoveryResult.Success(token);
    }

    public async Task<PasswordRecoveryResult> SendEmailOtpAsync(string username)
    {
        var user = await _userRepository.GetByUsernameAsync(username);

        if (user is null || string.IsNullOrWhiteSpace(user.RecoveryEmail) || user.EmailProvider is null
            || string.IsNullOrWhiteSpace(user.EmailAppPasswordEncrypted))
        {
            return PasswordRecoveryResult.Failure("Email recovery is not available for this account.");
        }

        var otpCode = Random.Shared.Next(100000, 999999).ToString();
        var appPassword = _dataProtector.Unprotect(user.EmailAppPasswordEncrypted);

        var sent = await _emailSendingService.SendOtpCodeAsync(
            user.RecoveryEmail, otpCode, user.EmailProvider.Value,
            user.SmtpHost, user.SmtpPort, user.RecoveryEmail, appPassword);

        if (!sent)
        {
            return PasswordRecoveryResult.Failure("The recovery code could not be sent. Please try again.");
        }

        _pendingOtps[username] = (_passwordHasher.Hash(otpCode), DateTime.UtcNow.AddMinutes(10));

        return PasswordRecoveryResult.Success();
    }
    public async Task<PasswordRecoveryResult> SendUsernameByEmailAsync(string recoveryEmail)
    {
        if (string.IsNullOrWhiteSpace(recoveryEmail))
        {
            return PasswordRecoveryResult.Failure("Please enter your email address.");
        }

        // 보안 원칙: 이메일이 등록되어 있든 없든, 아래 두 갈래 모두 사용자에게는
        // 반드시 동일한 성공 메시지를 반환해야 한다. 여기서는 실제 발송 여부만 갈릴 뿐,
        // 반환값(PasswordRecoveryResult.Success())은 항상 같다.
        try
        {
            var user = await _userRepository.GetByRecoveryEmailAsync(recoveryEmail);

            if (user is not null && user.EmailProvider is not null && !string.IsNullOrWhiteSpace(user.EmailAppPasswordEncrypted))
            {
                var appPassword = _dataProtector.Unprotect(user.EmailAppPasswordEncrypted);

                // 발송 실패(네트워크 문제 등)도 사용자에게 노출하지 않는다 — 실패 사실 자체가
                // "이 이메일은 등록되어 있었다"는 정보를 흘릴 수 있기 때문에 조용히 무시한다.
                await _emailSendingService.SendUsernameAsync(
                    recoveryEmail, user.Username, user.EmailProvider.Value,
                    user.SmtpHost, user.SmtpPort, recoveryEmail, appPassword);
            }
        }
        catch (Exception)
        {
            // 위와 같은 이유로 예외도 조용히 무시한다.
        }

        return PasswordRecoveryResult.Success();
    }
    public Task<PasswordRecoveryResult> VerifyEmailOtpAsync(string username, string enteredCode)
    {
        if (!_pendingOtps.TryGetValue(username, out var pending))
        {
            return Task.FromResult(PasswordRecoveryResult.Failure("Please request a new recovery code."));
        }

        if (DateTime.UtcNow > pending.ExpiresAt)
        {
            _pendingOtps.Remove(username);
            return Task.FromResult(PasswordRecoveryResult.Failure("This code has expired. Please request a new one."));
        }

        var isCorrect = _passwordHasher.Verify(enteredCode.Trim(), pending.CodeHash);

        if (!isCorrect)
        {
            return Task.FromResult(PasswordRecoveryResult.Failure("Incorrect code. Please try again."));
        }

        _pendingOtps.Remove(username);
        var token = Guid.NewGuid().ToString();
        _verifiedTokens[username] = token;

        return Task.FromResult(PasswordRecoveryResult.Success(token));
    }

    public async Task<PasswordRecoveryResult> ResetPasswordAsync(
        string username, string verifiedToken, string newPassword, string confirmNewPassword)
    {
        if (!_verifiedTokens.TryGetValue(username, out var expectedToken) || expectedToken != verifiedToken)
        {
            return PasswordRecoveryResult.Failure("Recovery session has expired. Please start over.");
        }

        if (newPassword != confirmNewPassword)
        {
            return PasswordRecoveryResult.Failure("Password and confirmation do not match.");
        }

        var policyResult = _passwordPolicyValidator.Validate(newPassword, username);
        if (!policyResult.IsValid)
        {
            return PasswordRecoveryResult.Failure(policyResult.ErrorMessage!);
        }

        var user = await _userRepository.GetByUsernameAsync(username);
        if (user is null)
        {
            return PasswordRecoveryResult.Failure("Recovery session has expired. Please start over.");
        }

        try
        {
            await _userRepository.UpdatePasswordHashAsync(user.UserId, _passwordHasher.Hash(newPassword));
        }
        catch (Exception)
        {
            return PasswordRecoveryResult.Failure("Password could not be reset. Please try again.");
        }

        _verifiedTokens.Remove(username);
        return PasswordRecoveryResult.Success();
    }
}