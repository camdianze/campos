namespace PharmaPOS.Application.Receipts;

/// <summary>
/// 그려진 영수증. 감열지에 그대로 보낼 고정폭 텍스트 줄들이다.
/// </summary>
public class ReceiptDocument
{
    public required IReadOnlyList<string> Lines { get; init; }

    /// <summary>인쇄 칼럼 수. print.width에서 나온다.</summary>
    public required int Width { get; init; }

    /// <summary>크메르어가 한 글자라도 들어 있는지. 인쇄 줄 간격을 넓히는 데 쓴다.</summary>
    public required bool ContainsKhmer { get; init; }
}
