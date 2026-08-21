using System.Globalization;
using PharmaPOS.Application.Counselling;
using PharmaPOS.Application.Inventory;
using PharmaPOS.Application.Receipts;

namespace PharmaPOS.Tests.Receipts;

/// <summary>
/// 영수증 렌더링. 여기서 확인하는 것들은 전부 "종이가 나온 뒤에야 드러나는" 문제다.
/// </summary>
public class ReceiptRendererTests
{
    /// <summary>크메르 숫자 ០~៩. 금액에 이게 섞이면 대조가 불가능해진다.</summary>
    private const char KhmerZero = '០';
    private const char KhmerNine = '៩';

    /// <summary>
    /// 검수를 마친 것으로 친 크메르 로케일. 동봉본은 review_status가 pending이라
    /// 그대로 쓰면 크메르어가 한 글자도 나오지 않아 이 검사들이 무의미해진다.
    /// </summary>
    private static CounsellingLocale ApprovedKhmer() => new(
        localeCode: "km-KH",
        languageName: "ភាសាខ្មែរ",
        script: "Khmer",
        renderMode: LocaleRenderMode.Raster,
        reviewStatus: "approved",
        reviewedBy: "test",
        contentVersion: "1.1.0",
        strings: new Dictionary<string, string>
        {
            [ReceiptStringKeys.LabelReceiptNo] = "លេខវិក្កយបត្រ",
            [ReceiptStringKeys.LabelDate] = "កាលបរិច្ឆេទ",
            [ReceiptStringKeys.LabelServedBy] = "បម្រើដោយ",
            [ReceiptStringKeys.LabelPayment] = "ការទូទាត់",
            [ReceiptStringKeys.ColumnItem] = "ទំនិញ",
            [ReceiptStringKeys.ColumnQty] = "បរិមាណ",
            [ReceiptStringKeys.ColumnPrice] = "តម្លៃ",
            [ReceiptStringKeys.ColumnAmount] = "ចំនួនទឹកប្រាក់",
            [ReceiptStringKeys.LabelTotalQty] = "បរិមាណសរុប",
            [ReceiptStringKeys.LabelTotal] = "សរុប",
            [ReceiptStringKeys.LabelInRiel] = "ជាប្រាក់រៀល",
            [ReceiptStringKeys.LabelFxRate] = "1 ដុល្លារ = {rate} រៀល",
            [ReceiptStringKeys.UnitBox] = "ប្រអប់",
            [ReceiptStringKeys.UnitEach] = "ឯកតា",
            [ReceiptStringKeys.LabelPieces] = "{count} ឯកតា",
            [ReceiptStringKeys.BrandTagline] = "ប្រព័ន្ធគ្រប់គ្រងស្តុកឱសថស្ថាន"
        });

    private static ReceiptSettings Settings() => new()
    {
        ShopNameKm = "ឱសថស្ថានគំរូ",
        ShopNameEn = "Sample Pharmacy",
        ShopAddressEn = "No.123, Phnom Penh",
        ShopTel = "012 345 678",
        ReceiptPrefix = "INV",
        ExchangeRate = 4100m
    };

    private static IReadOnlyList<SaleLineItem> Lines() => new[]
    {
        new SaleLineItem
        {
            ProductId = "p1",
            ProductName = "Amoxicillin 250mg",
            InventoryId = "i1",
            BatchNumber = "B1",
            ExpiryDate = 0,
            Quantity = 2,
            UnitPrice = 1.50m,
            CostPrice = 1m,
            IsBoxSale = true,
            UnitsPerBox = 10
        },
        new SaleLineItem
        {
            ProductId = "p2",
            ProductName = "Paracetamol 500mg",
            InventoryId = "i2",
            BatchNumber = "B2",
            ExpiryDate = 0,
            Quantity = 20,
            UnitPrice = 0.05m,
            CostPrice = 0.02m
        }
    };

