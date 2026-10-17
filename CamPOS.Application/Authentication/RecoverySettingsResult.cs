namespace PharmaPOS.Application.Authentication;

/// <summary>복구 정보(보안질문/이메일) 등록/수정 시도 결과.</summary>
public class RecoverySettingsResult
{
    public bool IsSuccess { get; }
    public string? Message { get; }

    private RecoverySettingsResult(bool isSuccess, string? message) { IsSuccess = isSuccess; Message = message; }

    public static RecoverySettingsResult Success() => new(true, null);
    public static RecoverySettingsResult Failure(string message) => new(false, message);
}