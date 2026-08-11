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
    public required IReadOnlyList<AntibioticSalesRow> Antibiotics { get; init; }

    public string AmountChange => PeriodChange.Format(Current.Amount, Previous.Amount);
    public string TransactionChange => PeriodChange.Format(Current.TransactionCount, Previous.TransactionCount);
    public string ItemChange => PeriodChange.Format(Current.ItemCount, Previous.ItemCount);

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
