namespace PharmaPOS.Application.Inventory;

/// <summary>
/// 재고 이력 화면에 표시할 Stock_Transaction 한 줄.
/// 입고·조정·판매·환불이 한 목록에 섞여 오며, 무엇인지는 TransactionType이 말한다.
/// </summary>
public class StockHistoryLineItem
{
    public required string TransactionId { get; set; }
    public required string ProductId { get; set; }
    public required string ProductName { get; set; }
    public required string BatchNumber { get; set; }
    public required long ExpiryDate { get; set; }

    /// <summary>
    /// 기록된 그대로의 수량이다. 부호를 종류별로 맞춰 놓지 않았다 — 환불은 판매를
    /// 되돌리는 뜻으로 음수인데 재고는 오히려 늘고, 게다가 재고로 돌리지 않는 환불도 있다.
    /// 재고가 실제로 얼마나 움직였는지는 StockBefore/StockAfter가 답한다.
    /// </summary>
    public required int Quantity { get; set; }

    /// <summary>"StockIn" / "StockOut" / "Adjustment" / "Refund".</summary>
    public required string TransactionType { get; set; }

    public string? Reason { get; set; }
    public string? PaymentMethod { get; set; }

    /// <summary>
    /// 이 거래 직전·직후 그 배치의 재고. Inventory에서 다시 읽은 값이며 계산한 값이 아니다.
    /// 이 컬럼이 생기기 전의 거래는 채울 방법이 없어 null이고 빈칸으로 나간다 —
    /// 0으로 채우면 "그때 재고가 0이었다"로 읽힌다.
    /// </summary>
    public long? StockBefore { get; set; }

    public long? StockAfter { get; set; }

    public required string Username { get; set; }
    public required long TransactionTime { get; set; }

    /// <summary>
    /// 날짜만으로는 부족하다 — 하루에 여러 번 움직인 배치를 시간순으로 읽는 것이
    /// 이 화면의 용도라, 같은 날 안에서의 순서가 보여야 한다.
    /// </summary>
    public string TransactionTimeText =>
        DateTimeOffset.FromUnixTimeMilliseconds(TransactionTime).ToLocalTime().ToString("yyyy-MM-dd HH:mm");

    /// <summary>화면에 그대로 쓰는 종류 이름. StockOut은 계산대 말로 Sale이다.</summary>
    public string TypeText => TransactionType switch
    {
        "StockIn" => "Stock In",
        "StockOut" => "Sale",
        "Adjustment" => "Adjustment",
        "Refund" => "Refund",
        _ => TransactionType
    };

    /// <summary>
    /// 종류마다 붙는 값이 달라서(입고=유효기간, 조정=사유, 판매=결제수단) 한 칸에 모은다.
    /// 컬럼을 종류마다 따로 두면 목록의 대부분이 빈칸이 된다.
    /// </summary>
    public string Detail => TransactionType switch
    {
        "StockIn" => ExpiryText,
        "Adjustment" => Reason ?? string.Empty,
        "Refund" => string.IsNullOrWhiteSpace(Reason) ? PaymentMethod ?? string.Empty : Reason,
        _ => PaymentMethod ?? string.Empty
    };

    /// <summary>
    /// 0은 1970년이 아니라 "유효기간 모름"이다(Inventory.NoExpiryDate).
    /// 종이로 관리하던 재고에는 날짜가 남아 있지 않은 경우가 흔하다.
    /// </summary>
    private string ExpiryText => ExpiryDate == 0
        ? "Exp —"
        : $"Exp {DateTimeOffset.FromUnixTimeMilliseconds(ExpiryDate).ToLocalTime():yyyy-MM}";
}
