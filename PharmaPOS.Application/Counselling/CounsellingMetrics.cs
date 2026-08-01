namespace PharmaPOS.Application.Counselling;

/// <summary>
/// 항생제 스튜어드십 지표. 기간을 잘라 집계한다.
/// </summary>
public class CounsellingMetrics
{
    /// <summary>기간 내 전체 판매 건수 (Stock_Transaction의 StockOut 행 수).</summary>
    public int TotalSaleLines { get; init; }

    /// <summary>그중 항생제로 매칭된 건수.</summary>
    public int AntibioticSaleLines { get; init; }

    public int AccessCount { get; init; }
    public int WatchCount { get; init; }
    public int ReserveCount { get; init; }
    public int NotRecommendedCount { get; init; }

    /// <summary>참조 데이터에서 찾지 못한 건수. 시드 데이터 보강 대상이다.</summary>
    public int UnmatchedCount { get; init; }

    public int PrintedCount { get; init; }
    public int SkippedCount { get; init; }

    /// <summary>항생제 판매 비중.</summary>
    public double AntibioticShare =>
        TotalSaleLines == 0 ? 0 : (double)AntibioticSaleLines / TotalSaleLines;

    /// <summary>
    /// ACCESS 그룹 비중.
    /// 2024년 UN 회원국이 2030년까지 인체 항생제 사용의 70%를 ACCESS로
    /// 채우기로 합의한 국제 지표에 대응한다.
    /// </summary>
    public double AccessShare =>
        AntibioticSaleLines == 0 ? 0 : (double)AccessCount / AntibioticSaleLines;

    /// <summary>복약안내 출력률.</summary>
    public double PrintRate =>
        AntibioticSaleLines == 0 ? 0 : (double)PrintedCount / AntibioticSaleLines;
}
