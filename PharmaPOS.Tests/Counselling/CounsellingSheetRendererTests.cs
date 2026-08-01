using PharmaPOS.Application.Counselling;
using PharmaPOS.Domain.Enums;

namespace PharmaPOS.Tests.Counselling;

public class CounsellingSheetRendererTests
{
    private const int Width = 32;

    private static CounsellingLocale BuildKhmerLocale(
        bool approved, params string[] omittedKeys)
    {
        var strings = new Dictionary<string, string>
        {
            [CounsellingStringKeys.SheetSubtitle] = "ការណែនាំប្រើថ្នាំអង់ទីប៊ីយោទិច",
            [CounsellingStringKeys.LabelDose] = "កម្រិតថ្នាំ",
            [CounsellingStringKeys.LabelFrequency] = "ចំនួនដងក្នុងមួយថ្ងៃ",
            [CounsellingStringKeys.LabelDuration] = "រយៈពេល",
            [CounsellingStringKeys.LabelTake] = "ពេលប្រើ",
            [CounsellingStringKeys.TakeBefore] = "មុនអាហារ",
            [CounsellingStringKeys.TakeAfter] = "ក្រោយអាហារ",
            [CounsellingStringKeys.TakeEither] = "ពេលណាក៏បាន",
            [CounsellingStringKeys.SectionImportant] = "សំខាន់",
            [CounsellingStringKeys.Important1] = "ត្រូវប្រើថ្នាំឲ្យគ្រប់វគ្គ",
            [CounsellingStringKeys.Important2] = "កុំឈប់ប្រើមុនកំណត់",
            [CounsellingStringKeys.Important3] = "កុំចែកថ្នាំអង់ទីប៊ីយោទិចឲ្យអ្នកដទៃ",
            [CounsellingStringKeys.Important4] = "កុំទុកថ្នាំសល់សម្រាប់ពេលក្រោយ",
            [CounsellingStringKeys.Important5] = "បើមានផលរំខាន ត្រូវទៅជួបគ្រូពេទ្យ",
            [CounsellingStringKeys.QrCaption] = "ព័ត៌មានបន្ថែម"
        };

        foreach (var key in omittedKeys)
        {
            strings.Remove(key);
        }

        return new CounsellingLocale(
            "km-KH", "ភាសាខ្មែរ", "Khmer", LocaleRenderMode.Raster,
            approved ? "approved" : "pending", null, "1.0.0", strings);
    }

    private static CounsellingSheetRequest BuildRequest(
        AwareGroup group = AwareGroup.Access,
        CounsellingLocale? locale = null,
        CounsellingSheetFormat format = CounsellingSheetFormat.Full,
        string? qrUrl = null)
    {
        return new CounsellingSheetRequest
        {
            ProductName = "Amoxicillin 500mg",
            GenericName = "Amoxicillin",
            AtcCode = "J01CA04",
            AwareGroup = group,
            SourceVersion = "WHO AWaRe 2025",
            Locale = locale ?? CounsellingLocale.EnglishOnly,
            Format = format,
            Width = Width,
            QrUrl = qrUrl
        };
    }

    private static string RenderText(CounsellingSheetRequest request)
        => CounsellingSheetRenderer.Render(request).ToPlainText();

    // ── 분류 표시 ────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(AwareGroup.Access, "[ACCESS]", "■□□")]
    [InlineData(AwareGroup.Watch, "[WATCH]", "■■□")]
    [InlineData(AwareGroup.Reserve, "[RESERVE]", "■■■")]
    [InlineData(AwareGroup.NotRecommended, "[NOT RECOMMENDED]", "▨▨▨")]
    public void Render_ShowsGroupLabelAndPattern(AwareGroup group, string label, string pattern)
    {
        var text = RenderText(BuildRequest(group));

        Assert.Contains(label, text);
        Assert.Contains(pattern, text);
    }

    /// <summary>ACCESS는 처방 권고 문구를 붙이지 않는다.</summary>
    [Fact]
    public void Render_OmitsPrescriptionLineForAccess()
    {
        Assert.DoesNotContain("Prescription strongly", RenderText(BuildRequest(AwareGroup.Access)));
    }

