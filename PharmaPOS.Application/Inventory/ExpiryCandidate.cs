namespace PharmaPOS.Application.Inventory;

/// <summary>Expiry 우선순위 분류 전, Repository가 반환하는 원본 데이터.</summary>
public class ExpiryCandidate
{
    public required string ProductId { get; set; }
    public required string ProductName { get; set; }
    public required string BatchNumber { get; set; }
    public required long ExpiryDate { get; set; }
    public required int Quantity { get; set; }
}