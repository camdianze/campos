using PharmaPOS.Application.Reports;
using PharmaPOS.Domain.Enums;

namespace PharmaPOS.Tests.Reports;

/// <summary>
/// AMR 연구에 제출하는 항생제 파일.
///
/// 이 파일은 약국 밖으로 나간다. 여기 있는 검사들이 지키는 것은 형식이 아니라
/// <b>"금액이 새어 나가지 않는다"</b>는 약속이다. 약국이 매출을 넘기지 않아도
/// 의무 제출을 할 수 있어야, 제출을 꺼리지 않는다.
/// </summary>
public class AntibioticExportCsvTests
{
    /// <summary>파일 어디에도 나와서는 안 되는, 알아보기 쉬운 금액.</summary>
    private const decimal SecretRevenue = 987654.32m;

    private static AntibioticSalesRow Row(
        string ingredient, string group, int quantity, decimal amount) => new()
    {
        Ingredient = ingredient,
        Strength = "500 mg",
        AwareGroup = group,
        Quantity = quantity,
        Amount = amount,
        SaleCount = 2,
        CounsellingPrinted = 2,
        PreviousQuantity = 1
    };

    private static ReportData Data(params AntibioticSalesRow[] rows) => new()
    {
        Range = ReportRange.Create(new DateTime(2026, 8, 1), new DateTime(2026, 8, 31)),
        // 요약 금액도 함께 채워 둔다. 이 값들이 파일로 새어 나가는지도 확인해야 한다.
        Current = new SalesTotals { Amount = SecretRevenue, ItemCount = 530, TransactionCount = 8 },
        Previous = new SalesTotals { Amount = SecretRevenue, ItemCount = 1, TransactionCount = 1 },
        Products = new[]
        {
            new ProductSalesRow
            {
                ProductId = "p1",
                ProductName = "Accu-Chek Guide Glucose Meter Kit",
                Quantity = 1,
                Amount = SecretRevenue
            }
        },
        Antibiotics = rows,
        AntibioticTrend = new[]
        {
            new AntibioticTrendPoint { Month = new DateTime(2026, 8, 1), AccessQuantity = 200 }
        },
        SalesTrend = new[]
        {
            new SalesTrendPoint { Month = new DateTime(2026, 8, 1), Amount = SecretRevenue }
        }
    };

    private static string Build(params AntibioticSalesRow[] rows) =>
        AntibioticExportCsv.Build(Data(rows), "KH-PP-014");

    // ── 새어 나가면 안 되는 것 ────────────────────────────────────────────

    /// <summary>
    /// 성분별 매출, 기간 매출, 상품 매출, 매출 추이 — 금액이 담긴 곳은 넷이다.
    /// 그중 어느 것도 이 파일에 실려서는 안 된다.
    /// </summary>
    [Fact]
    public void Build_NeverCarriesAnyMoneyFigure()
    {
        var csv = Build(
            Row("Amoxicillin", AwareGroupCodes.Access, 200, SecretRevenue),
            Row("Norfloxacin", AwareGroupCodes.Watch, 1, SecretRevenue));

        Assert.DoesNotContain("987654", csv);
        Assert.DoesNotContain("987,654", csv);
    }

