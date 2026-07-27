namespace PharmaPOS.Application.Repositories;

/// <summary>
/// DB 백업/복원, CSV/Excel 내보내기를 담당하는 인터페이스. (Screen SCR-BACKUP-018)
/// </summary>
public interface IBackupRepository
{
    /// <summary>내보내기 가능한 주요 테이블 이름 목록을 반환한다.</summary>
    IReadOnlyList<string> GetExportableTableNames();

    /// <summary>
    /// SQLite 전용 백업 API로 현재 DB의 일관된 스냅샷을 destinationDbPath에 만든다.
    /// WAL 모드에서도 안전하다 (단순 파일 복사와 다름).
    /// </summary>
    Task BackupDatabaseAsync(string destinationDbPath);

    /// <summary>지정된 테이블을 CSV 파일로 내보낸다.</summary>
    Task ExportTableToCsvAsync(string tableName, string destinationFilePath);

    /// <summary>지정된 테이블을 헤더+틀고정이 적용된 Excel(.xlsx) 파일로 내보낸다.</summary>
    Task ExportTableToExcelAsync(string tableName, string destinationFilePath);

    /// <summary>선택한 파일이 유효한 SQLite DB 파일인지 확인한다.</summary>
    Task<bool> IsValidSqliteFileAsync(string filePath);

    /// <summary>
    /// 현재 DB를 sourceDbPath의 내용으로 교체한다.
    /// 호출하는 쪽이 이미 "현재 DB 백업"을 먼저 수행했다고 가정한다.
    /// </summary>
    Task RestoreDatabaseAsync(string sourceDbPath);
}