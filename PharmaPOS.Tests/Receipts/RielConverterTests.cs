using PharmaPOS.Application.Receipts;

namespace PharmaPOS.Tests.Receipts;

/// <summary>
/// 리엘 환산 규칙. 캄보디아에는 소액 동전이 유통되지 않아 반올림 단위가
/// 실제로 낼 수 있는 돈과 영수증을 맞추는 장치다.
/// </summary>
public class RielConverterTests
{
    [Theory]
    // 5.10 × 4100 = 20,910 → 100 단위로 20,900
    [InlineData(5.10, 4100, 100, 20900)]
    // 500 단위면 21,000 쪽이 가깝다
    [InlineData(5.10, 4100, 500, 21000)]
    // 단위 0은 1리엘 단위 반올림
    [InlineData(5.10, 4100, 0, 20910)]
    public void ToRiel_RoundsToTheGivenUnit(double usd, double rate, int unit, long expected)
    {
        Assert.Equal(expected, RielConverter.ToRiel((decimal)usd, (decimal)rate, unit));
    }

    /// <summary>
    /// 정확히 절반인 경우 위로 올린다. 은행가 반올림을 쓰면 같은 금액이
    /// 어떤 날은 올라가고 어떤 날은 내려가서 서랍이 맞지 않는다.
    /// </summary>
    [Fact]
    public void ToRiel_RoundsHalfUp()
    {
        // 0.0125 × 4000 = 50 → 100 단위 반올림의 정확한 중간
        Assert.Equal(100, RielConverter.ToRiel(0.0125m, 4000m, 100));
    }

    [Fact]
    public void ToRiel_HandlesZeroAmount()
    {
        Assert.Equal(0, RielConverter.ToRiel(0m, 4100m, 100));
    }

    /// <summary>
    /// 천 단위 구분 쉼표와 리엘 기호. 구분자가 기기 문화권을 따라가면
    /// 유럽식 설정에서 20.900이 되어 금액이 다르게 읽힌다.
    /// </summary>
    [Fact]
    public void Format_UsesCommasAndRielSign()
    {
        Assert.Equal("20,900 ៛", RielConverter.Format(20900));
    }
}
