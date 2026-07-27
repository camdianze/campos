using PharmaPOS.Application.PasswordPolicy;
using PharmaPOS.Application.Repositories;
using PharmaPOS.Application.Security;
using PharmaPOS.Domain.Entities;
using PharmaPOS.Domain.Enums;

namespace PharmaPOS.Application.Authentication;

/// <summary>
/// IChangePasswordService의 구현체.
/// Screen 02, 4.1절 흐름(1~9단계)을 그대로 코드로 옮긴 것이다.
/// </summary>
public class ChangePasswordService : IChangePasswordService
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IPasswordPolicyValidator _passwordPolicyValidator;

    public ChangePasswordService(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IPasswordPolicyValidator passwordPolicyValidator)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _passwordPolicyValidator = passwordPolicyValidator;
    }

    public async Task<ChangePasswordResult> ChangePasswordAsync(
        User currentUser,
        string currentPassword,
        string newPassword,
        string confirmNewPassword)
    {
        // Screen 02, 4.3절: 입력값 미입력 검사
        if (string.IsNullOrWhiteSpace(currentPassword))
        {
            return ChangePasswordResult.Failure("Please enter your current password.");
        }

        if (string.IsNullOrWhiteSpace(newPassword))
        {
            return ChangePasswordResult.Failure("Please enter a new password.");
        }

        if (string.IsNullOrWhiteSpace(confirmNewPassword))
        {
            return ChangePasswordResult.Failure("Please confirm your new password.");
        }

        // 계정 상태 확인
        if (currentUser.Status != EntityStatus.Active)
        {
            return ChangePasswordResult.Failure("This account is inactive. Please contact the administrator.");
        }

        // 1~3단계: 현재 비밀번호 검증
        var isCurrentPasswordCorrect = _passwordHasher.Verify(currentPassword, currentUser.PasswordHash);
        if (!isCurrentPasswordCorrect)
        {
            return ChangePasswordResult.Failure("Current password is incorrect.");
        }

        // 4단계: 새 비밀번호 확인값 일치 검사
        if (newPassword != confirmNewPassword)
        {
            return ChangePasswordResult.Failure("New password and confirmation do not match.");
        }

        // 5단계: 비밀번호 규칙 검사 (username 동일 여부, 현재 비밀번호 동일 여부 포함)
        var policyResult = _passwordPolicyValidator.Validate(newPassword, currentUser.Username, currentPassword);
        if (!policyResult.IsValid)
        {
            return ChangePasswordResult.Failure(policyResult.ErrorMessage!);
        }

        // 6~7단계: 새 비밀번호 해싱 및 DB 업데이트
        var newPasswordHash = _passwordHasher.Hash(newPassword);

        try
        {
            await _userRepository.UpdatePasswordHashAsync(currentUser.UserId, newPasswordHash);
        }
        catch (Exception)
        {
            return ChangePasswordResult.Failure("Password could not be changed. Please try again.");
        }

        // 8~9단계: 성공. 세션 종료 및 로그인 화면 이동은 ViewModel(다음 Step)이 처리한다.
        return ChangePasswordResult.Success();
    }
}