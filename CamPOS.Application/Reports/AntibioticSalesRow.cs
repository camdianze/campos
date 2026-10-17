using System.Globalization;

namespace PharmaPOS.Application.Reports;

/// <summary>
/// 항생제 성분·용량별 판매 한 줄.
///
/// 판별 근거는 복약안내 로그(Counselling_Log)다. 판매 시점에 실제로 적용된 AWaRe 분류를
/// 그대로 쓰기 때문에, 목록에 보이는 그룹과 그때 인쇄된 안내지의 분류가 어긋나지 않는다.
/// 성분명 정규화 규칙(염 형태 제거, 표기 변형)이 C#에만 있어 SQL로는 같은 매칭을
/// 재현할 수 없다는 현실적인 이유도 있다.
/// </summary>
public class AntibioticSalesRow
{
    /// <summary>성분명. 상품에 성분명이 없으면 상품명으로 대신한다.</summary>
    public required string Ingredient { get; init; }

    /// <summary>용량. 상품에 없으면 빈 문자열.</summary>
    public required string Strength { get; init; }

    /// <summary>
    /// ACCESS / WATCH / RESERVE / NOT_RECOMMENDED.
    /// UNMATCHED(판정 실패)는 이 표에 오지 않는다 — 항생제로 확인된 것이 아니기 때문이다.
    /// </summary>
    public required string AwareGroup { get; init; }

    /// <summary>낱개 기준 판매 수량.</summary>
    public int Quantity { get; init; }
    public decimal Amount { get; init; }

    /// <summary>이 성분이 팔린 판매 라인 수. 복약안내 대상이 된 횟수와 같다.</summary>
    public int SaleCount { get; init; }

    /// <summary>실제로 복약안내가 출력된 횟수.</summary>
    public int CounsellingPrinted { get; init; }

    public int PreviousQuantity { get; init; }
    public decimal PreviousAmount { get; init; }
    public int PreviousSaleCount { get; init; }
    public int PreviousCounsellingPrinted { get; init; }

    public string QuantityChange => PeriodChange.Format(Quantity, PreviousQuantity);
    public string AmountChange => PeriodChange.Format(Amount, PreviousAmount);
    public string CounsellingChange => PeriodChange.Format(CounsellingPrinted, PreviousCounsellingPrinted);

    public ChangeDirection QuantityDirection => PeriodChange.DirectionOf(Quantity, PreviousQuantity);
    public ChangeDirection AmountDirection => PeriodChange.DirectionOf(Amount, PreviousAmount);

    /// <summary>
    /// 같은 기간 항생제 판매 수량 전체. 한 줄만으로는 알 수 없는 값이라
    /// ViewModel이 채운다 (ProductSalesRow.TotalAmountInPeriod와 같은 자리·같은 이유).
    /// </summary>
    public int TotalQuantityInPeriod { get; set; }

    /// <summary>
    /// 이 성분이 기간 내 항생제 판매에서 차지하는 비중.
    ///
    /// 분모는 이 표에 실린 줄들의 수량 합이라 표의 비중을 다 더하면 100%가 된다.
    /// 수량 기준인 이유는 ACCESS 비중(ReportData.AccessSharePercent)이 수량 기준이기
    /// 때문이다. 한 표 안에서 비중의 분모가 둘이면 두 값을 나란히 읽을 수 없다.
    /// </summary>
    public string QuantityShare => PeriodChange.FormatShare(Quantity, TotalQuantityInPeriod);

    /// <summary>"6 / 8" — 판매 8건 중 6건에 안내가 나갔다는 뜻.</summary>
    public string CounsellingDisplay => $"{CounsellingPrinted} / {SaleCount}";

    /// <summary>
    /// 복약안내 출력률. 화면에서는 요약 카드가 기간 전체의 출력률을 보여주므로
    /// 표에는 두지 않고, 성분별로 되짚어 봐야 하는 내보내기 파일에만 남긴다.
    /// 판매가 없으면 계산할 수 없다.
    /// </summary>
    public decimal? PrintedPercent =>
        SaleCount == 0 ? null : (decimal)CounsellingPrinted / SaleCount * 100m;

    public string PrintedPercentDisplay =>
        PrintedPercent is null
            ? "—"
            : PrintedPercent.Value.ToString("0", CultureInfo.InvariantCulture) + "%";
}
