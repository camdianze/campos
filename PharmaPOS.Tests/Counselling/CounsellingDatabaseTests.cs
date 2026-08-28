using Microsoft.Data.Sqlite;
using PharmaPOS.Application.Counselling;
using PharmaPOS.Application.Settings;
using PharmaPOS.DataAccess.Database;
using PharmaPOS.DataAccess.Repositories;
using PharmaPOS.Domain.Entities;
using PharmaPOS.Domain.Enums;

namespace PharmaPOS.Tests.Counselling;

/// <summary>
/// 실제 SQLite 파일에 대고 도는 통합 테스트.
/// 스키마, 마이그레이션, 리포지터리 매핑, 시드 적재를 한 번에 확인한다.
/// </summary>
public class CounsellingDatabaseTests : IDisposable
{
    private readonly string _directory;
    private readonly string _databasePath;
    private readonly SqliteConnectionFactory _connectionFactory;

    public CounsellingDatabaseTests()
    {
        _directory = Path.Combine(
            Path.GetTempPath(), "pharmapos-db-tests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_directory);

        _databasePath = Path.Combine(_directory, "test.db");
        _connectionFactory = new SqliteConnectionFactory(_databasePath);
    }

    public void Dispose()
    {
        // SQLite 연결 풀이 파일을 붙들고 있어 바로 지우면 실패할 수 있다.
        SqliteConnection.ClearAllPools();

        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
            // 임시 폴더가 남는 것은 테스트 결과에 영향을 주지 않는다.
        }
    }

    private void InitializeDatabase() => new DatabaseInitializer(_connectionFactory).Initialize();

    private void ExecuteSql(string sql)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private List<string> GetColumns(string tableName)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info({tableName});";

        using var reader = command.ExecuteReader();
        var columns = new List<string>();

        while (reader.Read())
        {
            columns.Add(reader.GetString(reader.GetOrdinal("name")));
        }

