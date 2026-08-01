using PharmaPOS.Application.Counselling;
using PharmaPOS.Application.Repositories;
using PharmaPOS.Domain.Entities;
using PharmaPOS.Domain.Enums;

namespace PharmaPOS.Tests.Counselling;

/// <summary>
/// 매칭 로직만 떼어 보기 위한 메모리 구현체.
/// 실제 리포지터리와 같은 규칙(정규화된 값으로 조회)을 따른다.
/// </summary>
public class FakeAwareClassificationRepository : IAwareClassificationRepository
{
    private readonly List<AwareClassification> _rows = new();

    /// <summary>true면 모든 조회가 예외를 던진다. DB 장애 상황 재현용.</summary>
    public bool ThrowOnQuery { get; set; }

    public FakeAwareClassificationRepository Add(
        string? atcCode, string antibioticName, AwareGroup group, bool isSystemic = true)
    {
        _rows.Add(new AwareClassification
        {
            AwareId = Guid.NewGuid().ToString(),
            AtcCode = atcCode is null ? null : AntibioticNameNormalizer.NormalizeAtcCode(atcCode),
            AntibioticName = antibioticName,
            NormalizedName = AntibioticNameNormalizer.Normalize(antibioticName),
            AwareGroup = group,
            IsSystemic = isSystemic,
            SourceVersion = "WHO AWaRe 2025",
            UpdatedAt = 0
        });

        return this;
    }

    public Task ReplaceAllAsync(IReadOnlyList<AwareClassification> classifications)
    {
        _rows.Clear();
        _rows.AddRange(classifications);
        return Task.CompletedTask;
    }

    public Task<AwareClassification?> FindByAtcCodeAsync(string normalizedAtcCode)
    {
        if (ThrowOnQuery)
        {
            throw new InvalidOperationException("database unavailable");
        }

        return Task.FromResult(_rows.FirstOrDefault(r => r.AtcCode == normalizedAtcCode));
    }

    public Task<AwareClassification?> FindByNormalizedNameAsync(string normalizedName)
    {
        if (ThrowOnQuery)
        {
            throw new InvalidOperationException("database unavailable");
        }

        return Task.FromResult(_rows.FirstOrDefault(r => r.NormalizedName == normalizedName));
    }

    public Task<int> CountAsync() => Task.FromResult(_rows.Count);
}