    private static ReceiptDocument Render(
        ReceiptSettings settings, ReceiptPrintLanguage language, CounsellingLocale? locale = null)
    {
        var lines = Lines();

        return ReceiptRenderer.Render(new ReceiptRenderRequest
        {
            Settings = settings,
            Text = new ReceiptText(language, locale ?? ApprovedKhmer()),
            Lines = lines,
            TotalAmount = lines.Sum(l => l.LineTotal),
            TransactionTime = 1_755_000_000_000,
            ReceiptNumber = "INV-20260821-0147",
            StaffName = "Sophea C.",
            PaymentMethod = "Cash"
        });
    }

    private static string Text(ReceiptDocument document) =>
        string.Join("\n", document.Lines);

    // ── 언어 ──────────────────────────────────────────────────────────────

    [Fact]
    public void English_PrintsNoKhmerAtAll()
    {
        var document = Render(Settings(), ReceiptPrintLanguage.English);

        Assert.False(document.ContainsKhmer);
        Assert.Contains("Receipt No.", Text(document));
        Assert.Contains("Total", Text(document));

        // 설정에 크메르어 상호가 들어 있어도 영어 전용이면 영어 쪽을 쓴다.
        Assert.Contains("Sample Pharmacy", Text(document));
        Assert.DoesNotContain("ឱសថស្ថានគំរូ", Text(document));
    }

    [Fact]
    public void KhmerAndEnglish_PutsTheEnglishLabelUnderTheKhmerOne()
    {
        var text = Text(Render(Settings(), ReceiptPrintLanguage.KhmerAndEnglish));

        Assert.Contains("លេខវិក្កយបត្រ", text);
        Assert.Contains("Receipt No.", text);
        Assert.Contains("ឱសថស្ថានគំរូ", text);
        Assert.Contains("Sample Pharmacy", text);
    }

    [Fact]
    public void Khmer_DropsTheEnglishAuxiliaryLabels()
    {
        var text = Text(Render(Settings(), ReceiptPrintLanguage.Khmer));

        Assert.Contains("លេខវិក្កយបត្រ", text);
        Assert.DoesNotContain("Receipt No.", text);
        Assert.DoesNotContain("Total qty", text);
    }

    /// <summary>
    /// 번역이 검수되지 않았으면 크메르어가 아니라 영어가 나가야 한다.
    /// km을 골랐다고 백지가 나오면 영수증으로서 쓸모가 없다.
    /// </summary>
    [Fact]
    public void Khmer_FallsBackToEnglishWhenTheTranslationIsNotApproved()
    {
        var text = Text(Render(
            Settings(), ReceiptPrintLanguage.Khmer, CounsellingLocale.EnglishOnly));

        Assert.Contains("Receipt No.", text);
        Assert.Contains("Total", text);
    }

    // ── 번역하지 않는 것들 ────────────────────────────────────────────────

    [Theory]
    [InlineData(ReceiptPrintLanguage.Khmer)]
    [InlineData(ReceiptPrintLanguage.KhmerAndEnglish)]
    [InlineData(ReceiptPrintLanguage.English)]
    public void MedicineNames_AreNeverTranslated(ReceiptPrintLanguage language)
    {
        var text = Text(Render(Settings(), language));

        Assert.Contains("Amoxicillin 250mg", text);
        Assert.Contains("Paracetamol 500mg", text);
    }

    [Theory]
    [InlineData(ReceiptPrintLanguage.Khmer)]
    [InlineData(ReceiptPrintLanguage.KhmerAndEnglish)]
    public void Figures_UseArabicNumeralsOnly(ReceiptPrintLanguage language)
    {
        var text = Text(Render(Settings(), language));

        Assert.DoesNotContain(text, c => c >= KhmerZero && c <= KhmerNine);
    }

    // ── 표시 항목 ─────────────────────────────────────────────────────────

    [Fact]
    public void UnitLabel_IsPrintedInKhmerAndCanBeSwitchedOff()
    {
        var on = Text(Render(Settings(), ReceiptPrintLanguage.KhmerAndEnglish));
        Assert.Contains("ប្រអប់", on);

        var settings = Settings();
        settings.ShowUnitLabel = false;

        var off = Text(Render(settings, ReceiptPrintLanguage.KhmerAndEnglish));
        Assert.DoesNotContain("ប្រអប់", off);
    }

