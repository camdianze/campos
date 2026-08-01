using PharmaPOS.Application.Counselling;
using PharmaPOS.Application.Inventory;
using PharmaPOS.Application.Repositories;
using PharmaPOS.Domain.Entities;
using PharmaPOS.Domain.Enums;

namespace PharmaPOS.Tests.Counselling;

public class CounsellingServiceTests
{
    // ── 테스트용 대역 ────────────────────────────────────────────────────────

    private class FakeSettingsService : ICounsellingSettingsService
    {
        public CounsellingSettings Settings { get; set; } = new();

        public Task<CounsellingSettings> GetAsync() => Task.FromResult(Settings);

        public Task SaveAsync(CounsellingSettings settings)
        {
            Settings = settings;
            return Task.CompletedTask;
        }

        public Task<(int Count, string? SourceVersion)> GetReferenceDataStatusAsync()
            => Task.FromResult((0, (string?)null));
    }

    private class FakeLocaleProvider : ICounsellingLocaleProvider
    {
        public CounsellingLocale Locale { get; set; } = CounsellingLocale.EnglishOnly;

        public Task<CounsellingLocale> GetLocaleAsync(string? localeCode) => Task.FromResult(Locale);

        public Task<IReadOnlyList<CounsellingLocale>> ListAvailableLocalesAsync()
            => Task.FromResult<IReadOnlyList<CounsellingLocale>>(new[] { Locale });
    }

    private class FakePrintingService : ICounsellingSheetPrintingService
    {
        public List<CounsellingSheetDocument> Printed { get; } = new();
        public bool ShouldFail { get; set; }
        public bool ShouldThrow { get; set; }

        public Task<CounsellingPrintResult> PrintAsync(CounsellingSheetDocument document)
        {
            if (ShouldThrow)
            {
                throw new InvalidOperationException("printer exploded");
            }

            if (ShouldFail)
            {
                return Task.FromResult(CounsellingPrintResult.Failure("no printer"));
            }

            Printed.Add(document);
            return Task.FromResult(CounsellingPrintResult.Success());
        }
    }

    private class FakeLogRepository : ICounsellingLogRepository
    {
        public List<CounsellingLogEntry> Entries { get; } = new();
        public bool ShouldThrow { get; set; }

        public Task AddAsync(CounsellingLogEntry entry)
        {
            if (ShouldThrow)
            {
                throw new InvalidOperationException("database unavailable");
            }

            Entries.Add(entry);
            return Task.CompletedTask;
        }

        public Task<CounsellingMetrics> GetMetricsAsync(long fromUtcMillis, long toUtcMillis)
            => Task.FromResult(new CounsellingMetrics());
    }

    private class Harness
    {
        public FakeAwareClassificationRepository Aware { get; } = new FakeAwareClassificationRepository()
            .Add("J01CA04", "Amoxicillin", AwareGroup.Access)
            .Add("J01FA10", "Azithromycin", AwareGroup.Watch)
            .Add("D06AX04", "Neomycin", AwareGroup.Access, isSystemic: false);

        public FakeSettingsService Settings { get; } = new();
        public FakeLocaleProvider Locales { get; } = new();
        public FakePrintingService Printer { get; } = new();
        public FakeLogRepository Log { get; } = new();

        public CounsellingService Build() => new(
            new AntibioticMatchingService(Aware), Settings, Locales, Printer, Log);
    }

    private static ConfirmedSaleLine Line(
        string transactionId, string productName, string? atcCode, string? genericName)
    {
        return new ConfirmedSaleLine
        {
            TransactionId = transactionId,
            Line = new SaleLineItem
            {
                ProductId = "product-" + transactionId,
                ProductName = productName,
                GenericName = genericName,
                AtcCode = atcCode,
                InventoryId = "inv-1",
                BatchNumber = "B1",
                ExpiryDate = 0,
                Quantity = 1,
                UnitPrice = 10m,
                CostPrice = 5m
            }
        };
    }

    // ── 준비 단계 ────────────────────────────────────────────────────────────

    [Fact]
    public async Task PrepareAsync_ReturnsCandidateForMatchedAntibiotic()
    {
        var harness = new Harness();

        var candidates = await harness.Build().PrepareAsync(
            new[] { Line("t1", "Amoxicillin 500mg", "J01CA04", "Amoxicillin") });

        var candidate = Assert.Single(candidates);
        Assert.Equal(AwareGroup.Access, candidate.AwareGroup);
        Assert.False(candidate.RequiresPrompt);
    }

    /// <summary>수용 기준: 항생제 2종을 함께 팔면 용지도 2매다.</summary>
    [Fact]
    public async Task PrepareAsync_ReturnsOneCandidatePerAntibioticInTheSale()
    {
        var harness = new Harness();

        var candidates = await harness.Build().PrepareAsync(new[]
        {
            Line("t1", "Amoxicillin 500mg", "J01CA04", "Amoxicillin"),
            Line("t2", "Azithromycin 250mg", "J01FA10", "Azithromycin"),
            Line("t3", "Paracetamol 500mg", null, "Paracetamol")
        });

        Assert.Equal(2, candidates.Count);
        Assert.Equal(new[] { "t1", "t2" }, candidates.Select(c => c.TransactionId));
    }

