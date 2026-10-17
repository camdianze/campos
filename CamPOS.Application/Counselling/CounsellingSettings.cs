namespace PharmaPOS.Application.Counselling;

/// <summary>
/// 복약안내 기능의 설정값 묶음.
/// </summary>
public class CounsellingSettings
{
    public CounsellingPrintMode PrintMode { get; set; } = CounsellingPrintMode.Always;

    public CounsellingSheetFormat SheetFormat { get; set; } = CounsellingSheetFormat.Full;

    /// <summary>프린터로 보낼지, 파일로 저장할지.</summary>
    public CounsellingOutput Output { get; set; } = CounsellingOutput.Printer;

    /// <summary>Output이 File일 때 저장할 폴더. 비어 있으면 기본 폴더를 쓴다.</summary>
    public string FileOutputFolder { get; set; } = string.Empty;

    /// <summary>현지어 로케일 코드. 비어 있으면 영어 단독 출력.</summary>
    public string LocaleCode { get; set; } = string.Empty;

    /// <summary>QR/추가 정보 주소. 비어 있으면 해당 영역을 인쇄하지 않는다.</summary>
    public string QrUrl { get; set; } = string.Empty;

    /// <summary>
    /// AMR 연구 제출용 사이트 코드. 용지 인쇄와는 무관하지만 같은 화면에서 다루므로
    /// 여기 함께 담는다 — 항생제 감시는 "안내를 주는 일"과 "결과를 보고하는 일"이
    /// 한 기능이고, 설정 화면도 하나다.
    ///
    /// 비어 있으면 내보낸 파일에 출처가 적히지 않는다. 그래도 내보내기를 막지는 않는다 —
    /// 약국이 자기 확인용으로 뽑는 것까지 막을 이유는 없다.
    /// </summary>
    public string ResearchSiteCode { get; set; } = string.Empty;
}
