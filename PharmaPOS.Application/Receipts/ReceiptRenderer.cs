using System.Globalization;
using System.Text;
using PharmaPOS.Application.Inventory;

namespace PharmaPOS.Application.Receipts;

/// <summary>
/// 판매 영수증을 고정폭 텍스트로 그린다.
///
/// 지켜야 하는 것들:
///   - 약품명은 번역하지 않는다. 라틴 문자 국제일반명(INN)을 그대로 찍는다.
///     번역하면 다른 약국·병원에서 같은 약인지 확인할 수 없다.
///   - 수량과 금액은 언제나 아라비아 숫자다. 크메르 숫자(០១២៣)를 쓰지 않는다.
///   - 문구는 전부 receipt.* 리소스 키에서 온다. 여기서 문장을 조립하지 않는다.
///   - 부가세는 받은 금액에 "포함된" 세액으로 계산한다. 합계 위에 얹으면
///     영수증 합계와 실제로 오간 돈이 어긋나고 잔돈 계산이 맞지 않는다.
///
/// 폭에 대하여: 80mm는 48칸, 58mm는 32칸으로 잡는다. 감열 프린터 표준 글꼴 A의
/// 값이며, 실제 용지 폭은 드라이버가 정하므로 여기서는 칸 수만 결정한다.
/// </summary>
public static class ReceiptRenderer
{
    public const int Columns80Mm = 48;
    public const int Columns58Mm = 32;

    public static int ColumnsFor(ReceiptPaperWidth width) =>
        width == ReceiptPaperWidth.Mm58 ? Columns58Mm : Columns80Mm;

    public static ReceiptDocument Render(ReceiptRenderRequest request)
    {
        var width = ColumnsFor(request.Settings.PaperWidth);
        var lines = new List<string>();

        AppendShopHeader(lines, request, width);
        AppendTransactionInfo(lines, request, width);
        AppendItems(lines, request, width);
        AppendTotals(lines, request, width);
        AppendFooter(lines, request, width);

        // 감열지는 절단선 위 몇 줄이 롤러에 가려 안 보인다. 빈 줄로 밀어낸다.
        lines.Add(string.Empty);
        lines.Add(string.Empty);

        return new ReceiptDocument
        {
            Lines = lines,
            Width = width,
            ContainsKhmer = lines.Any(ContainsKhmerScript)
        };
    }

    // ── 머리말 ────────────────────────────────────────────────────────────

    private static void AppendShopHeader(List<string> lines, ReceiptRenderRequest request, int width)
    {
        var settings = request.Settings;
        var text = request.Text;

        AppendCentered(lines, text.PrimaryOf(settings.ShopNameKm, settings.ShopNameEn), width);
        AppendCentered(lines, text.SecondaryOf(settings.ShopNameKm, settings.ShopNameEn), width);

        AppendCentered(lines, text.PrimaryOf(settings.ShopAddressKm, settings.ShopAddressEn), width);
        AppendCentered(lines, text.SecondaryOf(settings.ShopAddressKm, settings.ShopAddressEn), width);
        AppendCentered(lines, settings.ShopTel, width);

        // 등록번호는 표기를 켜고 번호를 넣었을 때만 찍는다.
        if (settings.VatEnabled && !string.IsNullOrWhiteSpace(settings.VatTin))
        {
            AppendCentered(
                lines,
                text.Primary(ReceiptStringKeys.LabelVatTin, ("tin", settings.VatTin.Trim())),
                width);
        }

        if (lines.Count > 0)
        {
            lines.Add(Rule('=', width));
        }
    }

    // ── 거래 정보 ─────────────────────────────────────────────────────────

    private static void AppendTransactionInfo(List<string> lines, ReceiptRenderRequest request, int width)
    {
        var settings = request.Settings;
        var text = request.Text;

        if (settings.ShowReceiptNumber && !string.IsNullOrWhiteSpace(request.ReceiptNumber))
        {
            AppendLabelledValue(lines, text, ReceiptStringKeys.LabelReceiptNo, request.ReceiptNumber!, width);
        }

        AppendLabelledValue(
            lines, text, ReceiptStringKeys.LabelDate,
            PhnomPenhClock.ToLocal(request.TransactionTime)
                .ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture),
            width);

