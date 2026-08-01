namespace PharmaPOS.Application.Inventory;

/// <summary>
/// POS 판매 화면(SCR-POS-005)의 Sale Cart 한 줄.
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
    public required int Quantity { get; set; }
    public required decimal UnitPrice { get; set; }

    /// <summary>Selling Price &lt; Cost Price 경고 판단에 사용.</summary>
    public required decimal CostPrice { get; set; }

    public decimal LineTotal => Quantity * UnitPrice;
}