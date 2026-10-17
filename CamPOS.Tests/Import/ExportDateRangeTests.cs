using Microsoft.Data.Sqlite;
using PharmaPOS.Application.Inventory;
using PharmaPOS.DataAccess.Database;
using PharmaPOS.DataAccess.Repositories;

namespace PharmaPOS.Tests.Import;

/// <summary>
/// Import / Export 화면의 내보내기 기간.
///
/// 기간은 판매 내역에만 걸린다. 상품은 현재 카탈로그라 잘라내면 고쳐서 다시 넣는
/// 왕복이 깨지고, 재고는 배치별 현재 수량이라 기간이라는 게 없다 — updated_at으로
/// 자르면 그동안 움직이지 않은 배치가 빠져서 재고 실사에 쓸 수 없게 된다.
/// </summary>
public class ExportDateRangeTests : IDisposable
{
    private const string FacilityId = "fac-1";
    private const string UserId = "user-1";
    private const string ProductId = "prod-1";

    private static readonly DateTime March = new(2026, 3, 10, 9, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime June = new(2026, 6, 20, 9, 0, 0, DateTimeKind.Utc);

    private readonly string _directory;
    private readonly SqliteConnectionFactory _connectionFactory;
    private readonly BackupRepository _repository;
    private readonly BackupService _service;

    public ExportDateRangeTests()
    {
        _directory = Path.Combine(
            Path.GetTempPath(), "pharmapos-export-range-tests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_directory);

        var databasePath = Path.Combine(_directory, "test.db");
        _connectionFactory = new SqliteConnectionFactory(databasePath);
        new DatabaseInitializer(_connectionFactory).Initialize();
        Seed();

        _repository = new BackupRepository(_connectionFactory, databasePath);
        _service = new BackupService(_repository);
    }

    public void Dispose()
    {
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

    private static long Ms(DateTime value) => new DateTimeOffset(value).ToUnixTimeMilliseconds();

    private void Seed()
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = $"""
            INSERT INTO Facility (facility_id, facility_name, country, district, facility_type, status)
            VALUES ('{FacilityId}', 'F', 'KH', 'D', 'Pharmacy', 'Active');

            INSERT INTO Users (user_id, facility_id, username, password_hash, role, status, created_at)
            VALUES ('{UserId}', '{FacilityId}', 'admin', 'h', 'Administrator', 'Active', 0);

            INSERT INTO Product_Master
                (product_id, product_name, unit, cost_price, selling_price, safety_stock_level,
                 status, created_at, is_combination, units_per_box)
            VALUES ('{ProductId}', 'Amoxil 500mg Capsule', 'Capsule', 3, 4.53, 5, 'Active', 0, 0, 100);

            INSERT INTO Inventory
                (inventory_id, facility_id, product_id, batch_number, expiry_date,
                 current_quantity, box_quantity, unit_quantity, updated_at)
            VALUES ('inv-1', '{FacilityId}', '{ProductId}', 'B2401', 0, 100, 1, 0, {Ms(March)});

            INSERT INTO Stock_Transaction
                (transaction_id, facility_id, product_id, user_id, transaction_type,
                 batch_number, expiry_date, quantity, selling_price_at_transaction,
                 total_amount, payment_method, transaction_time)
            VALUES ('t-march', '{FacilityId}', '{ProductId}', '{UserId}', 'StockOut',
                    'B2401', 0, 5, 1, 5, 'Cash', {Ms(March)});

            INSERT INTO Stock_Transaction
                (transaction_id, facility_id, product_id, user_id, transaction_type,
                 batch_number, expiry_date, quantity, selling_price_at_transaction,
                 total_amount, payment_method, transaction_time)
            VALUES ('t-june', '{FacilityId}', '{ProductId}', '{UserId}', 'StockOut',
                    'B2401', 0, 7, 1, 7, 'Cash', {Ms(June)});
            """;
        command.ExecuteNonQuery();
    }

    private async Task<string[]> ExportLinesAsync(
        ExportDataset dataset, long? dateFromUtc, long? dateToUtc)
    {
        var path = Path.Combine(_directory, $"{Guid.NewGuid()}.csv");
        await _repository.ExportDatasetAsync(dataset, path, isCsvFormat: true, dateFromUtc, dateToUtc);
        return await File.ReadAllLinesAsync(path);
    }

    [Fact]
    public async Task SalesHistory_WithoutRange_ExportsEveryPeriod()
    {
        var lines = await ExportLinesAsync(ExportDataset.SalesHistory, null, null);

        // 헤더 + 두 줄
        Assert.Equal(3, lines.Length);
    }

    [Fact]
    public async Task SalesHistory_WithRange_ExportsOnlyThatPeriod()
    {
        var lines = await ExportLinesAsync(
            ExportDataset.SalesHistory,
            Ms(new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc)),
            Ms(new DateTime(2026, 3, 31, 23, 59, 59, DateTimeKind.Utc)));

        Assert.Equal(2, lines.Length);
        Assert.Contains("5", lines[1]);
    }

    [Fact]
    public async Task SalesHistory_OpenEndedRange_FiltersOnOneSide()
    {
        var fromJuneOnly = await ExportLinesAsync(
            ExportDataset.SalesHistory,
            Ms(new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc)),
            null);

        var untilMayOnly = await ExportLinesAsync(
            ExportDataset.SalesHistory,
            null,
            Ms(new DateTime(2026, 5, 31, 23, 59, 59, DateTimeKind.Utc)));

        Assert.Equal(2, fromJuneOnly.Length);
        Assert.Equal(2, untilMayOnly.Length);
    }

