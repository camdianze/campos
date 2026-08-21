using PharmaPOS.Application.Repositories;

namespace PharmaPOS.Tests.Receipts;

/// <summary>
/// 메모리 위의 App_Setting. 설정 서비스의 규칙만 확인하려는 것이라
/// SQLite까지 세우지 않는다.
/// </summary>
public class FakeAppSettingRepository : IAppSettingRepository
{
    private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal);

    /// <summary>키별로 마지막에 기록된 (종류, 변경자).</summary>
    public Dictionary<string, (string? Type, string? UpdatedBy)> Audit { get; } = new(StringComparer.Ordinal);

    /// <summary>읽기 호출 횟수. 캐시가 실제로 DB를 덜 때리는지 확인하는 데 쓴다.</summary>
    public int ReadCount { get; private set; }

    /// <summary>true면 모든 읽기가 터진다. 설정을 못 읽을 때의 동작을 보려는 것이다.</summary>
    public bool FailReads { get; set; }

    /// <summary>true면 모든 쓰기가 터진다.</summary>
    public bool FailWrites { get; set; }

    public void Seed(string key, string value) => _values[key] = value;

    public Task<string?> GetAsync(string key) =>
        Task.FromResult(_values.TryGetValue(key, out var value) ? value : null);

    public Task<IReadOnlyDictionary<string, string>> GetManyAsync(IReadOnlyList<string> keys)
    {
        ReadCount++;

        if (FailReads)
        {
            throw new InvalidOperationException("read failed");
        }

        IReadOnlyDictionary<string, string> found = keys
            .Where(_values.ContainsKey)
            .ToDictionary(key => key, key => _values[key], StringComparer.Ordinal);

        return Task.FromResult(found);
    }

    public Task SetAsync(string key, string value, string? valueType = null, string? updatedBy = null)
    {
        if (FailWrites)
        {
            throw new InvalidOperationException("write failed");
        }

        _values[key] = value;
        Audit[key] = (valueType, updatedBy);

        return Task.CompletedTask;
    }
}
