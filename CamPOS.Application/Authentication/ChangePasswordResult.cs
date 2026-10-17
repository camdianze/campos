namespace PharmaPOS.Application.Authentication;

/// <summary>
/// 비밀번호 변경 시도 결과.
/// </summary>
public class ChangePasswordResult
{
    public bool IsSuccess { get; }
    public string? ErrorMessage { get; }

    private ChangePasswordResult(bool isSuccess, string? errorMessage)
    {
        IsSuccess = isSuccess;
        ErrorMessage = errorMessage;
    }

    public static ChangePasswordResult Success() => new(true, null);

    public static ChangePasswordResult Failure(string errorMessage) => new(false, errorMessage);
}