    [Theory]
    [InlineData(AwareGroup.Watch)]
    [InlineData(AwareGroup.Reserve)]
    [InlineData(AwareGroup.NotRecommended)]
    public void Render_AddsPrescriptionLineForNonAccessGroups(AwareGroup group)
    {
        var text = RenderText(BuildRequest(group));

        Assert.Contains("Prescription strongly", text);
        Assert.Contains("recommended.", text);
    }

    /// <summary>
    /// 분류명은 로케일이 검수 완료라도 번역되지 않는다.
    /// 번역되는 순간 국가 간 지표 집계가 불가능해진다.
    /// </summary>
    [Fact]
    public void Render_NeverTranslatesGroupLabel()
    {
        var text = RenderText(BuildRequest(
            AwareGroup.Watch, BuildKhmerLocale(approved: true)));

        Assert.Contains("[WATCH]", text);
    }

    // ── 공란 유지 ────────────────────────────────────────────────────────────

    /// <summary>
    /// 용량·횟수·기간은 공란이 설계 의도다. 시스템이 채우면 처방 행위가 된다.
    /// </summary>
    [Fact]
    public void Render_LeavesDoseFieldsBlank()
    {
        var text = RenderText(BuildRequest());

        Assert.Contains("______ tablet(s)", text);
        Assert.Contains("______ time(s)/day", text);
        Assert.Contains("______ day(s)", text);
    }

    [Fact]
    public void Render_LeavesTakeTimingUnchecked()
    {
        var text = RenderText(BuildRequest());

        Assert.Contains("[ ] Before", text);
        Assert.Contains("[ ] After", text);
        Assert.Contains("[ ] Either", text);
        Assert.DoesNotContain("[x]", text);
        Assert.DoesNotContain("[X]", text);
    }

    // ── 로케일 ───────────────────────────────────────────────────────────────

    /// <summary>수용 기준: 미검수 로케일이면 크메르어가 한 글자도 나가지 않는다.</summary>
    [Fact]
    public void Render_PrintsEnglishOnlyForPendingLocale()
    {
        var text = RenderText(BuildRequest(locale: BuildKhmerLocale(approved: false)));

        Assert.DoesNotContain(text, c => IsKhmer(c));
        Assert.Contains("Dose", text);
    }

    /// <summary>수용 기준: 검수 완료 로케일이면 현지어가 영어와 함께 나간다.</summary>
    [Fact]
    public void Render_PrintsLocalStringsForApprovedLocale()
    {
        var text = RenderText(BuildRequest(locale: BuildKhmerLocale(approved: true)));

        Assert.Contains("កម្រិតថ្នាំ", text);
        Assert.Contains("Dose", text);
    }

    /// <summary>
    /// 수용 기준: 키 하나가 빠지면 그 줄만 영어가 되고 나머지는 정상이어야 한다.
    /// 빈칸이나 키 문자열이 용지에 찍히면 안 된다.
    /// </summary>
    [Fact]
    public void Render_FallsBackPerLineWhenKeyIsMissing()
    {
        var locale = BuildKhmerLocale(approved: true, CounsellingStringKeys.LabelDose);
        var document = CounsellingSheetRenderer.Render(BuildRequest(locale: locale));
        var text = document.ToPlainText();

        // 빠진 줄은 영어만 남는다.
        Assert.Contains("Dose", text);
        Assert.DoesNotContain("Dose / ", text);

        // 나머지 줄의 현지어는 그대로 나온다.
        Assert.Contains("រយៈពេល", text);

        // 키 문자열이 그대로 찍히지 않는다.
        Assert.DoesNotContain("label.dose", text);
        Assert.DoesNotContain("{", text);
    }

    /// <summary>자소 결합이 유지돼야 한다 — 줄바꿈이 결합 문자를 쪼개면 안 된다.</summary>
    [Fact]
    public void Render_KeepsKhmerGraphemeClustersIntact()
    {
        var document = CounsellingSheetRenderer.Render(
            BuildRequest(locale: BuildKhmerLocale(approved: true)));

        // 결합 표시(coeng 등)로 시작하는 줄이 있으면 클러스터가 잘린 것이다.
        Assert.All(document.Lines, line =>
        {
            var trimmed = line.TrimStart();
            Assert.True(trimmed.Length == 0 || !IsKhmerCombiningMark(trimmed[0]),
                $"자소가 분해된 줄: '{line}'");
        });
    }