    /// <summary>수용 기준: 미등록 항생제는 unmatched로 기록되고 판매는 통과한다.</summary>
    [Fact]
    public async Task PrepareAsync_LogsUnmatchedAndReturnsNoCandidate()
    {
        var harness = new Harness();

        var candidates = await harness.Build().PrepareAsync(
            new[] { Line("t1", "Unlisted Antibiotic", null, "Unlisted Antibiotic") });

        Assert.Empty(candidates);

        var entry = Assert.Single(harness.Log.Entries);
        Assert.Equal(AwareGroupCodes.Unmatched, entry.AwareGroup);
        Assert.False(entry.Printed);
        Assert.Equal(CounsellingService.SkipReasonUnmatched, entry.SkipReason);
    }

    /// <summary>
    /// 수용 기준: 국소 제제는 출력되지 않는다.
    /// 로그도 남기지 않는다 — 남기면 항생제 판매 건수와 ACCESS 비중이
    /// 연고류로 오염돼 지표가 틀어진다.
    /// </summary>
    [Fact]
    public async Task PrepareAsync_SkipsTopicalAgentWithoutLogging()
    {
        var harness = new Harness();

        var candidates = await harness.Build().PrepareAsync(
            new[] { Line("t1", "Neomycin ointment", "D06AX04", "Neomycin") });

        Assert.Empty(candidates);
        Assert.Empty(harness.Log.Entries);
    }

    [Fact]
    public async Task PrepareAsync_MarksCandidateForPromptInAskMode()
    {
        var harness = new Harness();
        harness.Settings.Settings.PrintMode = CounsellingPrintMode.Ask;

        var candidates = await harness.Build().PrepareAsync(
            new[] { Line("t1", "Amoxicillin 500mg", "J01CA04", "Amoxicillin") });

        Assert.True(Assert.Single(candidates).RequiresPrompt);
    }

    /// <summary>never로 꺼 두어도 지표는 계속 쌓인다.</summary>
    [Fact]
    public async Task PrepareAsync_StillLogsWhenPrintingIsDisabled()
    {
        var harness = new Harness();
        harness.Settings.Settings.PrintMode = CounsellingPrintMode.Never;

        var candidates = await harness.Build().PrepareAsync(
            new[] { Line("t1", "Amoxicillin 500mg", "J01CA04", "Amoxicillin") });

        Assert.Empty(candidates);

        var entry = Assert.Single(harness.Log.Entries);
        Assert.Equal(AwareGroupCodes.Access, entry.AwareGroup);
        Assert.Equal(CounsellingService.SkipReasonDisabled, entry.SkipReason);
    }

    /// <summary>참조 데이터가 아예 없으면 전부 unmatched로 떨어지고 판매는 통과한다.</summary>
    [Fact]
    public async Task PrepareAsync_HandlesMissingReferenceData()
    {
        var harness = new Harness();
        var service = new CounsellingService(
            new AntibioticMatchingService(new FakeAwareClassificationRepository()),
            harness.Settings, harness.Locales, harness.Printer, harness.Log);

        var candidates = await service.PrepareAsync(
            new[] { Line("t1", "Amoxicillin 500mg", "J01CA04", "Amoxicillin") });

        Assert.Empty(candidates);
        Assert.Equal(AwareGroupCodes.Unmatched, Assert.Single(harness.Log.Entries).AwareGroup);
    }

    // ── 인쇄 / 로그 ──────────────────────────────────────────────────────────

    [Fact]
    public async Task PrintAsync_PrintsAndLogsSuccess()
    {
        var harness = new Harness();
        var service = harness.Build();

        var candidate = Assert.Single(await service.PrepareAsync(
            new[] { Line("t1", "Amoxicillin 500mg", "J01CA04", "Amoxicillin") }));

        var result = await service.PrintAsync(candidate);

        Assert.True(result.IsSuccess);
        Assert.Single(harness.Printer.Printed);

        var entry = Assert.Single(harness.Log.Entries);
        Assert.True(entry.Printed);
        Assert.Null(entry.SkipReason);
        Assert.Equal("t1", entry.TransactionId);
        Assert.Equal("WHO AWaRe 2025", entry.SourceVersion);
    }

