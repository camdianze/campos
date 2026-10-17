namespace PharmaPOS.Domain.Enums;

/// <summary>
/// WHO AWaRe 분류 그룹.
///
/// NOT_RECOMMENDED(고정용량복합제·베타락탐/베타락타마제 조합 등)를 반드시 포함한다.
/// 3분류만 두면 복합제가 조회에서 통째로 누락되어,
/// 정작 복약안내가 가장 필요한 상품에서 기능이 동작하지 않는다.
/// </summary>
public enum AwareGroup
{
    Access,
    Watch,
    Reserve,
    NotRecommended
}
