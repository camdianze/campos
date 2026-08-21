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

    /// <summary>
    /// AMR 연구 제출용 사이트 코드. 연구기관이 등록 때 부여한 값을 그대로 담는다.
    ///
    /// 이 값은 약국을 <b>가리키기만 하고 드러내지는 않는다</b> — 코드와 약국의 대응표는
    /// 연구기관만 갖는다. 라이선스 일련번호가 고객명을 담지 않고 발급 대장에만
    /// 대응을 남기는 것과 같은 방식이다(LicensePayload 주석 참조).
    ///
    /// 비어 있어도 된다. 등록 전이거나 약국이 자기 확인용으로 뽑는 경우가 있다.
    /// </summary>
    public const string ResearchSiteCode = "research.site_code";

    /// <summary>적재된 시드 파일의 지문(해시 + 정규화 규칙 버전). 재적재 여부 판단에 쓴다.</summary>
    public const string AwareSeedSignature = "aware.seed_signature";

    /// <summary>적재된 시드의 출처 표기. 예: 'WHO AWaRe 2025'.</summary>
    public const string AwareSourceVersion = "aware.source_version";

    // ── 판매 영수증 ───────────────────────────────────────────────────────
    //
    // 아래 21개 키는 영수증 설정 화면이 읽고 쓰는 전부다. 이 목록은 고정이며
    // 임의로 늘리거나 이름을 바꾸지 않는다. 다른 키들과 달리 점(.)으로 구분한
    // 소문자 이름을 쓰는데, 화면·저장소·문서가 같은 이름을 쓰게 하기 위해서다.

    public const string ShopNameKm = "shop.name.km";
    public const string ShopNameEn = "shop.name.en";
    public const string ShopAddressKm = "shop.addr.km";
    public const string ShopAddressEn = "shop.addr.en";
    public const string ShopTel = "shop.tel";

    /// <summary>영수증 표기 언어. km_en | km | en.</summary>
    public const string PrintLanguage = "print.lang";

    /// <summary>용지 폭. 80 | 58 (mm).</summary>
    public const string PrintWidth = "print.width";

    public const string CurrencyShowRiel = "currency.showRiel";

    /// <summary>1 USD당 리엘. 환율은 변동하므로 관리자가 직접 갱신한다.</summary>
    public const string CurrencyRate = "currency.rate";

    /// <summary>리엘 반올림 단위. 100 | 500 | 0.</summary>
    public const string CurrencyRounding = "currency.rounding";

    public const string ReceiptShowNo = "receipt.show.no";
    public const string ReceiptShowStaff = "receipt.show.staff";
    public const string ReceiptShowPrice = "receipt.show.price";
    public const string ReceiptShowUnit = "receipt.show.unit";

    public const string ReceiptPrefix = "receipt.prefix";

    /// <summary>일련번호 초기화 주기. daily | monthly | never.</summary>
    public const string ReceiptResetCycle = "receipt.resetCycle";

    public const string ReceiptFooterKm = "receipt.footer.km";
    public const string ReceiptFooterEn = "receipt.footer.en";

    public const string VatEnabled = "vat.enabled";
    public const string VatTin = "vat.tin";
    public const string VatRate = "vat.rate";

    /// <summary>
    /// 영수증 설정 키 전체. 한 번의 조회로 다 읽어 오는 데 쓴다.
    /// 순서는 저장 순서이기도 하다.
    /// </summary>
    public static readonly IReadOnlyList<string> ReceiptSettingKeys = new[]
    {
        ShopNameKm, ShopNameEn, ShopAddressKm, ShopAddressEn, ShopTel,
        PrintLanguage, PrintWidth,
        CurrencyShowRiel, CurrencyRate, CurrencyRounding,
        ReceiptShowNo, ReceiptShowStaff, ReceiptShowPrice, ReceiptShowUnit,
        ReceiptPrefix, ReceiptResetCycle, ReceiptFooterKm, ReceiptFooterEn,
        VatEnabled, VatTin, VatRate
    };
}
