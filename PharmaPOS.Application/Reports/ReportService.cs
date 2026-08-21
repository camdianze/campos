using PharmaPOS.Application.Repositories;

namespace PharmaPOS.Application.Reports;

/// <summary>
/// IReportService의 구현체. 집계 자체는 SQL이 하고, 여기서는 기간 검증과 순위 부여만 맡는다.
/// </summary>
public class ReportService : IReportService
{
    /// <summary>추이 그래프가 보여줄 달 수. 계절성을 보려면 최소 1년이 필요하다.</summary>
    public const int TrendMonths = 12;

    private readonly IReportRepository _reportRepository;

    public ReportService(IReportRepository reportRepository)
    {
        _reportRepository = reportRepository;
    }

    public async Task<ReportQueryResult> GetReportAsync(string facilityId, DateTime? from, DateTime? to)
    {
        if (from is not null && to is not null && from.Value.Date > to.Value.Date)
        {
            return ReportQueryResult.Failure("Start date cannot be later than end date.");
        }

        // 한쪽만 비어 있는 것도 받아 준다. 시작일만 고르면 오늘까지, 종료일만 고르면 그 달 1일부터.
        var range = (from, to) switch
        {
            (null, null) => ReportRange.CurrentMonthToDate(DateTime.Today),
            ({ } start, null) => ReportRange.Create(start, DateTime.Today),
            (null, { } end) => ReportRange.Create(new DateTime(end.Year, end.Month, 1), end),
            ({ } start, { } end) => ReportRange.Create(start, end)
        };

        try
        {
            var (current, previous) = await _reportRepository.GetSalesTotalsAsync(facilityId, range);
            var products = await _reportRepository.GetProductSalesAsync(facilityId, range);
            var antibiotics = await _reportRepository.GetAntibioticSalesAsync(facilityId, range);

            // 추이는 고른 기간이 아니라 그 기간이 끝나는 달까지의 1년이다.
            // 한 달치 표만으로는 그 달이 평소보다 많은지 적은지 판단할 수 없다.
            var trend = await _reportRepository.GetAntibioticTrendAsync(
                facilityId, range.To, TrendMonths);

            var salesTrend = await _reportRepository.GetSalesTrendAsync(
                facilityId, range.To, TrendMonths);

            // 순위(Rank)는 여기서 매기지 않는다. 화면에서 매출/판매수 중 무엇으로 정렬할지
            // 고를 수 있어서, 순위는 그 정렬을 아는 쪽이 매겨야 1위가 맨 위에 온다.
            // 조회 결과 자체는 매출 내림차순이다.
            return ReportQueryResult.Success(new ReportData
            {
                Range = range,
                Current = current,
                Previous = previous,
                Products = products,
                Antibiotics = antibiotics,
                AntibioticTrend = trend,
                SalesTrend = salesTrend
            });
        }
        catch (Exception)
        {
            return ReportQueryResult.Failure("The report could not be loaded.");
        }
    }
}
