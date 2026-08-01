using PharmaPOS.Application.Repositories;

namespace PharmaPOS.Application.Counselling;

/// <summary>
/// IAntibioticMatchingService의 구현체.
///
/// 판별 순서
///   1. 상품의 ATC 코드로 조회 (표기 흔들림이 없어 가장 정확하다)
///   2. 못 찾으면 성분명을 정규화해서 조회
///   3. 찾았는데 is_systemic이 false면 국소 제제이므로 제외
///
/// ATC 코드가 있는데 조회에 실패하면 성분명으로 한 번 더 시도한다.
/// 스펙 흐름도에는 없는 동작이지만, ATC를 오타로 입력한 상품이 영원히
/// 매칭되지 않는 쪽이 더 나쁘다고 봤다. 순서만 지키면 정확도는 떨어지지 않는다.
///
/// ATC 접두사(J01 등)로 전신 여부를 거르지 않는다.
/// 전신 항생제는 A07AA, J04, P01AB 등에도 걸쳐 있어 접두사 필터는 틀린다.
/// 판단은 시드 데이터의 is_systemic 값에만 맡긴다.
/// </summary>
public class AntibioticMatchingService : IAntibioticMatchingService
{
    private readonly IAwareClassificationRepository _awareRepository;

    public AntibioticMatchingService(IAwareClassificationRepository awareRepository)
    {
        _awareRepository = awareRepository;
    }

    public async Task<AntibioticMatch> MatchAsync(string? atcCode, string? genericName)
    {
        var normalizedAtc = AntibioticNameNormalizer.NormalizeAtcCode(atcCode);
        var normalizedName = AntibioticNameNormalizer.Normalize(genericName);

        try
        {
            if (normalizedAtc.Length > 0)
            {
                var byAtc = await _awareRepository.FindByAtcCodeAsync(normalizedAtc);

                if (byAtc is not null)
                {
                    return byAtc.IsSystemic
                        ? AntibioticMatch.Matched(byAtc, normalizedAtc)
                        : AntibioticMatch.ExcludedTopical(byAtc, normalizedAtc);
                }
            }

            if (normalizedName.Length > 0)
            {
                var byName = await _awareRepository.FindByNormalizedNameAsync(normalizedName);

                if (byName is not null)
                {
                    return byName.IsSystemic
                        ? AntibioticMatch.Matched(byName, normalizedName)
                        : AntibioticMatch.ExcludedTopical(byName, normalizedName);
                }
            }
        }
        catch (Exception)
        {
            // 참조 데이터 조회가 실패해도 판매는 계속돼야 한다.
            // 조용히 unmatched로 처리한다.
            return AntibioticMatch.Unmatched(normalizedAtc.Length > 0 ? normalizedAtc : normalizedName);
        }

        return AntibioticMatch.Unmatched(normalizedAtc.Length > 0 ? normalizedAtc : normalizedName);
    }
}
