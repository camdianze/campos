namespace PharmaPOS.Application.Inventory;

/// <summary>
/// 영수증 한 장을 인쇄해 달라는 요청.
///
/// 인자를 늘어놓는 대신 객체로 받는 이유: 영수증에 상호·번호·담당자·부가세를
/// 넣게 되면서 필요한 값이 늘었고, 앞으로도 늘어난다. 판매 화면과 판매 내역
/// 재출력이 같은 형태로 부르게 해 두면 새 항목을 붙일 때 한 곳만 고치면 된다.
/// </summary>
public class ReceiptPrintRequest
{
    public required IReadOnlyList<SaleLineItem> Lines { get; init; }

    public required decimal TotalAmount { get; init; }

    /// <summary>
    /// 판매를 가리키는 시각(Unix epoch ms, UTC).
    /// 영수증 번호는 이 값과 UserId의 짝으로 판매를 식별하므로,
    /// 재출력할 때도 원래 판매의 시각을 그대로 넘겨야 같은 번호가 나온다.
    /// </summary>
    public required long TransactionTime { get; init; }

    public required string UserId { get; init; }

    /// <summary>영수증에 찍을 담당자 이름. receipt.show.staff가 꺼져 있으면 쓰이지 않는다.</summary>
    public string? Username { get; init; }

    public string? PaymentMethod { get; init; }

    /// <summary>현금 결제일 때만 채운다.</summary>
    public decimal? CashTendered { get; init; }

    public decimal? ChangeDue { get; init; }
}
