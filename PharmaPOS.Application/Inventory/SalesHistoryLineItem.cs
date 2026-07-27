namespace PharmaPOS.Application.Inventory;

/// <summary>
/// 판매 내역 화면(SCR-SALES-007)에 표시할 조인된 판매 라인 데이터.
/// </summary>
public class SalesHistoryLineItem
{
    public required string TransactionId { get; set; }
    public required string ProductId { get; set; }
    public required string ProductName { get; set; }
    public required string BatchNumber { get; set; }
    public required int Quantity { get; set; }
    public required decimal UnitPrice { get; set; }
    public required decimal LineTotal { get; set; }
    public required string PaymentMethod { get; set; }
    public required string UserId { get; set; }
    public required string Username { get; set; }
    public required long TransactionTime { get; set; }
}