using PharmaPOS.Application.Inventory;
using PharmaPOS.Domain.Entities;
using PharmaPOS.Domain.Enums;

namespace PharmaPOS.Tests.Products;

/// <summary>
/// 낱개가를 비워 둔 상품의 가격.
///
/// 소분 상품 190개 중 155개가 낱개가를 비워 두고 있고, 그런 상품은 박스가를 개수로
/// 나눠 쓴다. $10 ÷ 30은 나누어떨어지지 않는데 그 나머지를 그대로 들고 다니면
/// 0.3333333333333333이 화면과 장바구니를 지나 원장까지 들어간다. 낼 수도 없고
/// 맞춰 볼 수도 없는 금액이고, 합계는 19.566666666666666 같은 값이 된다.
///
/// 손으로 적어 넣은 낱개가는 오래전부터 네 자리로 막혀 있었다. 앱이 <b>스스로 만든</b>
/// 값만 그 규칙 밖에 있었던 셈이라, 검사하는 쪽보다 만드는 쪽이 느슨했다.
/// </summary>
public class DerivedLoosePriceTests
{
    private static Product Boxed(decimal sellingPrice, int unitsPerBox, decimal costPrice = 0m) => new()
    {
        ProductId = "p1",
        ProductName = "Amoxil 500mg Capsule",
        Unit = "Capsule",
        Manufacturer = "Maker A",
        UnitsPerBox = unitsPerBox,
        CostPrice = costPrice,
        SellingPrice = sellingPrice,
        SafetyStockLevel = 0,
        Status = EntityStatus.Active,
        CreatedAt = 0
    };

    /// <summary>$10을 30으로 나눈 값은 0.3333에서 끊긴다.</summary>
    [Theory]
    [InlineData(10, 30, 0.3333)]
    [InlineData(10, 14, 0.7143)]
    [InlineData(10, 3, 3.3333)]
    [InlineData(6.5, 18, 0.3611)]
    [InlineData(10, 12, 0.8333)]
    public void DerivedUnitPrice_StopsAtFourDecimals(decimal boxPrice, int unitsPerBox, decimal expected)
    {
        Assert.Equal(expected, Boxed(boxPrice, unitsPerBox).EffectiveUnitSellingPrice);
    }

    /// <summary>나누어떨어지면 그대로다. 없던 자리를 만들어 붙이지 않는다.</summary>
    [Fact]
    public void DerivedUnitPrice_LeavesACleanDivisionAlone()
    {
        Assert.Equal(0.625m, Boxed(10m, 16).EffectiveUnitSellingPrice);
    }

    /// <summary>따로 정해 둔 낱개가가 있으면 나눗셈을 하지 않는다.</summary>
    [Fact]
    public void AStoredUnitPrice_IsUsedAsItIs()
    {
        var product = Boxed(10m, 30);
        product.UnitSellingPrice = 0.5m;

        Assert.Equal(0.5m, product.EffectiveUnitSellingPrice);
    }

    /// <summary>원가도 같은 자리에서 끊는다. 원가 미만 경고가 이 값으로 판단된다.</summary>
    [Fact]
    public void DerivedUnitCost_StopsAtFourDecimals()
    {
        Assert.Equal(0.2333m, Boxed(10m, 30, costPrice: 7m).UnitCostPrice);
    }

    /// <summary>박스/낱개 구분이 없는 상품은 나눌 것이 없다.</summary>
    [Fact]
    public void AProductThatIsNotSoldLoose_KeepsItsOwnPrice()
    {
        var product = Boxed(4.53m, 1);

        Assert.Equal(4.53m, product.EffectiveUnitSellingPrice);
    }

    // ── 줄 금액 ─────────────────────────────────────────────────────────────

    /// <summary>
    /// 단가는 네 자리를 가질 수 있어도 <b>금액</b>은 통화 단위여야 한다.
    /// 영수증은 이 값을 0.00으로 찍는데 원장에 2.3331이 들어가면, 하루치 영수증을
    /// 더한 값과 매출 카드의 숫자가 서로 맞지 않는다.
    /// </summary>
    [Fact]
    public void LineTotal_IsRoundedToCents()
    {
        var line = Line(unitPrice: 0.3333m, quantity: 7);

        Assert.Equal(2.33m, line.LineTotal);
    }

    [Fact]
    public void LineTotal_RoundsHalfAway()
    {
        Assert.Equal(1.67m, Line(unitPrice: 0.3333m, quantity: 5).LineTotal);
    }

    /// <summary>센트에서 이미 떨어지는 금액은 그대로다.</summary>
    [Fact]
    public void LineTotal_LeavesAWholeCentAmountAlone()
    {
        Assert.Equal(9.06m, Line(unitPrice: 4.53m, quantity: 2).LineTotal);
    }

    private static SaleLineItem Line(decimal unitPrice, int quantity) => new()
    {
        ProductId = "p1",
        ProductName = "Amoxil 500mg Capsule",
        InventoryId = "inv-1",
        BatchNumber = "B2401",
        ExpiryDate = 0,
        Quantity = quantity,
        UnitPrice = unitPrice,
        CostPrice = 0m
    };
}
