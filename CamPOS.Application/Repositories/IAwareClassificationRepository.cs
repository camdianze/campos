using PharmaPOS.Domain.Entities;

namespace PharmaPOS.Application.Repositories;

/// <summary>
/// WHO AWaRe 참조 데이터 접근. 앱은 이 데이터를 조회만 하고,
/// 쓰기는 시드 파일 적재(ReplaceAllAsync) 한 경로뿐이다.
/// </summary>
public interface IAwareClassificationRepository
{
    /// <summary>
    /// 기존 데이터를 전부 지우고 새로 적재한다. 하나의 트랜잭션으로 처리하므로
    /// 중간에 실패하면 이전 데이터가 그대로 남는다 (빈 상태로 방치되지 않는다).
    /// </summary>
    Task ReplaceAllAsync(IReadOnlyList<AwareClassification> classifications);

    /// <summary>정규화된 ATC 코드로 조회한다. 없으면 null.</summary>
    Task<AwareClassification?> FindByAtcCodeAsync(string normalizedAtcCode);

    /// <summary>정규화된 성분명으로 조회한다. 없으면 null.</summary>
    Task<AwareClassification?> FindByNormalizedNameAsync(string normalizedName);

    Task<int> CountAsync();
}
