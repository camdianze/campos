namespace PharmaPOS.Application.Receipts;

/// <summary>
/// 영수증 날짜와 일련번호 주기는 프놈펜 시간으로 고정한다.
///
/// 서버(여기서는 PC) 기본 시간대를 쓰면, 계산대 PC의 시간대 설정 하나로
/// 영수증 날짜와 "매일 0001부터" 주기가 통째로 밀린다.
/// 캄보디아는 서머타임이 없고 항상 UTC+07:00이라, 시간대 데이터베이스를
/// 못 읽는 환경에서도 고정 오프셋으로 같은 결과를 낼 수 있다.
/// </summary>
public static class PhnomPenhClock
{
    private static readonly TimeSpan FixedOffset = TimeSpan.FromHours(7);

    private static readonly string[] TimeZoneIds = { "Asia/Phnom_Penh", "SE Asia Standard Time" };

    private static readonly TimeZoneInfo? TimeZone = FindTimeZone();

    /// <summary>UTC 기준 epoch 밀리초를 프놈펜 현지 시각으로 바꾼다.</summary>
    public static DateTimeOffset ToLocal(long unixTimeMilliseconds) =>
        ToLocal(DateTimeOffset.FromUnixTimeMilliseconds(unixTimeMilliseconds));

    public static DateTimeOffset ToLocal(DateTimeOffset instant) =>
        TimeZone is null
            ? instant.ToOffset(FixedOffset)
            : TimeZoneInfo.ConvertTime(instant, TimeZone);

    public static DateTimeOffset Now() => ToLocal(DateTimeOffset.UtcNow);

    private static TimeZoneInfo? FindTimeZone()
    {
        foreach (var id in TimeZoneIds)
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (Exception)
            {
                // 이 이름을 모르는 환경이다. 다음 이름, 없으면 고정 오프셋으로 간다.
            }
        }

        return null;
    }
}
