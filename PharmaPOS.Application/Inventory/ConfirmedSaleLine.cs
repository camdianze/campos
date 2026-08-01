namespace PharmaPOS.Application.Inventory;

/// <summary>
/// 저장이 끝난 판매 한 줄과, 그때 만들어진 거래 ID.
///
/// 이 프로그램에는 판매 헤더 테이블이 없고 장바구니 한 줄마다 Stock_Transaction
/// 행이 하나씩 생긴다. 복약안내 로그를 거래에 붙이려면 그 ID를 알아야 해서,
/// 판매 확정 결과가 ID를 함께 돌려준다.
/// </summary>
public class ConfirmedSaleLine
{
    public required string TransactionId { get; init; }

    public required SaleLineItem Line { get; init; }
}