        return columns;
    }

    // ── 스키마 / 마이그레이션 ────────────────────────────────────────────────

    [Fact]
    public void Initialize_CreatesCounsellingTables()
    {
        InitializeDatabase();

        Assert.NotEmpty(GetColumns("Aware_Classification"));
        Assert.NotEmpty(GetColumns("Counselling_Log"));
        Assert.NotEmpty(GetColumns("App_Setting"));
    }

    /// <summary>
    /// 이미 배포돼 돌고 있는 DB에도 새 컬럼이 붙어야 한다.
    /// CREATE TABLE IF NOT EXISTS는 기존 테이블을 건드리지 않으므로
    /// ApplyMigrations가 실제로 동작하는지 확인한다.
    /// </summary>
    [Fact]
    public void Initialize_AddsAtcColumnsToExistingProductTable()
    {
        // atc_code / is_combination이 없던 시절의 테이블을 흉내 낸다.
        ExecuteSql("""
            CREATE TABLE Product_Master (
                product_id          TEXT PRIMARY KEY,
                barcode             TEXT,
                internal_barcode    TEXT,
                product_name        TEXT NOT NULL,
                generic_name        TEXT,
                strength            TEXT,
                unit                TEXT NOT NULL,
                manufacturer        TEXT,
                country_of_origin   TEXT,
                cost_price          REAL NOT NULL,
                selling_price       REAL NOT NULL,
                safety_stock_level  INTEGER NOT NULL,
                status              TEXT NOT NULL,
                created_at          INTEGER NOT NULL
            );
            """);

        InitializeDatabase();

        var columns = GetColumns("Product_Master");

        Assert.Contains("atc_code", columns);
        Assert.Contains("is_combination", columns);
    }

    // ── 리포지터리 ───────────────────────────────────────────────────────────

    [Fact]
    public async Task AwareClassificationRepository_RoundTripsRows()
    {
        InitializeDatabase();

        var repository = new AwareClassificationRepository(_connectionFactory);

        await repository.ReplaceAllAsync(new[]
        {
            BuildClassification("J01CA04", "Amoxicillin", AwareGroup.Access),
            // 고유 ATC가 없는 복합제. 이 행이 저장되지 않으면 기능의 핵심이 빠진다.
            BuildClassification(null, "Amoxicillin/clavulanic acid FDC", AwareGroup.NotRecommended)
        });

        Assert.Equal(2, await repository.CountAsync());

        var byAtc = await repository.FindByAtcCodeAsync("J01CA04");
        Assert.Equal(AwareGroup.Access, byAtc!.AwareGroup);
        Assert.True(byAtc.IsSystemic);

        var byName = await repository.FindByNormalizedNameAsync(
            AntibioticNameNormalizer.Normalize("Amoxicillin/clavulanic acid FDC"));
        Assert.Equal(AwareGroup.NotRecommended, byName!.AwareGroup);
        Assert.Null(byName.AtcCode);
    }

    /// <summary>재적재는 기존 데이터를 지우고 새로 넣는다.</summary>
    [Fact]
    public async Task AwareClassificationRepository_ReplaceAllClearsPreviousRows()
    {
        InitializeDatabase();

        var repository = new AwareClassificationRepository(_connectionFactory);

        await repository.ReplaceAllAsync(new[] { BuildClassification("J01CA04", "Amoxicillin", AwareGroup.Access) });
        await repository.ReplaceAllAsync(new[] { BuildClassification("J01FA10", "Azithromycin", AwareGroup.Watch) });

        Assert.Equal(1, await repository.CountAsync());
        Assert.Null(await repository.FindByAtcCodeAsync("J01CA04"));
    }

    [Fact]
    public async Task ProductRepository_RoundTripsAtcFields()
    {
        InitializeDatabase();

        var repository = new ProductRepository(_connectionFactory);

        await repository.InsertAsync(new Product
        {
            ProductId = "p1",
            ProductName = "Amoxi-Clav 625mg",
            GenericName = "Amoxicillin/clavulanic acid",
            Unit = "tablet",
            CostPrice = 1m,
            SellingPrice = 2m,
            SafetyStockLevel = 10,
            Status = EntityStatus.Active,
            CreatedAt = 0,
            AtcCode = "J01CR02",
            IsCombination = true
        });

        var loaded = await repository.GetByIdAsync("p1");

        Assert.Equal("J01CR02", loaded!.AtcCode);
        Assert.True(loaded.IsCombination);
    }

    [Fact]
    public async Task AppSettingRepository_UpsertsValues()
    {
        InitializeDatabase();

        var repository = new AppSettingRepository(_connectionFactory);

        Assert.Null(await repository.GetAsync(AppSettingKeys.CounsellingPrintMode));

        await repository.SetAsync(AppSettingKeys.CounsellingPrintMode, "Ask");
        Assert.Equal("Ask", await repository.GetAsync(AppSettingKeys.CounsellingPrintMode));

        await repository.SetAsync(AppSettingKeys.CounsellingPrintMode, "Always");
        Assert.Equal("Always", await repository.GetAsync(AppSettingKeys.CounsellingPrintMode));
    }

    [Fact]
    public async Task CounsellingLogRepository_AggregatesStewardshipMetrics()
    {
        InitializeDatabase();
        SeedFacilityUserAndProduct();

        var logRepository = new CounsellingLogRepository(_connectionFactory);

        // 판매 4건 중 3건이 항생제, 그중 2건이 ACCESS.
        InsertSaleTransaction("tx-1", 1_000);
        InsertSaleTransaction("tx-2", 1_000);
        InsertSaleTransaction("tx-3", 1_000);
        InsertSaleTransaction("tx-4", 1_000);

        await logRepository.AddAsync(BuildLog("tx-1", AwareGroupCodes.Access, printed: true));
        await logRepository.AddAsync(BuildLog("tx-2", AwareGroupCodes.Access, printed: false, "pharmacist_skipped"));
        await logRepository.AddAsync(BuildLog("tx-3", AwareGroupCodes.Watch, printed: true));
        await logRepository.AddAsync(BuildLog("tx-4", AwareGroupCodes.Unmatched, printed: false, "unmatched"));

        var metrics = await logRepository.GetMetricsAsync(0, 10_000);

        Assert.Equal(4, metrics.TotalSaleLines);
        Assert.Equal(3, metrics.AntibioticSaleLines);
        Assert.Equal(2, metrics.AccessCount);
        Assert.Equal(1, metrics.WatchCount);
        Assert.Equal(1, metrics.UnmatchedCount);
        Assert.Equal(2, metrics.PrintedCount);
        Assert.Equal(1, metrics.SkippedCount);

        // ACCESS 비중은 항생제 건수 기준이다 (2/3). unmatched는 분모에 들어가지 않는다.
        Assert.Equal(2.0 / 3.0, metrics.AccessShare, precision: 5);
    }

    [Fact]
    public async Task CounsellingLogRepository_ExcludesRowsOutsideThePeriod()
    {
        InitializeDatabase();
        SeedFacilityUserAndProduct();

        var logRepository = new CounsellingLogRepository(_connectionFactory);

        InsertSaleTransaction("tx-1", 500);
        await logRepository.AddAsync(BuildLog("tx-1", AwareGroupCodes.Access, printed: true, createdAt: 500));

        var metrics = await logRepository.GetMetricsAsync(1_000, 2_000);

        Assert.Equal(0, metrics.TotalSaleLines);
        Assert.Equal(0, metrics.AntibioticSaleLines);
    }

    // ── 시드 적재 ────────────────────────────────────────────────────────────

    [Fact]
    public async Task AwareSeedLoader_LoadsFileThenSkipsUnchangedReload()
    {
        InitializeDatabase();

        var seedPath = Path.Combine(_directory, "aware_2025.csv");
        await File.WriteAllTextAsync(seedPath, string.Join("\n",
            "atc_code,antibiotic_name,aware_group,is_systemic,source_version",
            "J01CA04,Amoxicillin,ACCESS,true,WHO AWaRe 2025",
            ",Amoxicillin/clavulanic acid FDC,NOT_RECOMMENDED,true,WHO AWaRe 2025"));

        var awareRepository = new AwareClassificationRepository(_connectionFactory);
        var settingRepository = new AppSettingRepository(_connectionFactory);
        var loader = new AwareSeedLoader(awareRepository, settingRepository, new[] { seedPath });

        var first = await loader.LoadIfChangedAsync();

        Assert.True(first.IsSuccess);
        Assert.False(first.WasAlreadyUpToDate);
        Assert.Equal(2, first.LoadedCount);
        Assert.Equal("WHO AWaRe 2025", first.SourceVersion);

        var second = await loader.LoadIfChangedAsync();

        Assert.True(second.WasAlreadyUpToDate);
        Assert.Equal(2, await awareRepository.CountAsync());
    }

    [Fact]
    public async Task AwareSeedLoader_ReloadsWhenFileChanges()
    {
        InitializeDatabase();

        var seedPath = Path.Combine(_directory, "aware_2025.csv");
        const string header = "atc_code,antibiotic_name,aware_group,is_systemic,source_version";

        await File.WriteAllTextAsync(seedPath, header + "\nJ01CA04,Amoxicillin,ACCESS,true,WHO AWaRe 2025");

        var awareRepository = new AwareClassificationRepository(_connectionFactory);
        var loader = new AwareSeedLoader(
            awareRepository, new AppSettingRepository(_connectionFactory), new[] { seedPath });

        await loader.LoadIfChangedAsync();

        await File.WriteAllTextAsync(seedPath, header
            + "\nJ01CA04,Amoxicillin,ACCESS,true,WHO AWaRe 2026"
            + "\nJ01FA10,Azithromycin,WATCH,true,WHO AWaRe 2026");

        var result = await loader.LoadIfChangedAsync();

        Assert.False(result.WasAlreadyUpToDate);
        Assert.Equal(2, result.LoadedCount);
        Assert.Equal("WHO AWaRe 2026", result.SourceVersion);
    }

    /// <summary>
    /// 시드 파일이 없어도 예외 없이 실패만 알리고 넘어가야 한다.
    /// 이 경로에서 예외가 나면 앱이 아예 뜨지 못한다.
    /// </summary>
    [Fact]
    public async Task AwareSeedLoader_ReportsFailureWhenFileIsMissing()
    {
        InitializeDatabase();

        var loader = new AwareSeedLoader(
            new AwareClassificationRepository(_connectionFactory),
            new AppSettingRepository(_connectionFactory),
            new[] { Path.Combine(_directory, "does-not-exist.csv") });

        var result = await loader.LoadIfChangedAsync();

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Message);
    }

    /// <summary>동봉된 빈 템플릿을 읽은 상황. 적재할 행이 없다고 알린다.</summary>
    [Fact]
    public async Task AwareSeedLoader_ReportsFailureForHeaderOnlyTemplate()
    {
        InitializeDatabase();

        var seedPath = Path.Combine(_directory, "aware_2025.csv");
        await File.WriteAllTextAsync(
            seedPath, "atc_code,antibiotic_name,aware_group,is_systemic,source_version\n");

        var loader = new AwareSeedLoader(
            new AwareClassificationRepository(_connectionFactory),
            new AppSettingRepository(_connectionFactory),
            new[] { seedPath });

        var result = await loader.LoadIfChangedAsync();

        Assert.False(result.IsSuccess);
    }

    /// <summary>%APPDATA% 자리에 놓인 파일이 설치 폴더 동봉본을 이긴다.</summary>
    [Fact]
    public async Task AwareSeedLoader_PrefersFirstCandidatePath()
    {
        InitializeDatabase();

        const string header = "atc_code,antibiotic_name,aware_group,is_systemic,source_version";

        var overridePath = Path.Combine(_directory, "override.csv");
        var bundledPath = Path.Combine(_directory, "bundled.csv");

        await File.WriteAllTextAsync(overridePath, header + "\nJ01FA10,Azithromycin,WATCH,true,WHO AWaRe 2026");
        await File.WriteAllTextAsync(bundledPath, header + "\nJ01CA04,Amoxicillin,ACCESS,true,WHO AWaRe 2025");

        var loader = new AwareSeedLoader(
            new AwareClassificationRepository(_connectionFactory),
            new AppSettingRepository(_connectionFactory),
            new[] { overridePath, bundledPath });

        var result = await loader.LoadIfChangedAsync();

        Assert.Equal("WHO AWaRe 2026", result.SourceVersion);
    }

    // ── 도우미 ───────────────────────────────────────────────────────────────

    private static AwareClassification BuildClassification(
        string? atcCode, string name, AwareGroup group, bool isSystemic = true)
    {
        return new AwareClassification
        {
            AwareId = Guid.NewGuid().ToString(),
            AtcCode = atcCode,
            AntibioticName = name,
            NormalizedName = AntibioticNameNormalizer.Normalize(name),
            AwareGroup = group,
            IsSystemic = isSystemic,
            SourceVersion = "WHO AWaRe 2025",
            UpdatedAt = 0
        };
    }

    private static CounsellingLogEntry BuildLog(
        string transactionId, string awareGroup, bool printed,
        string? skipReason = null, long createdAt = 1_000)
    {
        return new CounsellingLogEntry
        {
            LogId = Guid.NewGuid().ToString(),
            TransactionId = transactionId,
            ProductId = "p1",
            AtcCode = "J01CA04",
            AwareGroup = awareGroup,
            Printed = printed,
            SkipReason = skipReason,
            Locale = "en",
            SourceVersion = "WHO AWaRe 2025",
            CreatedAt = createdAt
        };
    }

    /// <summary>외래 키 제약이 켜져 있어 참조 대상 행을 먼저 만들어야 한다.</summary>
    private void SeedFacilityUserAndProduct()
    {
        ExecuteSql("""
            INSERT INTO Facility (facility_id, facility_name, country, district, facility_type, status)
            VALUES ('f1', 'Test Pharmacy', 'KH', 'PP', 'Pharmacy', 'Active');

            INSERT INTO Users (user_id, facility_id, username, password_hash, role, status, created_at)
            VALUES ('u1', 'f1', 'tester', 'hash', 'Administrator', 'Active', 0);

            INSERT INTO Product_Master
                (product_id, product_name, unit, cost_price, selling_price,
                 safety_stock_level, status, created_at)
            VALUES ('p1', 'Amoxicillin 500mg', 'tablet', 1.0, 2.0, 10, 'Active', 0);
            """);
    }

    private void InsertSaleTransaction(string transactionId, long transactionTime)
    {
        ExecuteSql($"""
            INSERT INTO Stock_Transaction
                (transaction_id, facility_id, product_id, user_id, transaction_type,
                 batch_number, expiry_date, quantity, selling_price_at_transaction,
                 payment_method, total_amount, reason, transaction_time)
            VALUES
                ('{transactionId}', 'f1', 'p1', 'u1', 'StockOut',
                 'B1', 0, 1, 2.0, 'Cash', 2.0, NULL, {transactionTime});
            """);
    }
}
