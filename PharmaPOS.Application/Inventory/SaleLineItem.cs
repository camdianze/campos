namespace PharmaPOS.Application.Inventory;

/// <summary>
/// POS 판매 화면(SCR-POS-005)의 Sale Cart 한 줄.
///
/// 수량에 대해. Quantity는 "판매 단위 기준 개수"다 — 박스로 팔면 박스 개수,
/// 낱개로 팔면 낱개 개수. 금액(LineTotal)은 이 값과 판매 단위 가격으로 계산한다.
/// 반면 재고 차감과 원장(Stock_Transaction) 기록은 언제나 낱개 기준이라
/// PieceQuantity를 쓴다. 이 둘을 섞으면 박스 하나를 팔고 재고가 하나만 줄어든다.
/// </summary>
public class SaleLineItem
{
    public required string ProductId { get; set; }
    public required string ProductName { get; set; }

    /// <summary>항생제 복약안내 매칭에 쓴다. 항생제가 아닌 상품은 비어 있다.</summary>
    public string? GenericName { get; set; }

    /// <summary>항생제 복약안내 매칭에 쓴다. 성분명보다 우선한다.</summary>
    public string? AtcCode { get; set; }
    public required string InventoryId { get; set; }
    public required string BatchNumber { get; set; }
    public required long ExpiryDate { get; set; }

    /// <summary>판매 단위 기준 개수. IsBoxSale이면 박스 개수, 아니면 낱개 개수.</summary>
    public required int Quantity { get; set; }

    /// <summary>판매 단위 하나의 가격. IsBoxSale이면 박스 한 통 가격이다.</summary>
    public required decimal UnitPrice { get; set; }

    /// <summary>Selling Price &lt; Cost Price 경고 판단에 사용. UnitPrice와 같은 단위여야 한다.</summary>
    public required decimal CostPrice { get; set; }

    /// <summary>박스째 파는 줄인지. 낱개 판매(-EA 바코드)면 false.</summary>
    public bool IsBoxSale { get; set; }

    /// <summary>상품의 박스당 낱개 수. 박스/낱개 구분이 없는 상품은 1.</summary>
    public int UnitsPerBox { get; set; } = 1;

    /// <summary>
    /// 이 줄의 금액. 센트에서 끊는다.
    ///
    /// UnitPrice는 네 자리까지 가질 수 있다 — 낱개가는 박스가를 개수로 나눈 값이거나
    /// 리엘에서 환산한 값이라 센트로 떨어지지 않는다. 하지만 그것은 <b>단가</b>이고,
    /// 실제로 기록되고 인쇄되는 <b>금액</b>은 통화 단위여야 한다.
    ///
    /// 끊지 않으면 영수증과 원장이 어긋난다: 영수증은 이 값을 0.00 형식으로 찍는데
    /// (ReceiptRenderer) 원장의 total_amount에는 2.3331이 들어가서, 하루치 영수증을
    /// 더한 값과 매출 카드의 숫자가 서로 맞지 않게 된다.
    /// </summary>
    public decimal LineTotal =>
        decimal.Round(Quantity * UnitPrice, 2, MidpointRounding.AwayFromZero);

    /// <summary>낱개로 환산한 실제 출고 수량. 재고 차감과 원장 기록에 쓰는 값이다.</summary>
    public int PieceQuantity => IsBoxSale ? Quantity * UnitsPerBox : Quantity;


    /// <summary>화면·영수증에 붙일 판매 단위 표기.</summary>
    public string SaleUnitLabel => IsBoxSale ? "Box" : "Each";
}
