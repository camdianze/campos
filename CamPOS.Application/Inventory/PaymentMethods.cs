using PharmaPOS.Domain.Enums;

namespace PharmaPOS.Application.Inventory;

/// <summary>
/// 계산대에서 받을 수 있는 결제수단.
///
/// 열거형 전체(PaymentMethod)와 이 목록이 다르다. 열거형은 <b>기록에 남을 수 있는 값</b>이고,
/// 이 목록은 <b>지금 팔 때 고를 수 있는 값</b>이다. 취급하지 않기로 한 수단을 열거형에서
/// 지우지 않는 이유는, 이미 그 값으로 저장된 판매가 남아 있을 수 있기 때문이다.
///
/// 목록이 화면이 아니라 여기 있는 이유: "무엇을 받는가"는 약국의 영업 방침이지
/// 화면 배치가 아니다. 화면에 두면 판매 화면과 판매 내역 필터가 서로 다른 목록을
/// 갖게 되기 쉽고, 어느 쪽이 맞는지 코드를 다 뒤져야 알 수 있다.
/// </summary>
public static class PaymentMethods
{
    /// <summary>
    /// 판매 화면의 결제수단 선택 목록.
    ///
    /// Insurance는 빠져 있다 — 보험 처리를 하지 않기로 했다. 열거형에는 남아 있으므로
    /// 예전에 그렇게 기록된 판매가 있다면 판매 내역에 그대로 보인다.
    /// </summary>
    public static IReadOnlyList<PaymentMethod> OfferedAtTill { get; } = new[]
    {
        PaymentMethod.Cash,
        PaymentMethod.MobilePayment,
        PaymentMethod.Credit,
        PaymentMethod.Other
    };
}
