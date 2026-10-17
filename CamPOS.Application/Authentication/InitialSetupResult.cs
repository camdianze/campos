namespace PharmaPOS.Application.Authentication;

/// <summary>
/// 초기 시설 설정 시도 결과.
/// </summary>
public class InitialSetupResult
{
    public bool IsSuccess { get; }
    public string? ErrorMessage { get; }

    private InitialSetupResult(bool isSuccess, string? errorMessage)
    {
        IsSuccess = isSuccess;
        ErrorMessage = errorMessage;
    }

    public static InitialSetupResult Success() => new(true, null);

    public static InitialSetupResult Failure(string errorMessage) => new(false, errorMessage);
}