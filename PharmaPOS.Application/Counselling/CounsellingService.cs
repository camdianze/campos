using PharmaPOS.Application.Inventory;
using PharmaPOS.Application.Repositories;
using PharmaPOS.Domain.Entities;
using PharmaPOS.Domain.Enums;

namespace PharmaPOS.Application.Counselling;

/// <summary>
/// ICounsellingService의 구현체.
///
/// 전체 흐름에서 지키는 원칙 하나: 무슨 일이 있어도 판매를 방해하지 않는다.
/// 매칭 실패, 참조 데이터 없음, 로케일 파일 손상, 프린터 없음, 로그 저장 실패 —
/// 전부 조용히 처리하고 넘어간다. 판매는 이 메서드들이 호출되기 전에 이미 끝났다.
/// </summary>
public class CounsellingService : ICounsellingService
{
    /// <summary>로그의 skip_reason에 쓰는 값. 집계할 수 있도록 문구를 고정한다.</summary>
    public const string SkipReasonUnmatched = "unmatched";
    public const string SkipReasonDisabled = "setting_never";
    public const string SkipReasonPharmacist = "pharmacist_skipped";
    public const string SkipReasonPrintFailed = "print_failed";

    private const string EnglishOnlyLocaleCode = "en";

    private readonly IAntibioticMatchingService _matchingService;
    private readonly ICounsellingSettingsService _settingsService;
    private readonly ICounsellingLocaleProvider _localeProvider;
    private readonly ICounsellingSheetPrintingService _printingService;
    private readonly ICounsellingSheetFileWriter _fileWriter;
    private readonly ICounsellingLogRepository _logRepository;

    public CounsellingService(
        IAntibioticMatchingService matchingService,
        ICounsellingSettingsService settingsService,
        ICounsellingLocaleProvider localeProvider,
        ICounsellingSheetPrintingService printingService,
        ICounsellingSheetFileWriter fileWriter,
        ICounsellingLogRepository logRepository)
    {
        _matchingService = matchingService;
        _settingsService = settingsService;
        _localeProvider = localeProvider;
        _printingService = printingService;
        _fileWriter = fileWriter;
        _logRepository = logRepository;
    }

    public async Task<IReadOnlyList<CounsellingCandidate>> PrepareAsync(
        IReadOnlyList<ConfirmedSaleLine> confirmedLines)
    {
        var candidates = new List<CounsellingCandidate>();

        try
        {
            var settings = await _settingsService.GetAsync();
            var locale = await _localeProvider.GetLocaleAsync(settings.LocaleCode);

            // 검수되지 않은 로케일은 현지어가 잠기므로, 로그에도 영어 단독으로 남긴다.
            var loggedLocale = locale.IsApproved && !string.IsNullOrWhiteSpace(locale.LocaleCode)
                ? locale.LocaleCode
                : EnglishOnlyLocaleCode;

            foreach (var confirmed in confirmedLines)
            {
                var line = confirmed.Line;
                var match = await _matchingService.MatchAsync(line.AtcCode, line.GenericName);

                if (match.Outcome == AntibioticMatchOutcome.ExcludedTopical)
                {
                    // 국소 제제는 안내 대상이 아니다. 로그에 남기면 항생제 판매 건수와
                    // ACCESS 비중이 국소 제제로 오염돼 지표가 틀어진다.
                    continue;
                }

                if (match.Outcome == AntibioticMatchOutcome.Unmatched)
                {
                    // 판매는 그대로 통과시키고 기록만 남긴다.
                    // 이 로그가 쌓이면 시드 데이터에서 빠진 항목을 찾을 수 있다.
                    await LogAsync(
                        confirmed.TransactionId, line.ProductId, line.AtcCode,
                        AwareGroupCodes.Unmatched, printed: false,
                        SkipReasonUnmatched, loggedLocale, sourceVersion: null);
                    continue;
                }

                var classification = match.Classification!;

                if (settings.PrintMode == CounsellingPrintMode.Never)
                {
                    await LogAsync(
                        confirmed.TransactionId, line.ProductId, classification.AtcCode,
                        AwareGroupCodes.ToCode(classification.AwareGroup), printed: false,
                        SkipReasonDisabled, loggedLocale, classification.SourceVersion);
                    continue;
                }

                var document = CounsellingSheetRenderer.Render(new CounsellingSheetRequest
                {
                    ProductName = line.ProductName,
                    GenericName = line.GenericName ?? classification.AntibioticName,
                    AtcCode = classification.AtcCode ?? line.AtcCode,
                    AwareGroup = classification.AwareGroup,
                    SourceVersion = classification.SourceVersion,
                    Locale = locale,
                    Format = settings.SheetFormat,
                    QrUrl = settings.QrUrl
                });

                candidates.Add(new CounsellingCandidate
                {
                    TransactionId = confirmed.TransactionId,
                    ProductId = line.ProductId,
                    ProductName = line.ProductName,
                    AtcCode = classification.AtcCode ?? line.AtcCode,
                    AwareGroup = classification.AwareGroup,
                    SourceVersion = classification.SourceVersion,
                    LocaleCode = loggedLocale,
                    Document = document,
                    Output = settings.Output,
                    FileOutputFolder = settings.FileOutputFolder,
                    RequiresPrompt = settings.PrintMode == CounsellingPrintMode.Ask
                });
            }
        }
        catch (Exception)
        {
            // 준비 단계가 통째로 실패해도 판매는 이미 끝났다. 조용히 넘어간다.
            return candidates;
        }

        return candidates;
    }

