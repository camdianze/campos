using PharmaPOS.Domain.Enums;

namespace PharmaPOS.Application.Reports;

/// <summary>
/// 리포트 한 판의 집계 결과.
/// </summary>
public class ReportData
{
    public required ReportRange Range { get; init; }
    public required SalesTotals Current { get; init; }
    public required SalesTotals Previous { get; init; }
    public required IReadOnlyList<ProductSalesRow> Products { get; init; }

    /// <summary>
    /// 항생제 성분별 판매. <b>AWaRe 그룹으로 판정된 줄만</b> 들어 있다(UNMATCHED 제외).
    /// 아래 세 지표가 이 목록을 그대로 합산하므로 그 전제가 깨지면 값이 조용히 틀어진다.
    /// </summary>
    public required IReadOnlyList<AntibioticSalesRow> Antibiotics { get; init; }

    /// <summary>
    /// 최근 12개월 항생제 판매 추이. 고른 기간이 아니라 <b>기간 종료월까지의 1년</b>이다 —
    /// 한 달치 표만 보면 그 달이 평소보다 많은지 적은지 알 수 없다.
    /// 판매가 없던 달도 0으로 들어 있어 12칸이 항상 채워진다.
    /// </summary>
    public required IReadOnlyList<AntibioticTrendPoint> AntibioticTrend { get; init; }

    /// <summary>
    /// 최근 12개월 순매출 추이. AntibioticTrend와 같은 창이라 두 그래프의
    /// 가로축 눈금이 정확히 겹친다.
    /// </summary>
    public required IReadOnlyList<SalesTrendPoint> SalesTrend { get; init; }

    public string AmountChange => PeriodChange.Format(Current.Amount, Previous.Amount);
    public string TransactionChange => PeriodChange.Format(Current.TransactionCount, Previous.TransactionCount);
    public string ItemChange => PeriodChange.Format(Current.ItemCount, Previous.ItemCount);

    // 백분율 옆에 붙는 증감 절대량. 12.3%가 4,000인지 40인지는 백분율만으로 알 수 없다.
    // 직전 기간이 0이거나 변화가 없으면 빈 문자열이라 카드에 아무것도 붙지 않는다.
    public string AmountDelta => PeriodChange.FormatDelta(Current.Amount, Previous.Amount);
    public string TransactionDelta => PeriodChange.FormatDelta(Current.TransactionCount, Previous.TransactionCount);
    public string ItemDelta => PeriodChange.FormatDelta(Current.ItemCount, Previous.ItemCount);

    // 화살표 색을 정하는 데 쓴다. 위 문자열을 되짚어 읽는 것보다 방향을 따로 내주는 편이 안전하다.
    public ChangeDirection AmountDirection => PeriodChange.DirectionOf(Current.Amount, Previous.Amount);
    public ChangeDirection TransactionDirection => PeriodChange.DirectionOf(Current.TransactionCount, Previous.TransactionCount);
    public ChangeDirection ItemDirection => PeriodChange.DirectionOf(Current.ItemCount, Previous.ItemCount);

    /// <summary>항생제 판매 건수 합계.</summary>
    public int AntibioticSaleCount => Antibiotics.Sum(a => a.SaleCount);

    /// <summary>복약안내 출력 횟수 합계.</summary>
    public int CounsellingPrintedCount => Antibiotics.Sum(a => a.CounsellingPrinted);

    /// <summary>
    /// 복약안내 출력률. 항생제를 판 줄 중 몇 %에 안내가 나갔는지다.
    ///
    /// 세는 단위가 "장수"가 아니라 "안내가 전달된 판매"인 점에 주의. 한 상품이 두 줄로
    /// 쪼개져 담겨도 용지는 한 장 나가지만, 그 손님은 안내를 받았으므로 두 줄 모두
    /// 출력된 것으로 기록된다(CounsellingService.PrintAsync). 그래서 이 비율의 분자와
    /// 분모가 같은 단위로 맞는다.
    ///
    /// 항생제를 팔지 않았으면 계산할 수 없다 — 0%가 아니라 "해당 없음"이다.
    /// </summary>
    public decimal? CounsellingPrintRatePercent =>
        AntibioticSaleCount == 0
            ? null
            : (decimal)CounsellingPrintedCount / AntibioticSaleCount * 100m;

    /// <summary>기간 내 항생제 판매 수량 전체. 등급별 비중의 분모다.</summary>
    public int AntibioticQuantity => Antibiotics.Sum(a => a.Quantity);

    /// <summary>
    /// ACCESS 비중. WHO는 항생제 소비의 70% 이상을 ACCESS 그룹으로 권고하므로,
    /// 이 값 하나가 스튜어드십 상태를 가장 잘 요약한다. 수량 기준으로 계산한다.
    /// </summary>
    public decimal? AccessSharePercent =>
        GroupShares.FirstOrDefault(g => g.Group == AwareGroupCodes.Access)?.SharePercent;

    /// <summary>
    /// 네 등급의 비중을 심각도 순으로 늘어놓는다. 판매가 없는 등급도 0%로 남긴다 —
    /// 목록에서 빠지면 "0이었다"와 "그런 등급이 없다"를 구분할 수 없다.
    /// </summary>
    public IReadOnlyList<AwareGroupShare> GroupShares
    {
        get
        {
            var total = AntibioticQuantity;

            return new[]
                {
                    AwareGroupCodes.Access,
                    AwareGroupCodes.Watch,
                    AwareGroupCodes.Reserve,
                    AwareGroupCodes.NotRecommended
                }
                .Select(group => new AwareGroupShare
                {
                    Group = group,
                    Quantity = Antibiotics.Where(a => a.AwareGroup == group).Sum(a => a.Quantity),
                    TotalQuantity = total
                })
                .ToList();
        }
    }
}

/// <summary>
/// 리포트 조회 결과. 다른 서비스와 같이 예외 대신 결과 객체로 실패를 알린다.
/// </summary>
public class ReportQueryResult
{
    private ReportQueryResult(bool isSuccess, string? message, ReportData? data)
    {
        IsSuccess = isSuccess;
        Message = message;
        Data = data;
    }

    public bool IsSuccess { get; }
    public string? Message { get; }
    public ReportData? Data { get; }

    public static ReportQueryResult Success(ReportData data) => new(true, null, data);
    public static ReportQueryResult Failure(string message) => new(false, message, null);
}
