using PharmaPOS.Domain.Entities;

namespace PharmaPOS.Application.Authentication;

/// <summary>
/// F-01 비밀번호 변경 로직을 담당하는 인터페이스.
/// </summary>
public interface IChangePasswordService
{
    /// <summary>
    /// Screen 02, 4.1절의 비밀번호 변경 흐름을 수행한다.
    /// </summary>
    Task<ChangePasswordResult> ChangePasswordAsync(
        User currentUser,
        string currentPassword,
        string newPassword,
        string confirmNewPassword);
}