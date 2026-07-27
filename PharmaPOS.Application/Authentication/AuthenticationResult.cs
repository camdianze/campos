using PharmaPOS.Domain.Entities;

namespace PharmaPOS.Application.Authentication;

/// <summary>
/// 로그인 시도 결과. 성공 시 AuthenticatedUser에 값이 들어있고,
/// 실패 시 Error에 실패 사유가 들어있다.
/// </summary>
public class AuthenticationResult
{
    public bool IsSuccess { get; }
    public User? AuthenticatedUser { get; }
    public AuthenticationError Error { get; }

    private AuthenticationResult(bool isSuccess, User? user, AuthenticationError error)
    {
        IsSuccess = isSuccess;
        AuthenticatedUser = user;
        Error = error;
    }

    public static AuthenticationResult Success(User user) =>
        new(isSuccess: true, user: user, error: AuthenticationError.None);

    public static AuthenticationResult Failure(AuthenticationError error) =>
        new(isSuccess: false, user: null, error: error);
}