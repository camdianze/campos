namespace PharmaPOS.Domain.Enums;

/// <summary>
/// POS 판매 시 결제수단. Screen SCR-POS-005, 3.1절.
/// Stage 1 MVP에서는 외부 결제 시스템과 연동하지 않고 기록만 남긴다.
/// </summary>
public enum PaymentMethod
{
    Cash,
    MobilePayment,
    Insurance,
    Credit,
    Other
}