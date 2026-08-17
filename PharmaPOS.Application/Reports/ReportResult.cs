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
    /// ACCESS 비중. WHO는 항생제 소비의 70% 이상을 ACCESS 그룹으로 권고하므로,
    /// 이 값 하나가 스튜어드십 상태를 가장 잘 요약한다. 수량 기준으로 계산한다.
    /// </summary>
    public decimal? AccessSharePercent
    {
        get
        {
            var total = Antibiotics.Sum(a => a.Quantity);

            if (total == 0)
            {
                return null;
            }

            var access = Antibiotics
                .Where(a => a.AwareGroup == AwareGroupCodes.Access)
                .Sum(a => a.Quantity);

            return (decimal)access / total * 100m;
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
