using PharmaPOS.Domain.Entities;
using PharmaPOS.Domain.Enums;

namespace PharmaPOS.Application.Repositories;

/// <summary>
/// 초기 재고 임포트 이력에 대한 데이터 접근.
/// </summary>
public interface IImportHistoryRepository
{
    /// <summary>같은 종류로 같은 내용의 파일을 이미 넣었는지.</summary>
    Task<bool> ExistsAsync(ImportType importType, string fileHash);

    /// <summary>임포트 1회를 기록한다.</summary>
    Task AddAsync(ImportHistoryEntry entry);
}
