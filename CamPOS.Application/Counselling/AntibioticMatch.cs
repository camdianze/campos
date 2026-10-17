using PharmaPOS.Domain.Entities;

namespace PharmaPOS.Application.Counselling;

/// <summary>판매 상품을 AWaRe 참조 데이터와 맞춰본 결과.</summary>
public enum AntibioticMatchOutcome
{
    /// <summary>전신 항생제로 확인됨. 복약안내 대상이다.</summary>
    Matched,

    /// <summary>항생제로는 확인됐지만 국소 제제라 안내 대상에서 제외한다.</summary>
    ExcludedTopical,

    /// <summary>참조 데이터에서 찾지 못함. 항생제가 아니거나 시드에 없는 항목이다.</summary>
    Unmatched
}

/// <summary>
/// 매칭 결과. 어떤 경우에도 판매를 막지 않는다 —
/// Unmatched는 "안내를 띄우지 않는다"는 뜻일 뿐 오류가 아니다.
/// </summary>
public class AntibioticMatch
{
    public AntibioticMatchOutcome Outcome { get; }

    /// <summary>Matched / ExcludedTopical일 때만 값이 있다.</summary>
    public AwareClassification? Classification { get; }

    /// <summary>매칭에 실제로 쓰인 값 (ATC 코드 또는 정규화된 성분명). unmatched 로그 보강용.</summary>
    public string? MatchedOn { get; }

    private AntibioticMatch(
        AntibioticMatchOutcome outcome, AwareClassification? classification, string? matchedOn)
    {
        Outcome = outcome;
        Classification = classification;
        MatchedOn = matchedOn;
    }

    public bool RequiresCounselling => Outcome == AntibioticMatchOutcome.Matched;

    public static AntibioticMatch Matched(AwareClassification classification, string matchedOn)
        => new(AntibioticMatchOutcome.Matched, classification, matchedOn);

    public static AntibioticMatch ExcludedTopical(AwareClassification classification, string matchedOn)
        => new(AntibioticMatchOutcome.ExcludedTopical, classification, matchedOn);

    public static AntibioticMatch Unmatched(string? attemptedValue = null)
        => new(AntibioticMatchOutcome.Unmatched, null, attemptedValue);
}
