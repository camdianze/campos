namespace PharmaPOS.Application.Inventory;

/// <summary>
/// 백업/내보내기/복원 시도 결과.
/// </summary>
public class BackupResult
{
    public bool IsSuccess { get; }
    public string? Message { get; }

    private BackupResult(bool isSuccess, string? message)
    {
        IsSuccess = isSuccess;
        Message = message;
    }

    public static BackupResult Success(string? message = null) => new(true, message);

    public static BackupResult Failure(string message) => new(false, message);
}