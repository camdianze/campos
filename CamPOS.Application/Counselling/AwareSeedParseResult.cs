namespace PharmaPOS.Application.Counselling;

/// <summary>
/// 시드 CSV 파싱 결과.
/// 잘못된 줄이 있어도 전체를 실패시키지 않는다 — 읽을 수 있는 줄은 적재하고,
/// 나머지는 Errors에 모아 관리자가 시드 파일을 고칠 수 있게 한다.
/// 단, 헤더 자체가 잘못된 경우는 파일 전체가 무의미하므로 실패로 처리한다.
/// </summary>
public class AwareSeedParseResult
{
    public bool IsSuccess { get; }

    /// <summary>헤더가 잘못돼 파일 전체를 읽을 수 없을 때의 사유.</summary>
    public string? Message { get; }

    public IReadOnlyList<AwareSeedRow> Rows { get; }

    /// <summary>건너뛴 줄들의 사유. "3행: unknown aware_group 'ACESS'" 형태.</summary>
    public IReadOnlyList<string> Errors { get; }

    private AwareSeedParseResult(
        bool isSuccess, string? message, IReadOnlyList<AwareSeedRow> rows, IReadOnlyList<string> errors)
    {
        IsSuccess = isSuccess;
        Message = message;
        Rows = rows;
        Errors = errors;
    }

    public static AwareSeedParseResult Success(IReadOnlyList<AwareSeedRow> rows, IReadOnlyList<string> errors)
        => new(true, null, rows, errors);

    public static AwareSeedParseResult Failure(string message)
        => new(false, message, Array.Empty<AwareSeedRow>(), Array.Empty<string>());
}
