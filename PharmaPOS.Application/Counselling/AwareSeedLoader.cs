using System.Security.Cryptography;
using PharmaPOS.Application.Repositories;
using PharmaPOS.Application.Settings;
using PharmaPOS.Domain.Entities;

namespace PharmaPOS.Application.Counselling;

/// <summary>
/// IAwareSeedLoader의 구현체.
///
/// 시드 파일은 여러 후보 경로에서 찾는다. 먼저 발견된 파일 하나만 쓴다.
/// 보통 이렇게 넘긴다:
///   1) %APPDATA%\PharmaPOS\seeds\aware_2025.csv   ← 현장에서 교체 가능 (재빌드 불필요)
///   2) (설치 폴더)\seeds\aware_2025.csv           ← 기본 동봉본
/// AWaRe 분류가 개정되면 1번 자리에 새 파일만 놓으면 다음 실행에서 반영된다.
/// </summary>
public class AwareSeedLoader : IAwareSeedLoader
{
    private readonly IAwareClassificationRepository _awareRepository;
    private readonly IAppSettingRepository _settingRepository;
    private readonly IReadOnlyList<string> _candidatePaths;

    public AwareSeedLoader(
        IAwareClassificationRepository awareRepository,
        IAppSettingRepository settingRepository,
        IReadOnlyList<string> candidatePaths)
    {
        _awareRepository = awareRepository;
        _settingRepository = settingRepository;
        _candidatePaths = candidatePaths;
    }

    public async Task<AwareSeedLoadResult> LoadIfChangedAsync()
    {
        string? seedPath = null;

        foreach (var path in _candidatePaths)
        {
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
            {
                seedPath = path;
                break;
            }
        }

        if (seedPath is null)
        {
            return AwareSeedLoadResult.Failure(
                "The AWaRe reference file was not found. Antibiotic counselling sheets cannot be printed until it is installed.");
        }

        string content;
        string signature;

        try
        {
            var bytes = await File.ReadAllBytesAsync(seedPath);

            // 파일 내용 + 정규화 규칙 버전을 함께 지문으로 삼는다.
            // 규칙이 바뀌면 파일이 그대로여도 저장된 normalized_name이 낡은 것이므로
            // 다시 적재해야 한다.
            signature = Convert.ToHexString(SHA256.HashData(bytes))
                        + "|" + AntibioticNameNormalizer.RuleVersion;

            content = System.Text.Encoding.UTF8.GetString(bytes);
        }
        catch (Exception)
        {
            return AwareSeedLoadResult.Failure("The AWaRe reference file could not be read.");
        }

        var storedSignature = await _settingRepository.GetAsync(AppSettingKeys.AwareSeedSignature);

        if (string.Equals(storedSignature, signature, StringComparison.Ordinal))
        {
            var existingCount = await _awareRepository.CountAsync();

            // 지문은 같은데 테이블이 비어 있다면(DB만 초기화된 경우) 다시 적재한다.
            if (existingCount > 0)
            {
                var storedVersion = await _settingRepository.GetAsync(AppSettingKeys.AwareSourceVersion);
                return AwareSeedLoadResult.AlreadyUpToDate(existingCount, storedVersion);
            }
        }

        var parseResult = AwareSeedCsvParser.Parse(content);

        if (!parseResult.IsSuccess)
        {
            return AwareSeedLoadResult.Failure(parseResult.Message!);
        }

        if (parseResult.Rows.Count == 0)
        {
            return AwareSeedLoadResult.Failure("The AWaRe reference file contains no usable rows.");
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        var classifications = parseResult.Rows.Select(row => new AwareClassification
        {
            AwareId = Guid.NewGuid().ToString(),
            AtcCode = row.AtcCode,
            AntibioticName = row.AntibioticName,
            NormalizedName = AntibioticNameNormalizer.Normalize(row.AntibioticName),
            AwareGroup = row.AwareGroup,
            IsSystemic = row.IsSystemic,
            SourceVersion = row.SourceVersion,
            UpdatedAt = now
        }).ToList();

        try
        {
            await _awareRepository.ReplaceAllAsync(classifications);
        }
        catch (Exception)
        {
            return AwareSeedLoadResult.Failure("The AWaRe reference data could not be saved.");
        }

        // 파일 안에 출처 표기가 섞여 있으면 가장 많이 쓰인 값을 대표로 남긴다.
        var sourceVersion = classifications
            .GroupBy(c => c.SourceVersion, StringComparer.Ordinal)
            .OrderByDescending(g => g.Count())
            .First()
            .Key;

        await _settingRepository.SetAsync(AppSettingKeys.AwareSourceVersion, sourceVersion);
        await _settingRepository.SetAsync(AppSettingKeys.AwareSeedSignature, signature);

        return AwareSeedLoadResult.Loaded(
            classifications.Count, parseResult.Errors.Count, parseResult.Errors, sourceVersion);
    }
}
