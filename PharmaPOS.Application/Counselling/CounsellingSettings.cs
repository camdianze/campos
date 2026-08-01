namespace PharmaPOS.Application.Counselling;

/// <summary>
/// 복약안내 기능의 설정값 묶음.
/// </summary>
public class CounsellingSettings
{
    public CounsellingPrintMode PrintMode { get; set; } = CounsellingPrintMode.Always;

    public CounsellingSheetFormat SheetFormat { get; set; } = CounsellingSheetFormat.Full;

    /// <summary>현지어 로케일 코드. 비어 있으면 영어 단독 출력.</summary>
    public string LocaleCode { get; set; } = string.Empty;

    /// <summary>QR/추가 정보 주소. 비어 있으면 해당 영역을 인쇄하지 않는다.</summary>
    public string QrUrl { get; set; } = string.Empty;
}