    public async Task<CounsellingPrintResult> PrintAsync(CounsellingCandidate candidate)
    {
        CounsellingPrintResult result;

        try
        {
            // 파일 저장은 프린터 없이 용지 내용을 확인하기 위한 경로다.
            // 로그에는 둘 다 "출력됨"으로 남는다 — 약사에게 안내가 전달됐는지가 지표의 기준이고,
            // 어느 장치로 나갔는지는 지표의 관심사가 아니다.
            result = candidate.Output == CounsellingOutput.File
                ? await _fileWriter.WriteAsync(
                    candidate.Document, candidate.FileOutputFolder, candidate.TransactionId)
                : await _printingService.PrintAsync(candidate.Document);
        }
        catch (Exception)
        {
            result = CounsellingPrintResult.Failure("The counselling sheet could not be printed.");
        }

        await LogAsync(
            candidate.TransactionId,
            candidate.ProductId,
            candidate.AtcCode,
            AwareGroupCodes.ToCode(candidate.AwareGroup),
            result.IsSuccess,
            result.IsSuccess ? null : SkipReasonPrintFailed,
            candidate.LocaleCode,
            candidate.SourceVersion);

        return result;
    }

    public async Task LogSkipAsync(CounsellingCandidate candidate, string skipReason)
    {
        await LogAsync(
            candidate.TransactionId,
            candidate.ProductId,
            candidate.AtcCode,
            AwareGroupCodes.ToCode(candidate.AwareGroup),
            printed: false,
            skipReason,
            candidate.LocaleCode,
            candidate.SourceVersion);
    }

    private async Task LogAsync(
        string transactionId,
        string productId,
        string? atcCode,
        string awareGroup,
        bool printed,
        string? skipReason,
        string locale,
        string? sourceVersion)
    {
        try
        {
            await _logRepository.AddAsync(new CounsellingLogEntry
            {
                LogId = Guid.NewGuid().ToString(),
                TransactionId = transactionId,
                ProductId = productId,
                AtcCode = atcCode,
                AwareGroup = awareGroup,
                Printed = printed,
                SkipReason = skipReason,
                Locale = locale,
                SourceVersion = sourceVersion,
                CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            });
        }
        catch (Exception)
        {
            // 지표 기록에 실패해도 판매나 인쇄를 되돌리지 않는다.
        }
    }
}
