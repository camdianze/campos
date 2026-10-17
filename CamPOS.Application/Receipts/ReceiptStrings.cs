using System.Text;

namespace PharmaPOS.Application.Receipts;

/// <summary>
/// 영수증 문구의 영어 레이어.
///
/// 이 앱에는 화면 전체를 감싸는 리소스 시스템이 없고, 복약안내가 쓰는
/// locales/{bcp47}.json 이 현지어 레이어만 담는 구조다(영어는 코드 쪽 고정 레이어).
/// 그래서 영수증도 같은 형태를 따르되, 영어 문구를 코드 곳곳에 흩뿌리는 대신
/// receipt.* 키만 담은 최소 리소스 모듈로 여기 한곳에 모은다.
///
/// 값에 들어가는 변수는 "{name}" 자리표시자다. 조각을 이어 붙이지 않는 이유는
/// 크메르어처럼 어순이 다른 언어에서 문장이 깨지기 때문이다.
/// </summary>
public static class ReceiptStrings
{
    private static readonly IReadOnlyDictionary<string, string> EnglishStrings =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [ReceiptStringKeys.LabelReceiptNo] = "Receipt No.",
            [ReceiptStringKeys.LabelDate] = "Date",
            [ReceiptStringKeys.LabelServedBy] = "Served by",
            [ReceiptStringKeys.LabelPayment] = "Payment",
            [ReceiptStringKeys.LabelVatTin] = "VAT TIN {tin}",

            [ReceiptStringKeys.ColumnItem] = "Item",
            [ReceiptStringKeys.ColumnQty] = "Qty",
            [ReceiptStringKeys.ColumnPrice] = "Price",
            [ReceiptStringKeys.ColumnAmount] = "Amount",

            [ReceiptStringKeys.LabelTotalQty] = "Total qty",
            [ReceiptStringKeys.LabelVat] = "VAT",
            [ReceiptStringKeys.LabelTotal] = "Total",
            [ReceiptStringKeys.LabelInRiel] = "In Riel",
            [ReceiptStringKeys.LabelFxRate] = "1 USD = {rate} KHR",
            [ReceiptStringKeys.LabelCashTendered] = "Cash tendered",
            [ReceiptStringKeys.LabelChangeDue] = "Change due",

            [ReceiptStringKeys.UnitBox] = "Box",
            [ReceiptStringKeys.UnitEach] = "Each",
            [ReceiptStringKeys.LabelPieces] = "{count} units",

            [ReceiptStringKeys.BrandTagline] = "Pharmacy Inventory System",

            [ReceiptStringKeys.ErrorShopNameKmRequired] =
                "Enter the pharmacy name in Khmer. It is printed at the top of every receipt.",
            [ReceiptStringKeys.ErrorShopNameEnRequired] =
                "Enter the pharmacy name in English. It is printed at the top of every receipt.",
            [ReceiptStringKeys.ErrorPrefixRequired] =
                "Enter a receipt number prefix. It is the leading part of numbers such as INV-20260821-0001.",
            [ReceiptStringKeys.ErrorPrefixFormat] =
                "Use 2 to 5 characters, uppercase letters A-Z and digits 0-9 only. For example INV or SP01.",
            [ReceiptStringKeys.ErrorRateRange] =
                "Enter the exchange rate as a number of 1 or more. 0 and negative values are rejected.",
            [ReceiptStringKeys.ErrorVatTinRequired] =
                "VAT display is switched on, so the tax registration number is required. Enter the number issued by the GDT, or switch VAT display off.",
            [ReceiptStringKeys.ErrorVatRateRange] =
                "Enter the VAT rate as a number between 0 and 100.",
            [ReceiptStringKeys.ErrorNotAdministrator] =
                "Only an Administrator can change receipt settings. Sign in with an administrator account.",
            [ReceiptStringKeys.ErrorSaveFailed] =
                "Receipt settings could not be saved. Please try again.",
            [ReceiptStringKeys.MessageSaved] = "Receipt settings saved."
        };

    /// <summary>
    /// 영어 문구. 키가 없으면 키 문자열이 아니라 빈 문자열을 돌려준다 —
    /// 키가 종이에 찍히는 일은 없어야 한다.
    /// </summary>
    public static string English(string key) =>
        EnglishStrings.TryGetValue(key, out var value) ? value : string.Empty;

    /// <summary>
    /// "{name}" 자리표시자를 값으로 바꾼다. 짝이 없는 자리표시자는 그대로 둔다 —
    /// 조용히 지우면 무엇이 빠졌는지 알 수 없다.
    /// </summary>
    public static string Format(string template, params (string Name, string Value)[] values)
    {
        if (string.IsNullOrEmpty(template) || values.Length == 0)
        {
            return template;
        }

        var builder = new StringBuilder(template);

        foreach (var (name, value) in values)
        {
            builder.Replace("{" + name + "}", value);
        }

        return builder.ToString();
    }
}
