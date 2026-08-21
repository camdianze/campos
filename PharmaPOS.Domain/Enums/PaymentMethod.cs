namespace PharmaPOS.Domain.Enums;

/// <summary>
/// POS 판매 시 결제수단. Screen SCR-POS-005, 3.1절.
/// Stage 1 MVP에서는 외부 결제 시스템과 연동하지 않고 기록만 남긴다.
/// </summary>
public enum PaymentMethod
{
    Cash,
    MobilePayment,

    /// <summary>
    /// 더 이상 계산대에서 고를 수 없다 — 보험 처리를 하지 않기로 했다.
    ///
    /// 값을 지우지 않고 남겨 두는 이유: 이미 이 값으로 저장된 판매가 배포된 DB에
    /// 남아 있을 수 있고, 그 기록은 판매 내역에 그대로 보여야 한다.
    /// 지금 고를 수 있는 것은 PaymentMethods.OfferedAtTill 이 정한다.
    /// </summary>
    Insurance,

    Credit,
    Other
}