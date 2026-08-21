using PharmaPOS.Application.Reports;
using PharmaPOS.Domain.Enums;

namespace PharmaPOS.Tests.Reports;

/// <summary>
/// 등급별 비중. 요약 카드의 ACCESS 비중도 이 목록에서 나오므로,
/// 여기가 틀어지면 스튜어드십 지표가 통째로 틀어진다.
/// </summary>
public class AwareGroupShareTests
{
    private static AntibioticSalesRow Row(string group, int quantity) => new()
    {
        Ingredient = "x",
        Strength = string.Empty,
        AwareGroup = group,
        Quantity = quantity
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

    /// <summary>
    /// 판매가 없는 등급도 0%로 남아야 한다. 목록에서 빠지면
    /// "0이었다"와 "그런 등급이 없다"를 구분할 수 없다.
    /// </summary>
    [Fact]
    public void GroupShares_AlwaysListsAllFourGroupsInSeverityOrder()
    {
        var shares = Data(Row(AwareGroupCodes.Access, 10)).GroupShares;

        Assert.Equal(
            new[]
            {
                AwareGroupCodes.Access,
                AwareGroupCodes.Watch,
                AwareGroupCodes.Reserve,
                AwareGroupCodes.NotRecommended
            },
            shares.Select(s => s.Group));

        Assert.Equal(0, shares[1].Quantity);
        Assert.Equal("0%", shares[1].ShareDisplay);
    }

    [Fact]
    public void GroupShares_AddUpToTheWholeOfAntibioticSales()
    {
        var data = Data(
            Row(AwareGroupCodes.Access, 200),
            Row(AwareGroupCodes.Access, 28),
            Row(AwareGroupCodes.Watch, 1),
            Row(AwareGroupCodes.Reserve, 1));

        Assert.Equal(230, data.AntibioticQuantity);
        Assert.Equal(100m, data.GroupShares.Sum(s => s.SharePercent ?? 0));
        Assert.Equal(228, data.GroupShares[0].Quantity);
    }

    /// <summary>요약 카드의 ACCESS 비중은 등급 목록에서 나온다. 두 값이 갈리면 안 된다.</summary>
    [Fact]
    public void AccessSharePercent_MatchesTheAccessRowOfTheBreakdown()
    {
        var data = Data(
            Row(AwareGroupCodes.Access, 3),
            Row(AwareGroupCodes.Watch, 1));

        Assert.Equal(75m, data.AccessSharePercent);
        Assert.Equal(data.GroupShares[0].SharePercent, data.AccessSharePercent);
    }

    /// <summary>항생제 판매가 없으면 비중을 계산할 수 없다. 0%가 아니라 "없음"이다.</summary>
    [Fact]
    public void GroupShares_ReportNoValueWhenNothingWasSold()
    {
        var data = Data();

        Assert.Null(data.AccessSharePercent);
        Assert.All(data.GroupShares, share => Assert.Null(share.SharePercent));
        Assert.All(data.GroupShares, share => Assert.Equal("—", share.ShareDisplay));
    }
}
