namespace PharmaPOS.Application.Receipts;

/// <summary>
/// 영수증 설정 저장 시도 결과.
///
/// 이 저장소의 다른 결과 객체와 달리 항목별 오류를 함께 돌려준다.
/// 설정 화면은 잘못된 입력 옆에 무엇이 잘못됐고 어떻게 고치는지를 보여줘야 하는데,
/// 메시지 하나만 돌려주면 어느 칸이 문제인지 화면이 알 수 없다.
/// </summary>
public class ReceiptSettingsSaveResult
{
    private static readonly IReadOnlyDictionary<string, string> NoErrors =
        new Dictionary<string, string>();

    public bool IsSuccess { get; }

    public string? Message { get; }

    /// <summary>설정 키 → 그 칸에 표시할 문구. 성공이면 비어 있다.</summary>
    public IReadOnlyDictionary<string, string> FieldErrors { get; }

    private ReceiptSettingsSaveResult(
        bool isSuccess, string? message, IReadOnlyDictionary<string, string> fieldErrors)
    {
        IsSuccess = isSuccess;
        Message = message;
        FieldErrors = fieldErrors;
    }

    public static ReceiptSettingsSaveResult Success(string message) =>
        new(true, message, NoErrors);

    public static ReceiptSettingsSaveResult Failure(string message) =>
        new(false, message, NoErrors);

    public static ReceiptSettingsSaveResult Invalid(IReadOnlyDictionary<string, string> fieldErrors) =>
        new(false, null, fieldErrors);
}
