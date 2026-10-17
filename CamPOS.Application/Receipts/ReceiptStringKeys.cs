namespace PharmaPOS.Application.Receipts;

/// <summary>
/// 영수증에서 쓰는 리소스 키. 접두어는 전부 "receipt." 로 통일한다.
///
/// 복약안내 키(CounsellingStringKeys)와 같은 규칙을 따른다:
/// 한번 확정한 키는 바꾸지 않고 추가만 하며, 폐기할 때는 "deprecated." 접두를 붙인다.
/// 이미 배포된 로케일 파일이 이 이름으로 값을 담고 있어서, 키를 바꾸면
/// 그 줄이 조용히 영어로 되돌아간다.
///
/// 값에 들어가는 변수는 "{name}" 형태다. "총 " + n + "개" 처럼 조각을 이어 붙이면
/// 어순이 다른 언어에서 문장이 깨지므로, 변수는 언제나 완성된 문장 안에 놓는다.
/// </summary>
public static class ReceiptStringKeys
{
    // 머리말 / 거래 정보
    public const string LabelReceiptNo = "receipt.lbl.receiptNo";
    public const string LabelDate = "receipt.lbl.date";
    public const string LabelServedBy = "receipt.lbl.servedBy";
    public const string LabelPayment = "receipt.lbl.payment";
    public const string LabelVatTin = "receipt.lbl.vatTin";

    // 품목 표 머리
    public const string ColumnItem = "receipt.col.item";
    public const string ColumnQty = "receipt.col.qty";
    public const string ColumnPrice = "receipt.col.price";
    public const string ColumnAmount = "receipt.col.amount";

    // 합계
    public const string LabelTotalQty = "receipt.lbl.totalQty";
    public const string LabelVat = "receipt.lbl.vat";
    public const string LabelTotal = "receipt.lbl.total";
    public const string LabelInRiel = "receipt.lbl.inRiel";
    public const string LabelFxRate = "receipt.lbl.fxRate";
    public const string LabelCashTendered = "receipt.lbl.cashTendered";
    public const string LabelChangeDue = "receipt.lbl.changeDue";

    // 제형·단위. 약품명은 번역하지 않지만 단위는 번역한다.
    public const string UnitBox = "receipt.unit.box";
    public const string UnitEach = "receipt.unit.each";
    public const string LabelPieces = "receipt.lbl.pieces";

    public const string BrandTagline = "receipt.brand.tagline";

    // 설정 저장 검증 문구. 화면에 그대로 표시된다.
    public const string ErrorShopNameKmRequired = "receipt.err.shopNameKm.required";
    public const string ErrorShopNameEnRequired = "receipt.err.shopNameEn.required";
    public const string ErrorPrefixRequired = "receipt.err.prefix.required";
    public const string ErrorPrefixFormat = "receipt.err.prefix.format";
    public const string ErrorRateRange = "receipt.err.rate.range";
    public const string ErrorVatTinRequired = "receipt.err.vatTin.required";
    public const string ErrorVatRateRange = "receipt.err.vatRate.range";
    public const string ErrorNotAdministrator = "receipt.err.notAdministrator";
    public const string ErrorSaveFailed = "receipt.err.saveFailed";
    public const string MessageSaved = "receipt.msg.saved";
}
