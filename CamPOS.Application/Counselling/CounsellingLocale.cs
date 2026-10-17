namespace PharmaPOS.Application.Counselling;

/// <summary>
/// 복합 문자 계열은 자소 결합 때문에 별도 처리가 필요하다는 표시.
/// (WPF 인쇄 경로에서는 텍스트 셰이핑이 렌더링 단계에서 처리되므로
///  실제 분기에 쓰이지는 않지만, 로케일 파일의 형식 계약이라 그대로 읽어 둔다.)
/// </summary>
public enum LocaleRenderMode
{
    Text,
    Raster
}

/// <summary>
/// 복약안내 용지의 현지어 레이어.
///
/// 영어는 고정 레이어라 이 객체가 없어도 항상 인쇄된다.
/// 이 객체는 "영어 줄 옆에 현지어를 덧붙일 수 있는가"만 판단한다.
/// 검수되지 않은 로케일(review_status != "approved")은 현지어를 내보내지 않는다 —
/// 잘못된 복약 문구가 환자에게 전달되는 것보다 영어 단독이 낫다.
/// </summary>
public class CounsellingLocale
{
    private readonly IReadOnlyDictionary<string, string> _strings;

    public string LocaleCode { get; }

    public string? LanguageName { get; }

    public string? Script { get; }

    public LocaleRenderMode RenderMode { get; }

    public string? ReviewStatus { get; }

    public string? ReviewedBy { get; }

    /// <summary>문구 개정 버전. AWaRe source_version과는 별개로 관리한다.</summary>
    public string? ContentVersion { get; }

    /// <summary>검수 완료 여부. false면 현지어를 한 글자도 인쇄하지 않는다.</summary>
    public bool IsApproved =>
        string.Equals(ReviewStatus, "approved", StringComparison.OrdinalIgnoreCase);

    public CounsellingLocale(
        string localeCode,
        string? languageName,
        string? script,
        LocaleRenderMode renderMode,
        string? reviewStatus,
        string? reviewedBy,
        string? contentVersion,
        IReadOnlyDictionary<string, string> strings)
    {
        LocaleCode = localeCode;
        LanguageName = languageName;
        Script = script;
        RenderMode = renderMode;
        ReviewStatus = reviewStatus;
        ReviewedBy = reviewedBy;
        ContentVersion = contentVersion;
        _strings = strings;
    }

    /// <summary>
    /// 현지어를 쓰지 않는 상태. 로케일 미설정, 파일 없음, 파일 손상, 미검수 —
    /// 어떤 이유든 결과는 같다: 영어 단독 출력.
    /// </summary>
    public static CounsellingLocale EnglishOnly { get; } = new(
        localeCode: string.Empty,
        languageName: null,
        script: null,
        renderMode: LocaleRenderMode.Text,
        reviewStatus: null,
        reviewedBy: null,
        contentVersion: null,
        strings: new Dictionary<string, string>());

    /// <summary>
    /// 화면 라벨용. 검수 여부로 막지 않는다.
    ///
    /// 인쇄물과 위험이 다르기 때문이다. 복약 문구는 환자가 그대로 따라 하는 글이라
    /// 틀리면 되돌릴 방법이 없지만, 버튼에 적힌 낱말이 어색한 것은 계산대에서 바로
    /// 눈에 띄고 고치면 그만이다. 대신 화면이 미검수 번역을 붉은 글씨로 보여 준다 —
    /// 검수를 마치고 approved로 바꾸면 저절로 보통 글씨가 된다.
    /// </summary>
    public string? GetInterfaceString(string key)
    {
        if (!_strings.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value;
    }

    /// <summary>
    /// 해당 키의 현지어 문구를 돌려준다. 없으면 null.
    ///
    /// null을 돌려주는 경우가 곧 "그 줄은 영어만 인쇄한다"는 뜻이다.
    /// 키 문자열이나 빈 칸이 용지에 찍히는 일은 없어야 하므로,
    /// 값이 공백뿐인 경우도 없는 것으로 취급한다.
    /// </summary>
    public string? GetString(string key)
    {
        if (!IsApproved)
        {
            return null;
        }

        if (!_strings.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value;
    }
}
