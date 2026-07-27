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
    public required StockTransaction Transaction { get; set; }
}