using PharmaPOS.Application.Inventory;
using PharmaPOS.Domain.Enums;

namespace PharmaPOS.Tests.Inventory;

/// <summary>
/// 계산대에서 받는 결제수단.
///
/// 이 검사가 지키는 것은 목록의 내용이 아니라 <b>열거형과 목록이 다른 역할이라는 사실</b>이다.
/// 취급하지 않기로 한 수단을 열거형에서 지워 버리면 이미 그렇게 저장된 판매가
/// 배포된 DB에 남아 있을 때 판매 내역이 그 기록을 잃는다.
/// </summary>
public class PaymentMethodsTests
{
    /// <summary>보험 처리를 하지 않기로 했다. 판매 화면에 나오면 안 된다.</summary>
    [Fact]
    public void OfferedAtTill_DoesNotIncludeInsurance()
    {
        Assert.DoesNotContain(PaymentMethod.Insurance, PaymentMethods.OfferedAtTill);
    }

    /// <summary>
    /// 값 자체는 열거형에 남아 있어야 한다. 지우면 예전 기록을 읽을 근거가 사라진다.
    /// </summary>
    [Fact]
    public void Insurance_RemainsAValueSoOldRecordsStillRead()
    {
        Assert.Contains(PaymentMethod.Insurance, Enum.GetValues<PaymentMethod>());
    }

    /// <summary>
    /// 뺀 것은 보험 하나뿐이다. 나중에 목록을 손대다 현금이 빠지면
    /// 계산대에서 결제수단을 아예 고를 수 없게 된다.
    /// </summary>
    [Fact]
    public void OfferedAtTill_KeepsEveryOtherMethod()
    {
        var expected = Enum.GetValues<PaymentMethod>()
            .Where(method => method != PaymentMethod.Insurance);

        Assert.Equal(expected, PaymentMethods.OfferedAtTill);
    }

    /// <summary>현금은 잔돈 계산이 붙는 유일한 수단이라 특히 빠지면 안 된다.</summary>
    [Fact]
    public void OfferedAtTill_StartsWithCash()
    {
        Assert.Equal(PaymentMethod.Cash, PaymentMethods.OfferedAtTill[0]);
    }
}
