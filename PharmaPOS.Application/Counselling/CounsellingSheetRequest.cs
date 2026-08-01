using PharmaPOS.Domain.Enums;

namespace PharmaPOS.Application.Counselling;

/// <summary>
/// 복약안내 용지 한 장을 그리는 데 필요한 입력.
/// </summary>
public class CounsellingSheetRequest
{
    public required string ProductName { get; set; }

    public string? GenericName { get; set; }

    public string? AtcCode { get; set; }

    /// <summary>매칭된 AWaRe 그룹. 이 값이 없으면 애초에 용지를 뽑지 않는다.</summary>
    public required AwareGroup AwareGroup { get; set; }

    /// <summary>예: 'WHO AWaRe 2025'. 인쇄물에 출처로 찍힌다.</summary>
    public string? SourceVersion { get; set; }

    /// <summary>현지어 레이어. 없으면 CounsellingLocale.EnglishOnly를 넘긴다.</summary>
    public required CounsellingLocale Locale { get; set; }

    public CounsellingSheetFormat Format { get; set; } = CounsellingSheetFormat.Full;

    /// <summary>
    /// 한 줄에 들어가는 글자 수. 58mm 감열지가 32자, 80mm가 48자다.
    /// </summary>
    public int Width { get; set; } = 32;

    /// <summary>QR에 넣을 주소. 비어 있으면 QR 영역을 아예 그리지 않는다.</summary>
    public string? QrUrl { get; set; }
}
