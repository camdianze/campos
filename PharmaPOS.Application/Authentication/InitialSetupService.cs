using PharmaPOS.Application.PasswordPolicy;
using PharmaPOS.Application.Repositories;
using PharmaPOS.Application.Security;
using PharmaPOS.Domain.Entities;
using PharmaPOS.Domain.Enums;

namespace PharmaPOS.Application.Authentication;

/// <summary>
/// IInitialSetupService의 구현체.
/// Screen SCR-SETUP-003, 4.1절 흐름을 그대로 코드로 옮긴 것이다.
/// 보안 질문/답은 관리자 본인 비밀번호 복구를 위해 필수로 등록받는다 (제품 오너 결정).
/// </summary>
public class InitialSetupService : IInitialSetupService
{
    private readonly IInitialSetupRepository _initialSetupRepository;
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IPasswordPolicyValidator _passwordPolicyValidator;

    public InitialSetupService(
        IInitialSetupRepository initialSetupRepository,
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IPasswordPolicyValidator passwordPolicyValidator)
    {
        _initialSetupRepository = initialSetupRepository;
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _passwordPolicyValidator = passwordPolicyValidator;
    }

    public async Task<InitialSetupResult> CompleteSetupAsync(
        string facilityName,
        string country,
        string district,
        FacilityType facilityType,
        string adminUsername,
        string adminPassword,
        string confirmAdminPassword,
        string securityQuestion,
        string securityAnswer)
    {
        if (string.IsNullOrWhiteSpace(facilityName))
        {
            return InitialSetupResult.Failure("Please enter the facility name.");
        }

        if (string.IsNullOrWhiteSpace(country))
        {
            return InitialSetupResult.Failure("Please enter the country.");
        }

        if (string.IsNullOrWhiteSpace(district))
        {
            return InitialSetupResult.Failure("Please enter the province or district.");
        }

        if (string.IsNullOrWhiteSpace(adminUsername))
        {
            return InitialSetupResult.Failure("Please enter the administrator username.");
        }

        if (string.IsNullOrWhiteSpace(adminPassword))
        {
            return InitialSetupResult.Failure("Please enter a password.");
        }

        if (string.IsNullOrWhiteSpace(confirmAdminPassword))
        {
            return InitialSetupResult.Failure("Please confirm the password.");
        }

        if (adminPassword != confirmAdminPassword)
        {
            return InitialSetupResult.Failure("Password and confirmation do not match.");
        }

        var policyResult = _passwordPolicyValidator.Validate(adminPassword, adminUsername);
        if (!policyResult.IsValid)
        {
            // 검증기가 돌려준 사유를 그대로 보여준다. 여기서 뭉뚱그리면
            // 첫 계정을 만드는 사람이 무엇을 고쳐야 하는지 알 수 없다.
            return InitialSetupResult.Failure(policyResult.ErrorMessage!);
        }

        // 보안 질문/답은 관리자 본인 비밀번호 복구를 위한 유일한 안전망이므로 필수로 요구한다.
        if (string.IsNullOrWhiteSpace(securityQuestion))
        {
            return InitialSetupResult.Failure("Please select a security question.");
        }

        if (string.IsNullOrWhiteSpace(securityAnswer))
        {
            return InitialSetupResult.Failure("Please enter the answer to your security question.");
        }

        var existingUser = await _userRepository.GetByUsernameAsync(adminUsername);
        if (existingUser is not null)
        {
            return InitialSetupResult.Failure("This username is already in use.");
        }

        var facilityId = Guid.NewGuid().ToString();

        var facility = new Facility
        {
            FacilityId = facilityId,
            FacilityName = facilityName,
            Country = country,
            District = district,
            FacilityType = facilityType,
            Status = EntityStatus.Active
        };

        // 답변을 소문자로 정규화한 뒤 해시 — RecoverySettingsService와 동일한 정규화 규칙을 적용한다.
        var securityAnswerHash = _passwordHasher.Hash(securityAnswer.Trim().ToLowerInvariant());

        var adminUser = new User
        {
            UserId = Guid.NewGuid().ToString(),
            FacilityId = facilityId,
            Username = adminUsername,
            PasswordHash = _passwordHasher.Hash(adminPassword),
            Role = UserRole.Administrator,
            Status = EntityStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            SecurityQuestion = securityQuestion,
            SecurityAnswerHash = securityAnswerHash
        };

        try
        {
            await _initialSetupRepository.SaveFacilityAndAdminAsync(facility, adminUser);
        }
        catch (Exception)
        {
            return InitialSetupResult.Failure("Initial setup could not be completed. Please try again.");
        }

        return InitialSetupResult.Success();
    }
}