    // ── 형식 / 폭 ────────────────────────────────────────────────────────────

    /// <summary>58mm 감열지 폭을 넘는 줄이 있으면 현장에서 잘려 나온다.</summary>
    [Theory]
    [InlineData(32)]
    [InlineData(48)]
    public void Render_KeepsEveryLineWithinWidth(int width)
    {
        var request = BuildRequest(locale: BuildKhmerLocale(approved: true), qrUrl: "https://example.org/amr");
        request.Width = width;

        var document = CounsellingSheetRenderer.Render(request);

        Assert.All(document.Lines, line =>
            Assert.True(
                new System.Globalization.StringInfo(line).LengthInTextElements <= width,
                $"{width}자를 넘는 줄: '{line}'"));
    }

    [Fact]
    public void Render_CompactFormatIsShorterAndDropsSignatureBlock()
    {
        var full = CounsellingSheetRenderer.Render(BuildRequest(qrUrl: "https://example.org/amr"));
        var compact = CounsellingSheetRenderer.Render(
            BuildRequest(format: CounsellingSheetFormat.Compact, qrUrl: "https://example.org/amr"));

        Assert.True(compact.Lines.Count < full.Lines.Count);

        var compactText = compact.ToPlainText();
        Assert.DoesNotContain("Pharmacist :", compactText);
        Assert.DoesNotContain("[QR]", compactText);
        Assert.Null(compact.QrUrl);

        // 축약본이어도 어느 약의 안내인지와 분류는 남아야 쓸모가 있다.
        Assert.Contains("Amoxicillin 500mg", compactText);
        Assert.Contains("[ACCESS]", compactText);

        // 공란과 주의사항은 축약본의 본체다.
        Assert.Contains("______ tablet(s)", compactText);
        Assert.Contains("Do not share antibiotics", compactText);
    }

    // ── 금지 사항 ────────────────────────────────────────────────────────────

    /// <summary>
    /// 이모지를 프린터로 보내면 안 된다. 감열 프린터는 흑백 단색이라
    /// 이모지 글리프가 없어 '?'나 공백으로 찍힌다.
    /// 이모지는 전부 BMP 밖이라 서로게이트 쌍으로 표현되므로 그것으로 걸러낸다.
    /// </summary>
    [Fact]
    public void Render_ContainsNoEmoji()
    {
        var document = CounsellingSheetRenderer.Render(
            BuildRequest(locale: BuildKhmerLocale(approved: true), qrUrl: "https://example.org/amr"));

        Assert.All(document.Lines, line =>
            Assert.DoesNotContain(line, char.IsSurrogate));
    }

    /// <summary>출처 표기가 인쇄물에 남아야 한다.</summary>
    [Fact]
    public void Render_ShowsSourceVersionAndDisclaimer()
    {
        var text = RenderText(BuildRequest());

        Assert.Contains("WHO AWaRe 2025", text);
        Assert.Contains("does not replace", text);
    }

    [Fact]
    public void Render_OmitsQrBlockWhenNoUrlConfigured()
    {
        var document = CounsellingSheetRenderer.Render(BuildRequest(qrUrl: null));

        Assert.DoesNotContain("[QR]", document.ToPlainText());
        Assert.Null(document.QrUrl);
    }

    [Fact]
    public void Render_IncludesQrCaptionWhenUrlConfigured()
    {
        var document = CounsellingSheetRenderer.Render(BuildRequest(qrUrl: "https://example.org/amr"));

        Assert.Contains("[QR]", document.ToPlainText());
        Assert.Equal("https://example.org/amr", document.QrUrl);
    }

    private static bool IsKhmer(char c) => c is >= 'ក' and <= '៿';

    private static bool IsKhmerCombiningMark(char c) =>
        c is '្' or (>= '឴' and <= '៑') or (>= '៝' and <= '៝');
}
