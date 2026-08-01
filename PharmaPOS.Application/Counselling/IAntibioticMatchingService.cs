namespace PharmaPOS.Application.Counselling;

/// <summary>
/// 판매 상품이 복약안내 대상 항생제인지 판별한다.
/// </summary>
public interface IAntibioticMatchingService
{
    /// <summary>
    /// ATC 코드를 먼저 보고, 없거나 못 찾으면 성분명으로 조회한다.
    /// 예외를 던지지 않는다 — 판별 실패는 Unmatched로 돌려주고 판매는 그대로 진행시킨다.
    /// </summary>
    Task<AntibioticMatch> MatchAsync(string? atcCode, string? genericName);
}
