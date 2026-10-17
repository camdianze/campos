using PharmaPOS.Application.Reports;
using PharmaPOS.Domain.Enums;

namespace PharmaPOS.Tests.Reports;

/// <summary>
/// 복약안내 출력률. 리포트 요약 카드가 이 값 하나로 "안내가 실제로 전달되고 있는가"를
/// 보여주므로, 분모와 분자가 어긋나면 기능이 도는지 여부를 잘못 읽게 된다.
/// </summary>
public class CounsellingPrintRateTests
{
    private static AntibioticSalesRow Row(int saleCount, int printed) => new()
    {
        Ingredient = "Amoxicillin",
        Strength = "500 mg",
        AwareGroup = AwareGroupCodes.Access,
        Quantity = 10,
        SaleCount = saleCount,
        CounsellingPrinted = printed
    };

    private static ReportData Data(params AntibioticSalesRow[] rows) => new()
    {
        Range = ReportRange.Create(new DateTime(2026, 8, 1), new DateTime(2026, 8, 31)),
        Current = SalesTotals.Empty,
        Previous = SalesTotals.Empty,
        Products = Array.Empty<ProductSalesRow>(),
        Antibiotics = rows,
        AntibioticTrend = Array.Empty<AntibioticTrendPoint>(),
        SalesTrend = Array.Empty<SalesTrendPoint>()
    };

    [Fact]
    public void PrintRate_IsPrintedSheetsOverAntibioticSaleLines()
    {
        var data = Data(Row(saleCount: 4, printed: 3), Row(saleCount: 2, printed: 2));

        Assert.Equal(6, data.AntibioticSaleCount);
        Assert.Equal(5, data.CounsellingPrintedCount);
        Assert.Equal(5m / 6m * 100m, data.CounsellingPrintRatePercent);
    }

    [Fact]
    public void PrintRate_IsFullWhenEverySaleGotASheet()
    {
        Assert.Equal(100m, Data(Row(saleCount: 3, printed: 3)).CounsellingPrintRatePercent);
    }

    /// <summary>
    /// 약사가 매번 Skip을 눌렀거나 프린터가 죽어 있으면 판매는 기록되지만 출력은 0이다.
    /// 그 상태가 0%로 드러나야 한다 — 아니면 안내가 나가고 있다고 착각한다.
    /// </summary>
    [Fact]
    public void PrintRate_IsZeroWhenNothingWasPrinted()
    {
        Assert.Equal(0m, Data(Row(saleCount: 3, printed: 0)).CounsellingPrintRatePercent);
    }

    /// <summary>
    /// 항생제를 팔지 않았으면 계산할 수 없다. 0%로 내보내면 "안내를 하나도 안 줬다"로
    /// 읽히는데, 실제로는 줄 일이 없었던 것이다.
    /// </summary>
    [Fact]
    public void PrintRate_HasNoValueWhenNoAntibioticWasSold()
    {
        Assert.Null(Data().CounsellingPrintRatePercent);
    }
}
