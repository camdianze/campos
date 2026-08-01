namespace PharmaPOS.Application.Settings;

/// <summary>
/// App_Setting 테이블에서 쓰는 키 이름. 문자열을 여기저기 흩뿌리지 않기 위해 한곳에 모은다.
/// 한번 정한 키는 바꾸지 않는다 — 이미 배포된 DB의 값이 그대로 남아 있기 때문이다.
/// </summary>
public static class AppSettingKeys
{
    /// <summary>복약안내 출력 기본 동작. always | ask | never.</summary>
    public const string CounsellingPrintMode = "counselling.print_mode";

    /// <summary>복약안내 용지 분량. full | compact.</summary>
    public const string CounsellingSheetFormat = "counselling.sheet_format";

    /// <summary>용지 출력 방식. printer | file.</summary>
    public const string CounsellingOutput = "counselling.output";

    /// <summary>파일 저장 방식일 때의 폴더 경로.</summary>
    public const string CounsellingFileFolder = "counselling.file_folder";

    /// <summary>현지어 로케일 코드(BCP 47). 비어 있으면 영어 단독 출력.</summary>
    public const string CounsellingLocale = "counselling.locale";

    /// <summary>QR에 넣을 주소. 비어 있으면 QR 영역 자체를 인쇄하지 않는다.</summary>
    public const string CounsellingQrUrl = "counselling.qr_url";

    /// <summary>적재된 시드 파일의 지문(해시 + 정규화 규칙 버전). 재적재 여부 판단에 쓴다.</summary>
    public const string AwareSeedSignature = "aware.seed_signature";

    /// <summary>적재된 시드의 출처 표기. 예: 'WHO AWaRe 2025'.</summary>
    public const string AwareSourceVersion = "aware.source_version";
}
