using PharmaPOS.Application.Reports;

namespace PharmaPOS.Application.Repositories;

/// <summary>
/// 리포트 집계 조회를 담당하는 인터페이스.
///
/// 세 메서드 모두 현재 기간과 직전 기간을 <b>한 번의 조회로 함께</b> 가져온다.
/// 같은 쿼리를 기간만 바꿔 두 번 던지면 그 사이에 판매가 들어왔을 때
/// 비교 대상이 서로 다른 시점의 데이터가 되고, 표에 적힌 증감이 실제와 어긋난다.
/// </summary>
public interface IReportRepository
{
    /// <summary>기간 전체의 매출·수량·거래 건수.</summary>
    Task<(SalesTotals Current, SalesTotals Previous)> GetSalesTotalsAsync(
        string facilityId, ReportRange range);

    /// <summary>상품별 판매 집계. 두 기간 중 한쪽에라도 판매가 있으면 포함된다.</summary>
    Task<IReadOnlyList<ProductSalesRow>> GetProductSalesAsync(string facilityId, ReportRange range);

    /// <summary>
    /// 항생제 성분·용량별 판매와 복약안내 출력 횟수.
    /// 복약안내 로그에 남은 판매만 집계 대상이다.
    /// </summary>
    /// <summary>
    /// 항생제 성분별 판매. <b>AWaRe 그룹으로 판정된 것만</b> 돌려준다 —
    /// 매칭에 실패한(UNMATCHED) 로그는 항생제가 아니므로 제외한다.
    /// ReportData의 ACCESS 비중·항생제 판매 건수가 이 결과를 그대로 합산하므로,
    /// 여기에 UNMATCHED를 섞으면 지표가 항생제와 무관한 수량에 희석된다.
    /// </summary>
    Task<IReadOnlyList<AntibioticSalesRow>> GetAntibioticSalesAsync(string facilityId, ReportRange range);
}
