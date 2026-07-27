namespace PharmaPOS.Application.Authentication;

/// <summary>
/// F-01 로그인 로직을 담당하는 인터페이스.
/// </summary>
public interface IAuthenticationService
{
    /// <summary>
    /// Screen 01, 6.1절의 로그인 흐름을 수행한다.
    /// </summary>
    Task<AuthenticationResult> LoginAsync(string username, string password);
}