namespace PharmaPOS.Application.Counselling;

/// <summary>
/// 시드 적재 시도 결과.
/// 실패하더라도 앱은 계속 뜬다 — 참조 데이터가 없으면 모든 상품이 unmatched가 될 뿐,
/// 판매 자체는 정상 동작해야 한다.
/// </summary>
public class AwareSeedLoadResult
{
    public bool IsSuccess { get; }

    public string? Message { get; }

    /// <summary>파일 내용이 그대로여서 다시 적재하지 않은 경우 true.</summary>
    public bool WasAlreadyUpToDate { get; }

    public int LoadedCount { get; }

    /// <summary>형식이 잘못돼 건너뛴 줄 수.</summary>
    public int SkippedCount { get; }

    public IReadOnlyList<string> RowErrors { get; }

    public string? SourceVersion { get; }

    private AwareSeedLoadResult(
        bool isSuccess,
        string? message,
        bool wasAlreadyUpToDate,
        int loadedCount,
        int skippedCount,
        IReadOnlyList<string> rowErrors,
        string? sourceVersion)
    {
        IsSuccess = isSuccess;
        Message = message;
        WasAlreadyUpToDate = wasAlreadyUpToDate;
        LoadedCount = loadedCount;
        SkippedCount = skippedCount;
        RowErrors = rowErrors;
        SourceVersion = sourceVersion;
    }

    public static AwareSeedLoadResult Loaded(
        int loadedCount, int skippedCount, IReadOnlyList<string> rowErrors, string? sourceVersion)
        => new(true, null, false, loadedCount, skippedCount, rowErrors, sourceVersion);

    public static AwareSeedLoadResult AlreadyUpToDate(int loadedCount, string? sourceVersion)
        => new(true, null, true, loadedCount, 0, Array.Empty<string>(), sourceVersion);

    public static AwareSeedLoadResult Failure(string message)
        => new(false, message, false, 0, 0, Array.Empty<string>(), null);
}
