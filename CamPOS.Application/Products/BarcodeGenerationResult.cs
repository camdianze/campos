namespace PharmaPOS.Application.Products;

/// <summary>
/// 내부 바코드 생성/조회 결과.
/// </summary>
public class BarcodeGenerationResult
{
    public bool IsSuccess { get; }
    public string? InternalBarcode { get; }
    public string? Message { get; }

    private BarcodeGenerationResult(bool isSuccess, string? internalBarcode, string? message)
    {
        IsSuccess = isSuccess;
        InternalBarcode = internalBarcode;
        Message = message;
    }

    public static BarcodeGenerationResult Success(string internalBarcode) =>
        new(true, internalBarcode, null);

    public static BarcodeGenerationResult Failure(string message) =>
        new(false, null, message);
}