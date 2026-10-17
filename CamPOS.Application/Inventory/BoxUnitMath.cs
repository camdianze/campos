namespace PharmaPOS.Application.Inventory;

/// <summary>
/// 한 배치의 재고를 "총 낱개 / 안 뜯은 박스 / 헐어 놓은 낱개"로 들고 있는 값.
/// TotalUnits가 재고의 진실이고, 나머지 둘은 그 총량을 나눠 놓은 것이다.
/// </summary>
public readonly record struct BoxUnitStock(int TotalUnits, int BoxQuantity, int UnitQuantity);

/// <summary>
/// 박스/낱개 재고 계산. 화면(장바구니에 담을 수 있는지)과 저장(실제 차감) 양쪽에서
/// 같은 규칙을 써야 하므로 한 곳에 모아 둔다.
///
/// 재고 판정은 언제나 TotalUnits가 기준이고, 박스/낱개 값은 "지금 몇 통이 안 뜯긴 채 있는가"를
/// 나타내는 표현일 뿐이다. 그래서 둘이 총량과 어긋난 데이터를 만나면(예: 컬럼이 생기기 전에
/// 쌓인 행) 계산 전에 총량 기준으로 다시 나눠서 자기 교정한다.
/// </summary>
public static class BoxUnitMath
{
    /// <summary>총 낱개 수를 박스 우선으로 나눈다. 300개 / 박스당 30 → 10박스 + 0낱개.</summary>
    public static BoxUnitStock Split(int totalUnits, int unitsPerBox)
    {
        if (unitsPerBox <= 1)
        {
            // 박스 개념이 없는 상품은 전량이 낱개다.
            return new BoxUnitStock(totalUnits, 0, totalUnits);
        }

        return new BoxUnitStock(totalUnits, totalUnits / unitsPerBox, totalUnits % unitsPerBox);
    }

    /// <summary>박스 × 박스당 개수 + 낱개. 오버플로 방지를 위해 long으로 계산한 뒤 잘라 낸다.</summary>
    public static int ToTotalUnits(int boxQuantity, int unitQuantity, int unitsPerBox)
    {
        var total = (long)boxQuantity * Math.Max(1, unitsPerBox) + unitQuantity;
        return total > int.MaxValue ? int.MaxValue : (int)total;
    }

    /// <summary>
    /// 박스/낱개가 총량과 맞는지 확인하고, 어긋나면 총량 기준으로 다시 나눈다.
    /// 재고의 진실은 총량이므로 총량 쪽을 남긴다.
    /// </summary>
    public static BoxUnitStock Reconcile(BoxUnitStock stock, int unitsPerBox)
    {
        var effectiveUnitsPerBox = Math.Max(1, unitsPerBox);

        var impliedTotal = ToTotalUnits(stock.BoxQuantity, stock.UnitQuantity, effectiveUnitsPerBox);

        return impliedTotal == stock.TotalUnits
            ? stock
            : Split(stock.TotalUnits, effectiveUnitsPerBox);
    }

    /// <summary>
    /// 박스 단위 출고. 안 뜯은 박스가 그만큼 있어야 한다
    /// (총량이 충분해도 헐어 놓은 낱개뿐이면 박스로는 못 판다).
    /// </summary>
    public static bool TryTakeBoxes(
        BoxUnitStock stock, int boxCount, int unitsPerBox, out BoxUnitStock result)
    {
        var effectiveUnitsPerBox = Math.Max(1, unitsPerBox);
        var current = Reconcile(stock, effectiveUnitsPerBox);
        var requestedUnits = (long)boxCount * effectiveUnitsPerBox;

        result = current;

        if (boxCount <= 0 || current.BoxQuantity < boxCount || current.TotalUnits < requestedUnits)
        {
            return false;
        }

        result = new BoxUnitStock(
            current.TotalUnits - (int)requestedUnits,
            current.BoxQuantity - boxCount,
            current.UnitQuantity);

        return true;
    }

    /// <summary>
    /// 낱개 단위 출고. 헐어 놓은 낱개가 모자라면 필요한 만큼 박스를 헐고(BoxesToOpen),
    /// 헐면서 나온 낱개에서 마저 뺀다.
    /// </summary>
    public static bool TryTakeUnits(
        BoxUnitStock stock, int unitCount, int unitsPerBox, out BoxUnitStock result)
    {
        var effectiveUnitsPerBox = Math.Max(1, unitsPerBox);
        var current = Reconcile(stock, effectiveUnitsPerBox);

        result = current;

        if (unitCount <= 0 || current.TotalUnits < unitCount)
        {
            return false;
        }

        var boxesToOpen = BoxesToOpen(current, unitCount, effectiveUnitsPerBox);

        if (current.BoxQuantity < boxesToOpen)
        {
            return false;
        }

        result = new BoxUnitStock(
            current.TotalUnits - unitCount,
            current.BoxQuantity - boxesToOpen,
            current.UnitQuantity + boxesToOpen * effectiveUnitsPerBox - unitCount);

        return true;
    }

    /// <summary>
    /// 환불로 낱개가 되돌아올 때. 되돌아온 물건이 안 뜯긴 박스인지 알 수 없으므로
    /// 전부 헐어 놓은 낱개로 받는다 — 박스로 되돌려 놓으면 다음 손님에게
    /// "안 뜯긴 박스"로 나가 버린다. 총량은 언제나 되돌린 만큼 늘어난다.
    /// </summary>
    public static BoxUnitStock AddUnits(BoxUnitStock stock, int unitCount, int unitsPerBox)
    {
        var current = Reconcile(stock, Math.Max(1, unitsPerBox));

        if (unitCount <= 0)
        {
            return current;
        }

        return new BoxUnitStock(
            current.TotalUnits + unitCount,
            current.BoxQuantity,
            current.UnitQuantity + unitCount);
    }

    /// <summary>
    /// 낱개 unitCount개를 빼려면 박스를 몇 개 헐어야 하는지. 낱개로 충분하면 0.
    /// 판매 화면이 "박스를 여시겠습니까?"를 물어볼지 판단하는 데도 같은 값을 쓴다.
    /// </summary>
    public static int BoxesToOpen(BoxUnitStock stock, int unitCount, int unitsPerBox)
    {
        var effectiveUnitsPerBox = Math.Max(1, unitsPerBox);
        var current = Reconcile(stock, effectiveUnitsPerBox);

        if (current.UnitQuantity >= unitCount)
        {
            return 0;
        }

        var shortfall = unitCount - current.UnitQuantity;

        // 올림 나눗셈. 모자란 만큼을 채우려면 마지막 박스는 덜 써도 헐어야 한다.
        return (shortfall + effectiveUnitsPerBox - 1) / effectiveUnitsPerBox;
    }
}