    /// <summary>
    /// 금액 열이 아예 없어야 한다. 값이 0인 채로 열만 남아 있으면
    /// 나중에 누가 "비어 있으니 채워 넣자"고 하게 된다.
    /// </summary>
    [Theory]
    [InlineData("Amount")]
    [InlineData("Revenue")]
    [InlineData("Price")]
    [InlineData("Cost")]
    public void Build_HasNoMonetaryColumn(string forbidden)
    {
        var csv = Build(Row("Amoxicillin", AwareGroupCodes.Access, 200, SecretRevenue));

        Assert.DoesNotContain(forbidden, csv, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>상품명은 판매 구성을 드러낸다. 연구가 보는 것은 성분이지 상품이 아니다.</summary>
    [Fact]
    public void Build_DoesNotNameProducts()
    {
        var csv = Build(Row("Amoxicillin", AwareGroupCodes.Access, 200, SecretRevenue));

        Assert.DoesNotContain("Accu-Chek", csv);
    }

    // ── 사이트 코드 ───────────────────────────────────────────────────────

    /// <summary>
    /// 파일에서 약국을 가리키는 값은 이 코드 하나뿐이다.
    /// 코드와 약국의 대응표는 연구기관만 가지므로, 파일만으로는 특정되지 않는다.
    /// </summary>
    [Fact]
    public void Build_CarriesTheSiteCode()
    {
        var csv = Build(Row("Amoxicillin", AwareGroupCodes.Access, 200, SecretRevenue));

        Assert.Contains("Site code,KH-PP-014", csv);
    }

    /// <summary>
    /// 코드가 없어도 파일은 나온다 — 등록 전이거나 약국이 자기 확인용으로 뽑는 경우가 있다.
    /// 다만 받는 쪽이 "출처가 빠졌다"를 파일만 보고 알 수 있어야 한다.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Build_MarksAnUnattributedFileExplicitly(string? siteCode)
    {
        var csv = AntibioticExportCsv.Build(
            Data(Row("Amoxicillin", AwareGroupCodes.Access, 200, SecretRevenue)), siteCode);

        Assert.Contains("Site code,(not set)", csv);
    }

    /// <summary>
    /// 붙여넣기로 들어온 코드에 줄바꿈이 섞이면 CSV 한 줄이 두 줄로 쪼개져
    /// 받는 쪽에서 파일이 통째로 어긋난다.
    /// </summary>
    [Fact]
    public void Build_KeepsAPastedSiteCodeOnOneLine()
    {
        const char cr = (char)13;
        const char lf = (char)10;

        var csv = AntibioticExportCsv.Build(
            Data(Row("Amoxicillin", AwareGroupCodes.Access, 200, SecretRevenue)),
            "KH-PP-014" + cr + lf + "  ");

        var siteCodeLines = csv
            .Split(lf)
            .Select(line => line.TrimEnd(cr))
            .Where(line => line.StartsWith("Site code,"))
            .ToList();

        Assert.Equal(new[] { "Site code,KH-PP-014" }, siteCodeLines);
    }

    // ── 형식 ──────────────────────────────────────────────────────────────

    [Fact]
    public void Build_CarriesThePeriodSoTheFileStandsAlone()
    {
        var csv = Build(Row("Amoxicillin", AwareGroupCodes.Access, 200, SecretRevenue));

        Assert.Contains("Report period,2026-08-01 ~ 2026-08-31", csv);
    }

    [Fact]
    public void Build_CarriesTheIngredientTableWithConsumptionAndCounselling()
    {
        var csv = Build(Row("Amoxicillin", AwareGroupCodes.Access, 200, SecretRevenue));

        Assert.Contains(
            "Ingredient,Strength,AwareGroup,Quantity,QuantityShare,Counselled,Sales,PrintRate,PrevQuantity,QuantityChange",
            csv);
        Assert.Contains("Amoxicillin,500 mg,ACCESS,200,", csv);
    }

    [Fact]
    public void Build_CarriesTheGroupSharesAndTheTrend()
    {
        var csv = Build(Row("Amoxicillin", AwareGroupCodes.Access, 200, SecretRevenue));

        Assert.Contains("AwareGroup,Quantity,Share", csv);
        Assert.Contains("ACCESS,200,100%", csv);

        Assert.Contains("AntibioticTrend (2026-08 ~ 2026-08)", csv);
        Assert.Contains("Month,Total,ACCESS,WATCH,RESERVE,NOT_RECOMMENDED", csv);
        Assert.Contains("2026-08,200,200,0,0,0", csv);
    }

    /// <summary>성분명에 쉼표가 들어가는 복합제가 있다. 감싸지 않으면 열이 밀린다.</summary>
    [Fact]
    public void Build_QuotesIngredientNamesContainingCommas()
    {
        var csv = Build(Row("Sulfamethoxazole,Trimethoprim", AwareGroupCodes.Access, 1, 0m));

        Assert.Contains("\"Sulfamethoxazole,Trimethoprim\"", csv);
    }

    /// <summary>
    /// 항생제 판매가 없어도 파일은 나와야 한다. 제출 대상 기간에 항생제를 팔지
    /// 않았다는 사실 자체가 보고 내용이다.
    /// </summary>
    [Fact]
    public void Build_StillProducesAFileWhenNothingWasSold()
    {
        var csv = Build();

        Assert.Contains("Report period,", csv);
        Assert.Contains("ACCESS,0,", csv);
    }
}
