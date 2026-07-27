using PharmaPOS.Application.Repositories;

namespace PharmaPOS.Application.Inventory;

/// <summary>
/// IBackupService의 구현체.
/// Screen SCR-BACKUP-018, 4절 흐름을 그대로 코드로 옮긴 것이다.
/// </summary>
public class BackupService : IBackupService
{
    private readonly IBackupRepository _backupRepository;

    public BackupService(IBackupRepository backupRepository)
    {
        _backupRepository = backupRepository;
    }

    public IReadOnlyList<string> GetExportableTableNames() => _backupRepository.GetExportableTableNames();

    public async Task<BackupResult> CreateDatabaseBackupAsync(string? backupLocation)
    {
        if (string.IsNullOrWhiteSpace(backupLocation))
        {
            return BackupResult.Failure("Please select a backup location.");
        }

        if (!Directory.Exists(backupLocation))
        {
            return BackupResult.Failure("Backup location is not available.");
        }

        var fileName = $"pharmapos_backup_{DateTime.Now:yyyyMMdd_HHmmss}.db";
        var destinationPath = Path.Combine(backupLocation, fileName);

        try
        {
            await _backupRepository.BackupDatabaseAsync(destinationPath);
        }
        catch (UnauthorizedAccessException)
        {
            return BackupResult.Failure("Cannot access the selected location.");
        }
        catch (Exception)
        {
            return BackupResult.Failure("Database backup failed.");
        }

        return BackupResult.Success($"Backup created: {fileName}");
    }

    public async Task<BackupResult> ExportDataAsync(string? backupLocation, string? exportType, bool isCsvFormat)
    {
        if (string.IsNullOrWhiteSpace(backupLocation))
        {
            return BackupResult.Failure("Please select a backup location.");
        }

        if (string.IsNullOrWhiteSpace(exportType))
        {
            return BackupResult.Failure("Please select data to export.");
        }

        if (!Directory.Exists(backupLocation))
        {
            return BackupResult.Failure("Backup location is not available.");
        }

        var tablesToExport = exportType == "All"
            ? GetExportableTableNames()
            : new[] { exportType };

        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var extension = isCsvFormat ? "csv" : "xlsx";

        try
        {
            foreach (var table in tablesToExport)
            {
                var fileName = $"{table}_{timestamp}.{extension}";
                var destinationPath = Path.Combine(backupLocation, fileName);

                if (isCsvFormat)
                {
                    await _backupRepository.ExportTableToCsvAsync(table, destinationPath);
                }
                else
                {
                    await _backupRepository.ExportTableToExcelAsync(table, destinationPath);
                }
            }
        }
        catch (Exception)
        {
            return BackupResult.Failure(isCsvFormat ? "CSV export failed." : "Excel export failed.");
        }

        return BackupResult.Success($"{tablesToExport.Count} table(s) exported successfully.");
    }

    public async Task<BackupResult> RestoreDatabaseAsync(string? backupFilePath, string autoBackupFolder)
    {
        if (string.IsNullOrWhiteSpace(backupFilePath))
        {
            return BackupResult.Failure("Please select a backup file.");
        }

        if (!File.Exists(backupFilePath))
        {
            return BackupResult.Failure("Invalid backup file.");
        }

        var isValid = await _backupRepository.IsValidSqliteFileAsync(backupFilePath);
        if (!isValid)
        {
            return BackupResult.Failure("Invalid backup file.");
        }

        // 복원 처리 원칙(Screen §5.1절): 복원 전 반드시 기존 DB를 자동 백업한다.
        try
        {
            var autoBackupFileName = $"pharmapos_pre_restore_backup_{DateTime.Now:yyyyMMdd_HHmmss}.db";
            var autoBackupPath = Path.Combine(autoBackupFolder, autoBackupFileName);
            await _backupRepository.BackupDatabaseAsync(autoBackupPath);
        }
        catch (Exception)
        {
            return BackupResult.Failure("Database restore failed.");
        }

        try
        {
            await _backupRepository.RestoreDatabaseAsync(backupFilePath);
        }
        catch (IOException)
        {
            return BackupResult.Failure("Database is currently in use. Please try again.");
        }
        catch (Exception)
        {
            return BackupResult.Failure("Database restore failed.");
        }

        return BackupResult.Success();
    }
}