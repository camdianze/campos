using System.Text;

namespace PharmaPOS.Application.Counselling;

/// <summary>
/// 성분명(generic_name)을 AWaRe 시드 데이터와 맞춰보기 위한 정규화 함수.
///
/// 핵심 원칙: 이 함수는 상품 쪽 이름과 시드 쪽 이름에 "똑같이" 적용된다.
/// 그래서 규칙 하나를 바꾸면 양쪽 표현이 함께 움직이고, 매칭 결과는 어긋나지 않는다.
/// (다만 시드는 적재 시점에 정규화해 저장하므로, 규칙을 바꾸면 시드를 다시 적재해야 한다.
///  그래서 로더는 정규화 규칙 버전을 함께 기록한다.)
/// </summary>
public static class AntibioticNameNormalizer
{
    /// <summary>
    /// 정규화 규칙 버전. 이 값이 바뀌면 저장된 normalized_name이 낡은 것이므로
    /// 시드를 강제로 다시 적재한다. 규칙을 손볼 때 반드시 같이 올릴 것.
    /// </summary>
    public const string RuleVersion = "1";

    /// <summary>
    /// 염·수화물 형태를 나타내는 토큰. 성분 자체를 가리키지 않으므로 제거한다.
    /// 예: "Azithromycin dihydrate" → "azithromycin"
    /// </summary>
    private static readonly HashSet<string> SaltTokens = new(StringComparer.Ordinal)
    {
        // 수화물
        "hydrate", "anhydrous", "monohydrate", "dihydrate", "trihydrate",
        "tetrahydrate", "pentahydrate", "hemihydrate", "sesquihydrate",
        // 무기염
        "sodium", "monosodium", "disodium", "potassium", "dipotassium",
        "calcium", "magnesium", "zinc", "aluminium", "aluminum",
        "hydrochloride", "hcl", "chloride", "bromide", "iodide",
        "sulfate", "sulphate", "phosphate", "nitrate", "carbonate",
        // 유기염
        "acetate", "benzoate", "besylate", "besilate", "citrate", "edetate",
        "embonate", "esylate", "estolate", "ethylsuccinate", "fumarate",
        "gluconate", "glutamate", "hyclate", "lactate", "lactobionate",
        "maleate", "malate", "mandelate", "mesylate", "mesilate", "napsylate",
        "oxalate", "palmitate", "pamoate", "propionate", "salicylate",
        "stearate", "succinate", "tartrate", "tosylate", "valerate", "xinafoate"
    };

    /// <summary>용량 토큰 판별용 단위. "500mg" 같은 토큰을 통째로 버린다.</summary>
    private static readonly string[] DoseUnits =
    {
        "mg", "mcg", "ug", "g", "kg", "ml", "l", "iu", "mgs", "%"
    };

    /// <summary>
    /// 성분명을 비교 가능한 형태로 바꾼다. 입력이 비어 있으면 빈 문자열을 돌려준다.
    /// </summary>
    public static string Normalize(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return string.Empty;
        }

        var lowered = name.ToLowerInvariant();

        // 1단계: 토큰 단위 정리 — 염 형태와 용량 표기를 버린다.
        //         구분자를 지우기 "전에" 해야 단어 경계를 알 수 있다.
        var kept = new List<string>();

        foreach (var rawToken in lowered.Split(
                     new[] { ' ', '\t', '-', '/', '+', ',', '(', ')', '.', ';', ':' },
                     StringSplitOptions.RemoveEmptyEntries))
        {
            var token = TrimNonAlphanumeric(rawToken);

            if (token.Length == 0 || SaltTokens.Contains(token) || IsDoseToken(token))
            {
                continue;
            }

            kept.Add(token);
        }

        // 염 토큰만으로 이루어진 이름은 있을 수 없다. 전부 걸러졌다면
        // 원본이 이상한 것이므로 걸러내기 전 상태로 되돌려 판단을 매칭 쪽에 맡긴다.
        var joined = kept.Count > 0
            ? string.Concat(kept)
            : KeepAlphanumeric(lowered);

        // 2단계: 남은 비영숫자 제거 (공백/하이픈은 이미 위에서 분리자로 처리됨).
        joined = KeepAlphanumeric(joined);

        // 3단계: 철자 변형 통합.
        return UnifySpellingVariants(joined);
    }

    /// <summary>
    /// ATC 코드 표기 흔들림(공백, 점, 대소문자)을 없앤다.
    /// </summary>
    public static string NormalizeAtcCode(string? atcCode)
    {
        if (string.IsNullOrWhiteSpace(atcCode))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(atcCode.Length);

        foreach (var c in atcCode)
        {
            if (char.IsLetterOrDigit(c))
            {
                builder.Append(char.ToUpperInvariant(c));
            }
        }

        return builder.ToString();
    }

    /// <summary>
    /// cef- / ceph- 는 같은 계열의 표기 차이일 뿐이므로 cef- 로 통일한다.
    /// (Cephalexin / Cefalexin, Cephradine / Cefradine …)
    /// sulph- / sulf- 도 같은 성격의 영/미 철자 차이라 함께 통일한다.
    /// 접두사에만 적용한다 — 단어 중간의 ph를 f로 바꾸면 관계없는 성분명이 망가진다.
    /// </summary>
    private static string UnifySpellingVariants(string value)
    {
        if (value.StartsWith("ceph", StringComparison.Ordinal))
        {
            value = "cef" + value[4..];
        }

        if (value.StartsWith("sulph", StringComparison.Ordinal))
        {
            value = "sulf" + value[5..];
        }

        return value;
    }

    /// <summary>"500mg", "5", "1.5g" 같은 용량 토큰인지 판단한다.</summary>
    private static bool IsDoseToken(string token)
    {
        if (!char.IsDigit(token[0]))
        {
            return false;
        }

        // 숫자로만 이루어진 토큰 (예: "500")
        if (token.All(char.IsDigit))
        {
            return true;
        }

        // 숫자 + 단위 (예: "500mg")
        var unitStart = 0;
        while (unitStart < token.Length && (char.IsDigit(token[unitStart]) || token[unitStart] == '.'))
        {
            unitStart++;
        }

        var unit = token[unitStart..];
        return DoseUnits.Contains(unit, StringComparer.Ordinal);
    }

    private static string TrimNonAlphanumeric(string token)
    {
        return KeepAlphanumeric(token);
    }

    private static string KeepAlphanumeric(string value)
    {
        var builder = new StringBuilder(value.Length);

        foreach (var c in value)
        {
            if (char.IsLetterOrDigit(c))
            {
                builder.Append(c);
            }
        }

        return builder.ToString();
    }
}
