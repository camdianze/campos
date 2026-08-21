namespace PharmaPOS.Application.Receipts;

/// <summary>
/// 판매 영수증 설정값 묶음.
///
/// 여기 있는 초기값이 곧 "키가 없거나 값이 비었을 때 쓰는 기본값"이다.
/// 약국 이름·주소·전화번호에 기본값을 넣지 않는 이유: 그 순간 남의 약국 정보가
/// 코드에 박힌다. 비워 두면 해당 줄이 인쇄되지 않고, 설정 화면이 채우라고 막는다.
/// </summary>
public class ReceiptSettings
{
    public string ShopNameKm { get; set; } = string.Empty;
    public string ShopNameEn { get; set; } = string.Empty;
    public string ShopAddressKm { get; set; } = string.Empty;
    public string ShopAddressEn { get; set; } = string.Empty;
    public string ShopTel { get; set; } = string.Empty;

    public ReceiptPrintLanguage PrintLanguage { get; set; } = ReceiptPrintLanguage.KhmerAndEnglish;

    public ReceiptPaperWidth PaperWidth { get; set; } = ReceiptPaperWidth.Mm80;

    public bool ShowRiel { get; set; } = true;

    /// <summary>1 USD가 몇 리엘인지. 환율은 고정이 아니므로 관리자가 직접 갱신한다.</summary>
    public decimal ExchangeRate { get; set; } = 4100m;

    /// <summary>리엘 반올림 단위. 0이면 반올림하지 않는다.</summary>
    public int RielRounding { get; set; } = 100;

    public bool ShowReceiptNumber { get; set; } = true;
    public bool ShowStaffName { get; set; } = true;
    public bool ShowUnitPrice { get; set; } = true;

    /// <summary>제형·단위(크메르어) 표기 여부.</summary>
    public bool ShowUnitLabel { get; set; } = true;

    public string ReceiptPrefix { get; set; } = "INV";

    public ReceiptNumberResetCycle ResetCycle { get; set; } = ReceiptNumberResetCycle.Daily;

    public string FooterKm { get; set; } = string.Empty;
    public string FooterEn { get; set; } = string.Empty;

    public bool VatEnabled { get; set; } = false;
    public string VatTin { get; set; } = string.Empty;
    public decimal VatRate { get; set; } = 10m;

    /// <summary>화면 편집용 사본. 미리보기가 저장 전 상태를 그리는 데 쓴다.</summary>
    public ReceiptSettings Clone() => (ReceiptSettings)MemberwiseClone();
}
