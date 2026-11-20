namespace CamPOS.Application.Counselling;

/// <summary>
/// 복약안내 설정 저장 시도 결과.
///
/// 저장이 거부될 수 있게 된 뒤에 생긴 타입이다. 전에는 <c>SaveAsync</c>가 아무것도
/// 돌려주지 않았고, 화면이 "저장됐다"를 자기 판단으로 적었다 — 거부가 생기는 순간
/// 그 문장은 거짓이 된다.
/// </summary>
public class CounsellingSettingsSaveResult
{
    private CounsellingSettingsSaveResult(bool isSuccess, string message)
    {
        IsSuccess = isSuccess;
        Message = message;
    }

    public bool IsSuccess { get; }

    /// <summary>화면에 그대로 띄우는 문구. 성공일 때도 비어 있지 않다.</summary>
    public string Message { get; }

    public static CounsellingSettingsSaveResult Success() =>
        new(true, "Settings saved.");

    public static CounsellingSettingsSaveResult Failure(string message) =>
        new(false, message);

    /// <summary>
    /// 관리자가 아니다. 이 문구는 무엇을 하면 되는지까지 말한다 — "권한이 없습니다"에서
    /// 끝나면 읽은 사람이 할 수 있는 일이 없다.
    /// </summary>
    public static CounsellingSettingsSaveResult NotAdministrator() =>
        new(false, "Only an administrator can change counselling settings. "
                   + "Ask the pharmacy owner to sign in and change them.");
}
