namespace PharmaPOS.Application.Import;

/// <summary>
/// 건너뛴 행 하나와 그 사유. 미리보기와 결과 화면이 같은 목록을 쓴다 —
/// "몇 건 실패"만 남기면 파일의 어디를 고쳐야 하는지 알 수 없다.
/// </summary>
/// <param name="LineNumber">파일에서 사람이 보는 행 번호(헤더가 1행).</param>
public sealed record ImportIssue(int LineNumber, string Reason)
{
    public override string ToString() => $"Line {LineNumber}: {Reason}";
}
