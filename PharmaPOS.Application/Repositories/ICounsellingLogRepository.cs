using PharmaPOS.Application.Counselling;
using PharmaPOS.Domain.Entities;

namespace PharmaPOS.Application.Repositories;

/// <summary>
/// 복약안내 출력 이력 저장 및 지표 집계.
/// </summary>
public interface ICounsellingLogRepository
{
    Task AddAsync(CounsellingLogEntry entry);

    /// <summary>지정한 기간(Unix epoch 밀리초)의 지표를 집계한다.</summary>
    Task<CounsellingMetrics> GetMetricsAsync(long fromUtcMillis, long toUtcMillis);
}