    /// <summary>
    /// 상품과 재고에는 기간이 걸리지 않는다. 좁은 기간을 넘겨도 전부 나와야 한다 —
    /// 이 둘은 "그동안 일어난 일"이 아니라 "지금 무엇이 있는가"라서다.
    /// </summary>
    [Theory]
    [InlineData(ExportDataset.Products)]
    [InlineData(ExportDataset.Inventory)]
    public async Task CatalogueAndStock_IgnoreTheRange(ExportDataset dataset)
    {
        var narrow = await ExportLinesAsync(
            dataset,
            Ms(new DateTime(2099, 1, 1, 0, 0, 0, DateTimeKind.Utc)),
            Ms(new DateTime(2099, 1, 2, 0, 0, 0, DateTimeKind.Utc)));

        var everything = await ExportLinesAsync(dataset, null, null);

        Assert.Equal(everything.Length, narrow.Length);
        Assert.Equal(2, everything.Length);
    }

    [Fact]
    public void SupportsDateRange_OnlySalesHistory()
    {
        Assert.True(ExportDatasets.SupportsDateRange(ExportDataset.SalesHistory));
        Assert.False(ExportDatasets.SupportsDateRange(ExportDataset.Products));
        Assert.False(ExportDatasets.SupportsDateRange(ExportDataset.Inventory));
    }

    [Fact]
    public async Task ReversedRange_IsRefusedBeforeAnyFileIsWritten()
    {
        var result = await _service.ExportDatasetsAsync(
            _directory,
            new[] { ExportDataset.SalesHistory },
            isCsvFormat: true,
            dateFrom: June,
            dateTo: March);

        Assert.False(result.IsSuccess);
        Assert.Equal("Start date cannot be later than end date.", result.Message);
        Assert.Empty(Directory.GetFiles(_directory, "*.csv"));
    }

    /// <summary>
    /// 기간을 자른 파일과 전 기간 파일이 폴더에서 구분되어야 한다.
    /// 이름이 같으면 나중에 어느 쪽인지 알 길이 없다.
    /// </summary>
    [Fact]
    public async Task FileName_CarriesThePeriodOnlyWhenOneWasSet()
    {
        var folder = Path.Combine(_directory, "named");
        Directory.CreateDirectory(folder);

        await _service.ExportDatasetsAsync(
            folder, new[] { ExportDataset.SalesHistory }, isCsvFormat: true,
            dateFrom: March, dateTo: June);

        var ranged = Directory.GetFiles(folder).Select(Path.GetFileName).Single();
        Assert.Contains("20260310-20260620", ranged);

        Directory.Delete(folder, recursive: true);
        Directory.CreateDirectory(folder);

        // 기간 없이 뽑은 판매 파일과, 애초에 기간이 걸리지 않는 상품 파일.
        await _service.ExportDatasetsAsync(
            folder, new[] { ExportDataset.SalesHistory, ExportDataset.Products }, isCsvFormat: true);

        var plain = Directory.GetFiles(folder).Select(Path.GetFileName).ToList();
        Assert.All(plain, name => Assert.DoesNotContain("-2026", name));
    }

    /// <summary>기간을 준 채 상품을 뽑아도 파일 이름에 기간이 붙으면 안 된다 — 걸리지 않은 조건이다.</summary>
    [Fact]
    public async Task FileName_LeavesThePeriodOffDatasetsItDoesNotApplyTo()
    {
        var folder = Path.Combine(_directory, "products-only");
        Directory.CreateDirectory(folder);

        await _service.ExportDatasetsAsync(
            folder, new[] { ExportDataset.Products }, isCsvFormat: true,
            dateFrom: March, dateTo: June);

        var name = Directory.GetFiles(folder).Select(Path.GetFileName).Single();
        Assert.DoesNotContain("20260310", name);
    }
}
