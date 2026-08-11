namespace PharmaPOS.Application.Reports;

/// <summary>
/// 관리자 리포트(기간별 매출, 상품별 순위, 항생제 성분별 판매) 조회를 담당한다.
/// </summary>
public interface IReportService
{
    /// <summary>
    /// 지정한 기간의 리포트를 만든다. 날짜를 비워 두면 이번 달 1일부터 오늘까지를 쓴다.
    /// 비교 대상 기간은 ReportRange가 정한다 (달 전체를 고르면 전월, 그 밖에는 같은 길이의 직전 구간).
    /// </summary>
    Task<ReportQueryResult> GetReportAsync(string facilityId, DateTime? from, DateTime? to);
}
