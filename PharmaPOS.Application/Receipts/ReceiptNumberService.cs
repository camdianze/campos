using System.Globalization;
using PharmaPOS.Application.Repositories;

namespace PharmaPOS.Application.Receipts;

/// <summary>
/// 영수증 번호를 발급한다. 형식은 {prefix}-{YYYYMMDD}-{0001}.
/// </summary>
public interface IReceiptNumberService
{
    /// <summary>
    /// 판매에 붙일 영수증 번호를 돌려준다. 같은 판매를 다시 출력하면 같은 번호가 나온다.
    /// 발번에 실패하면 null이다 — 번호가 없다고 영수증을 못 내면 안 된다.
    /// </summary>
    Task<string?> IssueAsync(string saleKey, ReceiptSettings settings, long transactionTime);
}

/// <summary>
/// IReceiptNumberService의 구현체.
///
/// 날짜는 언제나 프놈펜 시간이다. PC 시간대 설정에 따라 "매일 0001부터"가
/// 밀리면 하루 경계에서 번호가 겹치거나 건너뛴다.
///
/// 번호의 날짜 부분은 초기화 주기와 무관하게 항상 YYYYMMDD다.
/// 주기가 바꾸는 것은 "언제 0001로 되돌리는가"뿐이다.
/// </summary>
public class ReceiptNumberService : IReceiptNumberService
{
    private readonly IReceiptNumberRepository _repository;

    public ReceiptNumberService(IReceiptNumberRepository repository)
    {
        _repository = repository;
    }

    public async Task<string?> IssueAsync(string saleKey, ReceiptSettings settings, long transactionTime)
    {
        var prefix = settings.ReceiptPrefix?.Trim();

        if (string.IsNullOrEmpty(prefix))
        {
            return null;
        }

        var local = PhnomPenhClock.ToLocal(transactionTime);
        var datePart = local.ToString("yyyyMMdd", CultureInfo.InvariantCulture);

        // 서식의 "{0:0000}"만 저장소가 채운다. 접두어와 날짜는 여기서 확정한다.
        var format = prefix + "-" + datePart + "-{0:0000}";

        try
        {
            return await _repository.IssueAsync(
                saleKey,
                CounterKey(prefix, settings.ResetCycle, local),
                format,
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        }
        catch (Exception)
        {
            // 발번 실패는 영수증 번호 줄이 빠지는 것으로 끝난다. 판매는 이미 끝났다.
            return null;
        }
    }

    /// <summary>
    /// 어느 카운터에서 번호를 뽑을지. 카운터가 달라지는 순간이 곧 0001로 돌아가는 순간이다.
    /// </summary>
    private static string CounterKey(
        string prefix, ReceiptNumberResetCycle cycle, DateTimeOffset local) => cycle switch
    {
        ReceiptNumberResetCycle.Daily =>
            prefix + ":" + local.ToString("yyyyMMdd", CultureInfo.InvariantCulture),
        ReceiptNumberResetCycle.Monthly =>
            prefix + ":" + local.ToString("yyyyMM", CultureInfo.InvariantCulture),
        _ => prefix
    };
}

/// <summary>
/// 판매 하나를 가리키는 키.
///
/// 이 프로그램에는 판매 헤더 테이블이 없고, 한 판매는 (transaction_time, user_id)가
/// 같은 Stock_Transaction 행들의 묶음이다. 판매 내역과 환불이 이미 그 짝으로
/// 한 판매를 찾으므로, 영수증 번호도 같은 짝을 키로 쓴다.
/// 그래야 판매 내역에서 재출력할 때 처음 발급된 번호를 그대로 다시 찾을 수 있다.
/// </summary>
public static class ReceiptSaleKey
{
    public static string For(long transactionTime, string userId) =>
        transactionTime.ToString(CultureInfo.InvariantCulture) + "|" + userId;
}
