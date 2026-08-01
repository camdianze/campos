namespace PharmaPOS.Domain.Enums;

/// <summary>
/// AwareGroup의 표준 코드 문자열 변환.
///
/// 이 저장소의 다른 enum들은 .ToString()으로 저장하지만, AWaRe 그룹만은 예외로
/// 명시적 매핑을 쓴다. 이유는 두 가지다.
///   1. 저장/집계 값이 국제 지표(ACCESS 비중 70% 목표)에 그대로 쓰이므로,
///      C# enum 이름을 리팩터링해도 DB 값과 CSV 값이 흔들리면 안 된다.
///   2. NotRecommended는 .ToString()이 "NotRecommended"라 표준 표기
///      "NOT_RECOMMENDED"와 어긋난다.
/// 이 코드 문자열은 어떤 언어로도 번역하지 않는다.
/// </summary>
public static class AwareGroupCodes
{
    public const string Access = "ACCESS";
    public const string Watch = "WATCH";
    public const string Reserve = "RESERVE";
    public const string NotRecommended = "NOT_RECOMMENDED";

    /// <summary>매칭에 실패한 상품을 로그에 남길 때 쓰는 값 (분류 그룹이 아니다).</summary>
    public const string Unmatched = "UNMATCHED";

    public static string ToCode(AwareGroup group) => group switch
    {
        AwareGroup.Access => Access,
        AwareGroup.Watch => Watch,
        AwareGroup.Reserve => Reserve,
        AwareGroup.NotRecommended => NotRecommended,
        _ => throw new ArgumentOutOfRangeException(nameof(group), group, null)
    };

    /// <summary>
    /// 시드 파일/DB의 문자열을 그룹으로 되돌린다.
    /// 시드 파일이 손으로 관리되는 점을 감안해 대소문자와 구분자(_ - 공백)는 관대하게 받는다.
    /// </summary>
    public static bool TryParse(string? value, out AwareGroup group)
    {
        group = default;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value.Trim().ToUpperInvariant().Replace(' ', '_').Replace('-', '_');

        switch (normalized)
        {
            case Access:
                group = AwareGroup.Access;
                return true;
            case Watch:
                group = AwareGroup.Watch;
                return true;
            case Reserve:
                group = AwareGroup.Reserve;
                return true;
            case NotRecommended:
            case "NOTRECOMMENDED":
                group = AwareGroup.NotRecommended;
                return true;
            default:
                return false;
        }
    }
}
