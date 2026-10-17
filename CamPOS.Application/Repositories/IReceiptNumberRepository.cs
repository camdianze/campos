namespace PharmaPOS.Application.Repositories;

/// <summary>
/// 영수증 일련번호 저장소.
///
/// 번호가 중복 발급되면 환불 대조가 불가능해지므로, 번호를 올리는 일과
/// 그 번호를 판매에 붙이는 일이 한 트랜잭션 안에서 끝나야 한다.
/// </summary>
public interface IReceiptNumberRepository
{
    /// <summary>이 판매에 이미 붙은 번호가 있으면 돌려준다. 재출력이 같은 번호를 쓰게 한다.</summary>
    Task<string?> FindAsync(string saleKey);

    /// <summary>
    /// counterKey의 일련번호를 하나 올리고, 그 번호로 만든 영수증 번호를 판매에 붙인다.
    /// 이미 번호가 붙어 있으면 일련번호를 올리지 않고 붙어 있던 번호를 돌려준다.
    /// </summary>
    /// <param name="format">
    /// 일련번호를 끼워 넣을 서식. 예: "INV-20260821-{0:0000}".
    /// 접두어·날짜·자릿수 규칙은 호출자(Application)가 정하고, 여기서는 번호만 채운다.
    /// </param>
    Task<string> IssueAsync(string saleKey, string counterKey, string format, long issuedAt);
}