    /// <summary>박스로 판 줄은 낱개 환산 수량을 함께 적는다.</summary>
    [Fact]
    public void BoxSales_ShowHowManyLooseUnitsThatIs()
    {
        Assert.Contains("20 ឯកតា", Text(Render(Settings(), ReceiptPrintLanguage.Khmer)));
    }

    [Fact]
    public void UnitPrice_CanBeHidden()
    {
        var settings = Settings();
        settings.ShowUnitPrice = false;

        var document = Render(settings, ReceiptPrintLanguage.English);
        var text = Text(document);

        // 줄 금액(3.00)은 남고 단가(1.50)는 사라진다.
        Assert.Contains("3.00", text);
        Assert.DoesNotContain("1.50", text);
    }

    [Fact]
    public void ReceiptNumber_CanBeHidden()
    {
        var settings = Settings();
        settings.ShowReceiptNumber = false;

        Assert.DoesNotContain("INV-20260821-0147", Text(Render(settings, ReceiptPrintLanguage.English)));
    }

    [Fact]
    public void StaffName_CanBeHidden()
    {
        var settings = Settings();
        settings.ShowStaffName = false;

        Assert.DoesNotContain("Sophea C.", Text(Render(settings, ReceiptPrintLanguage.English)));
    }

    // ── 금액 ──────────────────────────────────────────────────────────────

    [Fact]
    public void Riel_IsPrintedRoundedAndCanBeSwitchedOff()
    {
        // 3.00 + 1.00 = 4.00 → 4.00 × 4100 = 16,400
        Assert.Contains("16,400 ៛", Text(Render(Settings(), ReceiptPrintLanguage.English)));

        var settings = Settings();
        settings.ShowRiel = false;

        Assert.DoesNotContain("៛", Text(Render(settings, ReceiptPrintLanguage.English)));
    }

    /// <summary>
    /// 부가세는 받은 금액에 포함된 것으로 계산한다. 합계 위에 얹으면
    /// 영수증 합계가 손님이 실제로 낸 돈과 달라진다.
    /// </summary>
    [Fact]
    public void Vat_IsShownAsIncludedInTheTotal()
    {
        var settings = Settings();
        settings.VatEnabled = true;
        settings.VatRate = 10m;
        settings.VatTin = "K001-901234567";

        var text = Text(Render(settings, ReceiptPrintLanguage.English));

        // 합계는 판매 금액 그대로다.
        Assert.Contains("$4.00", text);
        // 4.00 안에 든 10% 부가세 = 4.00 × 10 / 110 = 0.36…
        Assert.Contains("$0.36", text);
        Assert.Contains("VAT TIN K001-901234567", text);
    }

    // ── 용지 폭 ───────────────────────────────────────────────────────────

    [Theory]
    [InlineData(ReceiptPaperWidth.Mm58, ReceiptRenderer.Columns58Mm)]
    [InlineData(ReceiptPaperWidth.Mm80, ReceiptRenderer.Columns80Mm)]
    public void EveryLineFitsThePaper(ReceiptPaperWidth width, int columns)
    {
        var settings = Settings();
        settings.PaperWidth = width;

        var document = Render(settings, ReceiptPrintLanguage.KhmerAndEnglish);

        Assert.Equal(columns, document.Width);

        foreach (var line in document.Lines)
        {
            var length = new StringInfo(line).LengthInTextElements;

            Assert.True(
                length <= columns,
                $"{length} characters do not fit in {columns} columns: {line}");
        }
    }

    /// <summary>
    /// 크메르어가 들어갔는지를 인쇄 쪽에 알려 준다. 줄 간격을 넓히지 않으면
    /// 위아래로 쌓이는 자소가 앞뒤 줄에 닿아 잘려 보인다.
    /// </summary>
    [Fact]
    public void ContainsKhmer_FlagsWhichDocumentsNeedWiderLineSpacing()
    {
        Assert.True(Render(Settings(), ReceiptPrintLanguage.Khmer).ContainsKhmer);
        Assert.False(Render(Settings(), ReceiptPrintLanguage.English).ContainsKhmer);
    }
}
