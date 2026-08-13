namespace PharmaPOS.Application.Inventory;

/// <summary>
/// F-11 백업/내보내기/복원 로직을 담당하는 인터페이스. (Screen SCR-BACKUP-018)
/// </summary>
public interface IBackupService
{
    /// <summary>지정된 폴더에 타임스탬프가 붙은 DB 백업 파일을 만든다.</summary>
    Task<BackupResult> CreateDatabaseBackupAsync(string? backupLocation);

    /// <summary>
    /// 고른 데이터 묶음을 각각 파일 하나로 내보낸다(CSV 또는 Excel).
    /// 하나도 고르지 않았으면 실패로 돌려준다 — 아무 일도 일어나지 않은 것과
    /// "성공"을 구분해야 한다.
    /// </summary>
    Task<BackupResult> ExportDatasetsAsync(
        string? backupLocation, IReadOnlyList<ExportDataset> datasets, bool isCsvFormat);

    /// <summary>
    /// 복원을 수행한다. 호출 전에 이미 사용자 확인(Confirm)을 받았다고 가정한다.
    /// 내부적으로 현재 DB를 먼저 자동 백업한 뒤 복원을 진행한다.
    /// </summary>
    Task<BackupResult> RestoreDatabaseAsync(string? backupFilePath, string autoBackupFolder);
}
