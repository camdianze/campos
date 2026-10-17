using PharmaPOS.Application.PasswordPolicy;
using PharmaPOS.Application.Repositories;
using PharmaPOS.Application.Security;
using PharmaPOS.Domain.Enums;

namespace PharmaPOS.Application.Authentication;

/// <summary>IRecoverySettingsService의 구현체.</summary>
public class RecoverySettingsService : IRecoverySettingsService
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IRecoveryDataProtector _dataProtector;

    public RecoverySettingsService(
        IUserRepository userRepository, IPasswordHasher passwordHasher, IRecoveryDataProtector dataProtector)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _dataProtector = dataProtector;
    }

    public async Task<RecoverySettingsResult> SaveRecoverySettingsAsync(
        string userId, string username,
        string? securityQuestion, string? securityAnswer,
        string? recoveryEmail, EmailProvider? emailProvider,
        string? emailAppPassword, string? smtpHost, int? smtpPort)
    {
        // 최소 하나의 복구 수단은 있어야 의미가 있다.
        var hasSecurityQuestion = !string.IsNullOrWhiteSpace(securityQuestion) && !string.IsNullOrWhiteSpace(securityAnswer);
        var hasEmail = !string.IsNullOrWhiteSpace(recoveryEmail) && emailProvider is not null && !string.IsNullOrWhiteSpace(emailAppPassword);

        if (!hasSecurityQuestion && !hasEmail)
        {
            return RecoverySettingsResult.Failure("Please set up at least one recovery method.");
        }

        if (emailProvider == Domain.Enums.EmailProvider.Other && string.IsNullOrWhiteSpace(smtpHost))
        {
            return RecoverySettingsResult.Failure("Please enter the SMTP server address.");
        }

        // 답변을 소문자로 정규화한 뒤 해시 — 대소문자 차이로 본인이 오답 처리되는 걸 방지.
        var securityAnswerHash = hasSecurityQuestion
            ? _passwordHasher.Hash(securityAnswer!.Trim().ToLowerInvariant())
            : null;

        var encryptedAppPassword = hasEmail
            ? _dataProtector.Protect(emailAppPassword!)
            : null;

        try
        {
            await _userRepository.UpdateRecoveryInfoAsync(
                userId,
                hasSecurityQuestion ? securityQuestion : null,
                securityAnswerHash,
                hasEmail ? recoveryEmail : null,
                hasEmail ? emailProvider : null,
                encryptedAppPassword,
                emailProvider == Domain.Enums.EmailProvider.Other ? smtpHost : null,
                emailProvider == Domain.Enums.EmailProvider.Other ? smtpPort : null);
        }
        catch (Exception)
        {
            return RecoverySettingsResult.Failure("Recovery settings could not be saved.");
        }

        return RecoverySettingsResult.Success();
    }
}