    /// <summary>
    /// 수용 기준: 프린터가 없어도 판매는 그대로다.
    /// 여기서는 인쇄 실패가 예외로 새지 않고 결과값으로만 돌아오는지 확인한다.
    /// </summary>
    [Fact]
    public async Task PrintAsync_ReportsFailureWithoutThrowing()
    {
        var harness = new Harness();
        harness.Printer.ShouldFail = true;
        var service = harness.Build();

        var candidate = Assert.Single(await service.PrepareAsync(
            new[] { Line("t1", "Amoxicillin 500mg", "J01CA04", "Amoxicillin") }));

        var result = await service.PrintAsync(candidate);

        Assert.False(result.IsSuccess);

        var entry = Assert.Single(harness.Log.Entries);
        Assert.False(entry.Printed);
        Assert.Equal(CounsellingService.SkipReasonPrintFailed, entry.SkipReason);
    }

    /// <summary>프린터 어댑터가 예외를 던져도 밖으로 새지 않는다.</summary>
    [Fact]
    public async Task PrintAsync_SwallowsPrinterException()
    {
        var harness = new Harness();
        harness.Printer.ShouldThrow = true;
        var service = harness.Build();

        var candidate = Assert.Single(await service.PrepareAsync(
            new[] { Line("t1", "Amoxicillin 500mg", "J01CA04", "Amoxicillin") }));

        var result = await service.PrintAsync(candidate);

        Assert.False(result.IsSuccess);
    }

    /// <summary>로그 저장이 실패해도 인쇄 결과를 되돌리지 않는다.</summary>
    [Fact]
    public async Task PrintAsync_SucceedsEvenWhenLoggingFails()
    {
        var harness = new Harness();
        var service = harness.Build();

        var candidate = Assert.Single(await service.PrepareAsync(
            new[] { Line("t1", "Amoxicillin 500mg", "J01CA04", "Amoxicillin") }));

        harness.Log.ShouldThrow = true;

        var result = await service.PrintAsync(candidate);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task LogSkipAsync_RecordsPharmacistDecision()
    {
        var harness = new Harness();
        harness.Settings.Settings.PrintMode = CounsellingPrintMode.Ask;
        var service = harness.Build();

        var candidate = Assert.Single(await service.PrepareAsync(
            new[] { Line("t1", "Amoxicillin 500mg", "J01CA04", "Amoxicillin") }));

        await service.LogSkipAsync(candidate, CounsellingService.SkipReasonPharmacist);

        var entry = Assert.Single(harness.Log.Entries);
        Assert.False(entry.Printed);
        Assert.Equal(CounsellingService.SkipReasonPharmacist, entry.SkipReason);
        Assert.Empty(harness.Printer.Printed);
    }

    // ── 로케일 ───────────────────────────────────────────────────────────────

    /// <summary>
    /// 미검수 로케일이면 로그의 locale도 "en"으로 남긴다.
    /// 실제로 영어만 인쇄됐는데 로그에 km-KH로 남으면 지표가 사실과 어긋난다.
    /// </summary>
    [Fact]
    public async Task PrepareAsync_LogsEnglishLocaleWhenTranslationIsNotApproved()
    {
        var harness = new Harness();
        harness.Locales.Locale = new CounsellingLocale(
            "km-KH", "ភាសាខ្មែរ", "Khmer", LocaleRenderMode.Raster,
            "pending", null, "1.0.0",
            new Dictionary<string, string> { [CounsellingStringKeys.LabelDose] = "កម្រិតថ្នាំ" });

        var service = harness.Build();

        var candidate = Assert.Single(await service.PrepareAsync(
            new[] { Line("t1", "Amoxicillin 500mg", "J01CA04", "Amoxicillin") }));

        Assert.Equal("en", candidate.LocaleCode);
        Assert.DoesNotContain("កម្រិតថ្នាំ", candidate.Document.ToPlainText());
    }

    [Fact]
    public async Task PrepareAsync_LogsLocaleCodeWhenTranslationIsApproved()
    {
        var harness = new Harness();
        harness.Locales.Locale = new CounsellingLocale(
            "km-KH", "ភាសាខ្មែរ", "Khmer", LocaleRenderMode.Raster,
            "approved", "reviewer", "1.0.0",
            new Dictionary<string, string> { [CounsellingStringKeys.LabelDose] = "កម្រិតថ្នាំ" });

        var service = harness.Build();

        var candidate = Assert.Single(await service.PrepareAsync(
            new[] { Line("t1", "Amoxicillin 500mg", "J01CA04", "Amoxicillin") }));

        Assert.Equal("km-KH", candidate.LocaleCode);
        Assert.Contains("កម្រិតថ្នាំ", candidate.Document.ToPlainText());
    }

    [Fact]
    public async Task PrepareAsync_UsesCompactFormatWhenConfigured()
    {
        var harness = new Harness();
        harness.Settings.Settings.SheetFormat = CounsellingSheetFormat.Compact;

        var service = harness.Build();

        var candidate = Assert.Single(await service.PrepareAsync(
            new[] { Line("t1", "Amoxicillin 500mg", "J01CA04", "Amoxicillin") }));

        Assert.DoesNotContain("Pharmacist :", candidate.Document.ToPlainText());
    }
}