        if (settings.ShowStaffName && !string.IsNullOrWhiteSpace(request.StaffName))
        {
            AppendLabelledValue(lines, text, ReceiptStringKeys.LabelServedBy, request.StaffName!, width);
        }

        if (!string.IsNullOrWhiteSpace(request.PaymentMethod))
        {
            AppendLabelledValue(lines, text, ReceiptStringKeys.LabelPayment, request.PaymentMethod!, width);
        }

        lines.Add(Rule('-', width));
    }

    // ── 품목 ──────────────────────────────────────────────────────────────

    /// <summary>
    /// 품목은 두 줄로 낸다. 첫 줄은 약품명, 둘째 줄은 오른쪽에 붙인 수량·단가·금액이다.
    /// 한 줄에 이름과 숫자를 같이 넣으면 긴 국제일반명이 잘리는데,
    /// 약 이름을 자르는 것은 영수증에서 가장 하면 안 되는 일이다.
    /// </summary>
    private static void AppendItems(List<string> lines, ReceiptRenderRequest request, int width)
    {
        var settings = request.Settings;
        var text = request.Text;
        var columns = FigureColumns(width, settings.ShowUnitPrice);

        AppendItemsHeader(lines, text, columns, width, settings.ShowUnitPrice);

        foreach (var item in request.Lines)
        {
            // 약품명은 번역 대상이 아니다. 로케일과 무관하게 원문 그대로 나간다.
            AppendWrapped(lines, string.Empty, "  ", item.ProductName, width);

            if (settings.ShowUnitLabel)
            {
                AppendUnitLabel(lines, text, item, width);
            }

            lines.Add(FigureRow(
                width,
                columns,
                item.Quantity.ToString(CultureInfo.InvariantCulture),
                settings.ShowUnitPrice ? Money(item.UnitPrice) : null,
                Money(item.LineTotal)));
        }

        lines.Add(Rule('-', width));
    }

    private static void AppendItemsHeader(
        List<string> lines, ReceiptText text, FigureLayout columns, int width, bool showPrice)
    {
        var itemLabel = text.Primary(ReceiptStringKeys.ColumnItem);

        var block = FigureBlock(
            columns,
            text.Primary(ReceiptStringKeys.ColumnQty),
            showPrice ? text.Primary(ReceiptStringKeys.ColumnPrice) : null,
            text.Primary(ReceiptStringKeys.ColumnAmount));

        // 표 머리의 "Item"은 왼쪽 끝에, 나머지 칸은 숫자 줄과 같은 자리에 놓는다.
        // 숫자 줄과 같은 방식(오른쪽 끝에 붙이기)으로 자리를 잡아야 세로가 맞는다.
        var itemRoom = Math.Max(0, width - TextLength(block));

        if (TextLength(itemLabel) <= itemRoom)
        {
            lines.Add(itemLabel + new string(' ', itemRoom - TextLength(itemLabel)) + block);
        }
        else
        {
            // 현지어 머리말이 길어 한 줄에 못 들어가면 두 줄로 나눈다.
            // 칸 이름을 잘라내면 어느 숫자가 무엇인지 알 수 없게 된다.
            lines.Add(itemLabel);
            AppendRightAligned(lines, block, width);
        }

        lines.Add(Rule('-', width));
    }

    /// <summary>
    /// 제형·단위만 현지어로 낸다. 박스로 판 줄은 낱개로 몇 개인지도 붙인다 —
    /// 그 표기가 없으면 영수증만 보고는 실제로 몇 개를 산 건지 알 수 없다.
    /// </summary>
    private static void AppendUnitLabel(
        List<string> lines, ReceiptText text, SaleLineItem item, int width)
    {
        var unit = item.IsBoxSale
            ? text.Primary(ReceiptStringKeys.UnitBox)
            : text.Primary(ReceiptStringKeys.UnitEach);

        if (item.IsBoxSale)
        {
            var pieces = text.Primary(
                ReceiptStringKeys.LabelPieces,
                ("count", item.PieceQuantity.ToString(CultureInfo.InvariantCulture)));

            unit = unit + " / " + pieces;
        }

        AppendWrapped(lines, "  ", "  ", unit, width);
    }

    // ── 합계 ──────────────────────────────────────────────────────────────

    private static void AppendTotals(List<string> lines, ReceiptRenderRequest request, int width)
    {
        var settings = request.Settings;
        var text = request.Text;

        AppendLabelledValue(
            lines, text, ReceiptStringKeys.LabelTotalQty,
            request.Lines.Sum(l => l.Quantity).ToString(CultureInfo.InvariantCulture), width);

        // 부가세는 받은 금액에 이미 포함된 것으로 본다. 합계 위에 얹으면
        // 손님이 낸 돈과 영수증 합계가 달라진다.
        if (settings.VatEnabled && settings.VatRate > 0)
        {
            var included = request.TotalAmount * settings.VatRate / (100m + settings.VatRate);

            AppendLabelledValue(
                lines, text, ReceiptStringKeys.LabelVat, "$" + Money(included), width);
        }

        lines.Add(Rule('=', width));

        AppendLabelledValue(
            lines, text, ReceiptStringKeys.LabelTotal, "$" + Money(request.TotalAmount), width);

        if (settings.ShowRiel && settings.ExchangeRate > 0)
        {
            AppendLabelledValue(
                lines, text, ReceiptStringKeys.LabelInRiel,
                RielConverter.Format(request.TotalAmount, settings.ExchangeRate, settings.RielRounding),
                width);

            AppendRightAligned(
                lines,
                text.Primary(
                    ReceiptStringKeys.LabelFxRate,
                    ("rate", settings.ExchangeRate.ToString("N0", CultureInfo.InvariantCulture))),
                width);
        }

        if (request.CashTendered is not null)
        {
            AppendLabelledValue(
                lines, text, ReceiptStringKeys.LabelCashTendered,
                "$" + Money(request.CashTendered.Value), width);

            AppendLabelledValue(
                lines, text, ReceiptStringKeys.LabelChangeDue,
                "$" + Money(request.ChangeDue ?? 0m), width);
        }
    }

    // ── 맺음말 ────────────────────────────────────────────────────────────

    private static void AppendFooter(List<string> lines, ReceiptRenderRequest request, int width)
    {
        var settings = request.Settings;
        var text = request.Text;

        var footer = text.PrimaryOf(settings.FooterKm, settings.FooterEn);

        if (!string.IsNullOrWhiteSpace(footer))
        {
            lines.Add(Rule('-', width));
            AppendCentered(lines, footer, width);
            AppendCentered(lines, text.SecondaryOf(settings.FooterKm, settings.FooterEn), width);
        }

        lines.Add(Rule('=', width));

        // 제품명은 상표라서 번역하지 않는다. 약품명과 같은 이유다.
        AppendCentered(lines, "CamPOS", width);
        AppendCentered(lines, text.Primary(ReceiptStringKeys.BrandTagline), width);
    }

    // ── 레이아웃 도구 ─────────────────────────────────────────────────────

    /// <summary>수량·단가·금액 칸의 폭.</summary>
    private readonly record struct FigureLayout(int Qty, int Price, int Amount)
    {
        public int Total => Qty + Price + Amount;
    }

    private static FigureLayout FigureColumns(int width, bool showPrice)
    {
        var wide = width >= 40;

        return new FigureLayout(
            Qty: wide ? 6 : 5,
            Price: showPrice ? (wide ? 10 : 8) : 0,
            Amount: wide ? 11 : 9);
    }

    /// <summary>수량·단가·금액 세 칸만. 왼쪽 여백은 붙이지 않는다.</summary>
    private static string FigureBlock(
        FigureLayout columns, string qty, string? price, string amount)
    {
        var builder = new StringBuilder();

        builder.Append(PadLeft(qty, columns.Qty));

        if (price is not null)
        {
            builder.Append(PadLeft(price, columns.Price));
        }

        builder.Append(PadLeft(amount, columns.Amount));

        return builder.ToString();
    }

    /// <summary>
    /// 숫자 칸을 용지 오른쪽 끝에 붙인 한 줄.
    /// 칸 폭 합계가 아니라 실제 길이로 여백을 잡는 이유: 값이 칸보다 길면
    /// (현지어 머리말이 그렇다) 폭 합계로 계산한 여백이 줄을 용지 밖으로 밀어낸다.
    /// </summary>
    private static string FigureRow(
        int width, FigureLayout columns, string qty, string? price, string amount)
    {
        var block = FigureBlock(columns, qty, price, amount);

        return new string(' ', Math.Max(0, width - TextLength(block))) + block;
    }

    /// <summary>
    /// "라벨 ... 값" 한 줄. km_en이면 그 아래에 영어 라벨을 들여써서 덧붙인다.
    /// 값은 언제나 오른쪽 끝에 붙어 숫자 자리가 세로로 맞는다.
    /// </summary>
    private static void AppendLabelledValue(
        List<string> lines, ReceiptText text, string key, string value, int width)
    {
        lines.Add(TwoColumn(text.Primary(key), value, width));

        var auxiliary = text.Secondary(key);

        if (auxiliary is not null)
        {
            AppendWrapped(lines, "  ", "  ", auxiliary, width);
        }
    }

    private static string TwoColumn(string left, string right, int width)
    {
        var leftLength = TextLength(left);
        var rightLength = TextLength(right);

        // 둘이 한 줄에 못 들어가면 라벨만 남기고 값은 오른쪽 정렬로 다음 줄에 둔다.
        if (leftLength + 1 + rightLength > width)
        {
            return left;
        }

        return left + new string(' ', width - leftLength - rightLength) + right;
    }

    private static void AppendRightAligned(List<string> lines, string text, int width)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var length = TextLength(text);

        lines.Add(length >= width ? text : new string(' ', width - length) + text);
    }

    private static void AppendCentered(List<string> lines, string? text, int width)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        if (TextLength(text) >= width)
        {
            AppendWrapped(lines, string.Empty, string.Empty, text, width);
            return;
        }

        lines.Add(new string(' ', (width - TextLength(text)) / 2) + text);
    }

    private static string Rule(char character, int width) => new(character, width);

    /// <summary>금액 표기. 현재 문화권을 따르면 기기 설정에 따라 소수점 기호가 바뀐다.</summary>
    private static string Money(decimal value) =>
        value.ToString("0.00", CultureInfo.InvariantCulture);

    private static string PadLeft(string value, int columnWidth)
    {
        var length = TextLength(value);
        return length >= columnWidth ? value : new string(' ', columnWidth - length) + value;
    }

    /// <summary>
    /// 폭에 맞춰 줄을 나눈다. 크메르어처럼 단어 사이 공백이 없는 글은 한 덩어리로
    /// 들어오므로 글자 수로 끊되, 자소 결합이 깨지지 않도록 텍스트 요소 단위로 자른다.
    /// </summary>
    private static void AppendWrapped(
        List<string> lines, string firstPrefix, string hangingPrefix, string text, int width)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var prefix = firstPrefix;
        var current = new StringBuilder();

        int Available() => Math.Max(1, width - TextLength(prefix));

        void Flush()
        {
            if (current.Length == 0)
            {
                return;
            }

            lines.Add(prefix + current);
            current.Clear();
            prefix = hangingPrefix;
        }

        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var remaining = word;

            while (TextLength(remaining) > Available())
            {
                Flush();

                var take = Available();
                lines.Add(prefix + SubstringByTextElements(remaining, 0, take));
                remaining = SubstringByTextElements(remaining, take);
                prefix = hangingPrefix;
            }

            if (current.Length == 0)
            {
                current.Append(remaining);
            }
            else if (TextLength(current.ToString()) + 1 + TextLength(remaining) <= Available())
            {
                current.Append(' ').Append(remaining);
            }
            else
            {
                Flush();
                current.Append(remaining);
            }
        }

        Flush();
    }

    private static int TextLength(string text) => new StringInfo(text).LengthInTextElements;

    private static string SubstringByTextElements(string text, int start, int length)
        => new StringInfo(text).SubstringByTextElements(start, length);

    private static string SubstringByTextElements(string text, int start)
    {
        var info = new StringInfo(text);
        return start >= info.LengthInTextElements
            ? string.Empty
            : info.SubstringByTextElements(start);
    }

    /// <summary>
    /// 자소가 위아래로 쌓이는 크메르 문자(U+1780–U+17D3)가 들어 있는지.
    ///
    /// 리엘 기호(៛)와 크메르 숫자(U+17E0–U+17E9)는 같은 블록에 있지만 제외한다.
    /// 둘 다 한 칸짜리 글리프라 줄 간격을 넓힐 이유가 없고, 리엘 기호는 영어 전용
    /// 영수증에도 찍히므로 포함시키면 영어 영수증까지 크메르어로 판정된다.
    /// </summary>
    private static bool ContainsKhmerScript(string text) =>
        text.Any(c => c >= 'ក' && c <= '៓');
}
