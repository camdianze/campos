using PharmaPOS.Application.PasswordPolicy;
using PharmaPOS.Application.Repositories;
using PharmaPOS.Application.Security;
using PharmaPOS.Domain.Entities;
using PharmaPOS.Domain.Enums;

namespace PharmaPOS.Application.Authentication;

/// <summary>
/// IUserManagementService의 구현체.
/// Screen SCR-USER-016, 4절 흐름을 그대로 코드로 옮긴 것이다.
/// </summary>
public class UserManagementService : IUserManagementService
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IPasswordPolicyValidator _passwordPolicyValidator;

    public UserManagementService(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IPasswordPolicyValidator passwordPolicyValidator)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _passwordPolicyValidator = passwordPolicyValidator;
    }

    public async Task<UserManagementResult> CreateUserAsync(
        string facilityId, string username, string password, string confirmPassword, UserRole? role)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return UserManagementResult.Failure("Please enter the username.");
        }

        if (role is null)
        {
            return UserManagementResult.Failure("Please select a role.");
        }

        if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(confirmPassword))
        {
            return UserManagementResult.Failure("Please enter and confirm the password.");
        }

        if (password != confirmPassword)
        {
            return UserManagementResult.Failure("Password and confirmation do not match.");
        }

        var existingUser = await _userRepository.GetByUsernameAsync(username);
        if (existingUser is not null)
        {
            return UserManagementResult.Failure("This username is already in use.");
        }

        var policyResult = _passwordPolicyValidator.Validate(password, username);
        if (!policyResult.IsValid)
        {
            return UserManagementResult.Failure(policyResult.ErrorMessage!);
        }

        var newUser = new User
        {
            UserId = Guid.NewGuid().ToString(),
            FacilityId = facilityId,
            Username = username,
            PasswordHash = _passwordHasher.Hash(password),
            Role = role.Value,
            Status = EntityStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };

        try
        {
            await _userRepository.InsertAsync(newUser);
        }
        catch (Exception)
        {
            return UserManagementResult.Failure("User could not be created.");
        }

        return UserManagementResult.Success();
    }

    public async Task<UserManagementResult> DeactivateUserAsync(string targetUserId, string currentUserId)
    {
        if (targetUserId == currentUserId)
        {
            return UserManagementResult.Failure("You cannot deactivate your own account.");
        }

        try
        {
            await _userRepository.UpdateStatusAsync(targetUserId, EntityStatus.Inactive);
        }
        catch (Exception)
        {
            return UserManagementResult.Failure("User could not be deactivated.");
        }

        return UserManagementResult.Success();
    }

    public async Task<UserManagementResult> UpdateRoleAsync(string targetUserId, UserRole newRole)
    {
        try
        {
            await _userRepository.UpdateRoleAsync(targetUserId, newRole);
        }
        catch (Exception)
        {
            return UserManagementResult.Failure("User could not be updated.");
        }

        return UserManagementResult.Success();
    }

    public async Task<UserManagementResult> ResetPasswordAsync(
        string targetUserId, string username, string newPassword, string confirmPassword)
    {
        if (string.IsNullOrWhiteSpace(newPassword) || string.IsNullOrWhiteSpace(confirmPassword))
        {
            return UserManagementResult.Failure("Please enter and confirm the password.");
        }

        if (newPassword != confirmPassword)
        {
            return UserManagementResult.Failure("Password and confirmation do not match.");
        }

        var policyResult = _passwordPolicyValidator.Validate(newPassword, username);
        if (!policyResult.IsValid)
        {
            return UserManagementResult.Failure(policyResult.ErrorMessage!);
        }

        var newPasswordHash = _passwordHasher.Hash(newPassword);

        try
        {
            await _userRepository.UpdatePasswordHashAsync(targetUserId, newPasswordHash);
        }
        catch (Exception)
        {
            return UserManagementResult.Failure("Password could not be reset.");
        }

        return UserManagementResult.Success();
    }
}