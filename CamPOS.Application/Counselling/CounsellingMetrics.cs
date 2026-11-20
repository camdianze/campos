namespace CamPOS.Application.Counselling;

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

    /// <summary>
    /// 안내가 나가지 않은 건수 전체. 아래 두 값을 합친 것이지만 <b>꼭 같지는 않다</b> —
    /// 이유를 적지 않은 채 기록된 행이 있을 수 있어서다(skip_reason이 NULL).
    /// 그래서 이 값을 둘의 합으로 계산하지 않고 따로 센다.
    /// </summary>
    public int SkippedCount { get; init; }

    /// <summary>
    /// 약사가 Skip을 눌렀다. 판단이고 고장이 아니므로 오류로 읽히면 안 된다.
    /// </summary>
    public int PharmacistSkippedCount { get; init; }

    /// <summary>
    /// 인쇄가 실패했다 — 프린터를 봐야 한다는 뜻이다.
    ///
    /// 약사가 건너뛴 것과 한 칸에 담아 두면 "안 준 것"과 "줄 수 없었던 것"을
    /// 가를 수 없고, 출력률이 낮은 이유가 사람인지 기계인지 모르게 된다.
    /// 전자는 설명해서 풀 일이고 후자는 고쳐야 할 일이라 할 일이 전혀 다르다.
    /// </summary>
    public int PrintFailedCount { get; init; }

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
