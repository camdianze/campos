using PharmaPOS.Domain.Entities;

namespace PharmaPOS.Application.Inventory;

/// <summary>
/// ISaleRepository에 전달하는 판매 라인 하나의 저장용 데이터.
/// StockTransaction 자체에는 inventory_id가 없으므로(product_id+batch_number로
/// 식별하는 게 원칙), 저장 시 재고를 직접 갱신하기 위해 별도로 함께 전달한다.
/// </summary>
public class SaleLineForSave
{
    public required string InventoryId { get; set; }

    /// <summary>Quantity는 낱개 기준이다 (박스 판매도 낱개로 환산해서 기록한다).</summary>
    public required StockTransaction Transaction { get; set; }

    /// <summary>박스째 파는 줄인지. 안 뜯은 박스가 있어야 저장이 통과한다.</summary>
    public bool IsBoxSale { get; set; }

    /// <summary>박스 판매 시 차감할 박스 수. 낱개 판매면 0.</summary>
    public int BoxCount { get; set; }

    /// <summary>상품의 박스당 낱개 수. 저장 시점에 박스/낱개를 다시 나누는 데 쓴다.</summary>
    public int UnitsPerBox { get; set; } = 1;
}