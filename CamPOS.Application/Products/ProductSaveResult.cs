namespace PharmaPOS.Application.Products;

/// <summary>
/// 상품 저장(등록/수정) 시도 결과.
/// 성공/실패 외에, "경고 후 사용자 확인이 필요한" 세 번째 상태가 있다
/// (Screen SCR-PROD-012, 4.3절 "Selling Price &lt; Cost Price" 케이스).
/// </summary>
public class ProductSaveResult
{
    public bool IsSuccess { get; }
    public bool RequiresConfirmation { get; }
    public string? Message { get; }

    private ProductSaveResult(bool isSuccess, bool requiresConfirmation, string? message)
    {
        IsSuccess = isSuccess;
        RequiresConfirmation = requiresConfirmation;
        Message = message;
    }

    public static ProductSaveResult Success() => new(true, false, null);

    public static ProductSaveResult Failure(string message) => new(false, false, message);

    /// <summary>
    /// 저장을 진행하기 전에 사용자에게 확인을 받아야 하는 상태.
    /// 사용자가 "예"를 선택하면, 같은 저장 메서드를 acknowledgeWarning=true로 다시 호출한다.
    /// </summary>
    public static ProductSaveResult NeedsConfirmation(string message) => new(false, true, message);
}