namespace PharmaPOS.Application.Import;

/// <summary>
/// 임포트를 실제로 반영한 결과.
/// 미리보기에서 걸러진 행은 여기 실패로 다시 세지 않는다 — 사용자가 이미 보고 진행을 눌렀고,
/// 여기 숫자는 "넣기로 한 것 중 몇 건이 들어갔는가"여야 뜻이 분명하다.
/// </summary>
public sealed class ImportApplyResult
{
    public int SuccessCount { get; init; }
    public int FailureCount { get; init; }
    public IReadOnlyList<ImportIssue> Failures { get; init; } = [];

    /// <summary>임포트 이력을 남기지 못한 경우의 사유. 저장 자체는 이미 끝난 상태다.</summary>
    public string? HistoryWarning { get; init; }
}
