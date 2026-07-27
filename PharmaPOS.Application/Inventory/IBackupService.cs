namespace PharmaPOS.Application.Inventory;

/// <summary>
/// F-11 백업/내보내기/복원 로직을 담당하는 인터페이스. (Screen SCR-BACKUP-018)
/// </summary>
public interface IBackupService
{
    /// <summary>내보내기 가능한 테이블 이름 목록. "All"은 화면(ViewModel)에서 별도 옵션으로 추가한다.</summary>
    IReadOnlyList<string> GetExportableTableNames();

    /// <summary>지정된 폴더에 타임스탬프가 붙은 DB 백업 파일을 만든다.</summary>
    Task<BackupResult> CreateDatabaseBackupAsync(string? backupLocation);

    /// <summary>
    /// exportType이 null이면(=All) 모든 테이블을, 아니면 해당 테이블 하나만
    /// 지정된 형식(csv 여부)으로 내보낸다.
    /// </summary>
    Task<BackupResult> ExportDataAsync(string? backupLocation, string? exportType, bool isCsvFormat);

    /// <summary>
    /// 복원을 수행한다. 호출 전에 이미 사용자 확인(Confirm)을 받았다고 가정한다.
    /// 내부적으로 현재 DB를 먼저 자동 백업한 뒤 복원을 진행한다.
    /// </summary>
    Task<BackupResult> RestoreDatabaseAsync(string? backupFilePath, string autoBackupFolder);
}