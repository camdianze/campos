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

        // 같은 상품이 여러 줄로 갈라져 들어와도 안내문은 한 장이다. 장바구니는
        // (상품 + 배치 + 박스/낱개)로 줄을 나누므로, 재고가 두 배치에 걸쳐 있거나
        // 박스와 낱개를 함께 팔면 같은 항생제가 두 줄이 된다. 그때 나오는 두 장은
        // 내용이 완전히 같다 — 안내문에는 배치도 수량도 들어가지 않기 때문이다.
        // 상품별로 처음 만든 후보에 나머지 줄의 거래 ID만 붙여 둔다.
        var candidateIndexByProduct = new Dictionary<string, int>();
        var linkedTransactionIds = new List<List<string>>();

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

                // 이미 이 상품으로 만든 후보가 있으면 줄만 매달고 끝낸다.
                // 이 줄의 로그는 인쇄(또는 건너뜀) 결과가 나온 뒤 후보가 대신 남긴다.
                // never로 꺼 둔 경우에는 후보 자체가 안 만들어져 이 분기에 걸리지 않고,
                // 아래에서 종전처럼 줄마다 로그가 남는다.
                if (candidateIndexByProduct.TryGetValue(line.ProductId, out var existingIndex))
                {
                    linkedTransactionIds[existingIndex].Add(confirmed.TransactionId);
                    continue;
                }

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

                // 후보와 같은 자리의 목록을 만들어 둔다. 여기 담기는 List는 후보가
                // 들고 있는 것과 같은 객체라, 뒤에 같은 상품이 또 나오면 그대로 붙는다.
                var transactionIds = new List<string> { confirmed.TransactionId };
                candidateIndexByProduct[line.ProductId] = candidates.Count;
                linkedTransactionIds.Add(transactionIds);

                candidates.Add(new CounsellingCandidate
                {
                    TransactionId = confirmed.TransactionId,
                    TransactionIds = transactionIds,
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

        // 합쳐진 줄에도 같은 결과를 남긴다. 종이는 한 장이지만 그 상품을 산 손님은
        // 안내를 받았고, 지표가 세는 것은 장수가 아니라 "안내가 전달된 판매"다.
        foreach (var transactionId in candidate.TransactionIds)
        {
            await LogAsync(
                transactionId,
                candidate.ProductId,
                candidate.AtcCode,
                AwareGroupCodes.ToCode(candidate.AwareGroup),
                result.IsSuccess,
                result.IsSuccess ? null : SkipReasonPrintFailed,
                candidate.LocaleCode,
                candidate.SourceVersion);
        }

        return result;
    }

    public async Task LogSkipAsync(CounsellingCandidate candidate, string skipReason)
    {
        foreach (var transactionId in candidate.TransactionIds)
        {
            await LogAsync(
                transactionId,
                candidate.ProductId,
                candidate.AtcCode,
                AwareGroupCodes.ToCode(candidate.AwareGroup),
                printed: false,
                skipReason,
                candidate.LocaleCode,
                candidate.SourceVersion);
        }
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
