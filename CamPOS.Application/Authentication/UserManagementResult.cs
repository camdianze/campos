namespace PharmaPOS.Application.Authentication;

/// <summary>
/// 사용자 관리(생성/역할변경/비활성화/비밀번호초기화) 시도 결과.
/// </summary>
public class UserManagementResult
{
    public bool IsSuccess { get; }
    public string? Message { get; }

    private UserManagementResult(bool isSuccess, string? message)
    {
        IsSuccess = isSuccess;
        Message = message;
    }

    public static UserManagementResult Success() => new(true, null);

    public static UserManagementResult Failure(string message) => new(false, message);
}