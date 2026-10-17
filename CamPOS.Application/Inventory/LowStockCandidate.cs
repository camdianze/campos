namespace PharmaPOS.Application.Inventory;

/// <summary>Low Stock 우선순위 분류 전, Repository가 반환하는 원본 데이터.</summary>
public class LowStockCandidate
{
    public required string ProductId { get; set; }
    public required string ProductName { get; set; }
    public required int TotalQuantity { get; set; }
    public required int SafetyStockLevel { get; set; }
}