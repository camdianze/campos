namespace PharmaPOS.Application.Counselling;

/// <summary>
/// 복약안내 용지를 파일로 저장한다. 프린터를 거치지 않고 내용을 확인하는 용도다.
/// </summary>
public interface ICounsellingSheetFileWriter
{
    /// <summary>
    /// 용지를 텍스트 파일로 쓴다. 실패해도 예외를 던지지 않고 Failure를 돌려준다.
    /// </summary>
    /// <param name="folder">저장 폴더. 비어 있으면 기본 폴더를 쓴다.</param>
    /// <param name="fileNameHint">파일명에 넣을 식별자 (거래 ID 등).</param>
    Task<CounsellingPrintResult> WriteAsync(
        CounsellingSheetDocument document, string? folder, string fileNameHint);
}
