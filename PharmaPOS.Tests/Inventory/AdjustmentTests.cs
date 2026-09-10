using Microsoft.Data.Sqlite;
using PharmaPOS.Application.Inventory;
using PharmaPOS.DataAccess.Database;
using PharmaPOS.DataAccess.Repositories;

namespace PharmaPOS.Tests.Inventory;

/// <summary>
/// 재고 조정. 실제 SQLite에 대고 서비스와 리포지터리를 함께 돌린다.
///
/// 조정은 실사한 수치를 전산에 덮어쓰는 유일한 경로라, 조용히 어긋나면
/// 그 뒤의 모든 재고가 틀린 값 위에 쌓인다. 게다가 Inventory 갱신과 원장 기록이
/// 한 트랜잭션 안에 있어야 하는데, 반만 성공하는 실패는 화면에 아무 표시도 남기지 않는다.
/// </summary>
public class AdjustmentTests : IDisposable
{
    private const string FacilityId = "fac-1";
    private const string UserId = "user-1";
    private const string ProductId = "prod-1";
    private const string InventoryId = "inv-1";
    private const int UnitsPerBox = 30;

    private readonly string _directory;
    private readonly SqliteConnectionFactory _connectionFactory;
    private readonly AdjustmentService _service;

    public AdjustmentTests()
    {
        _directory = Path.Combine(
            Path.GetTempPath(), "pharmapos-adjustment-tests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_directory);

        _connectionFactory = new SqliteConnectionFactory(Path.Combine(_directory, "test.db"));
        new DatabaseInitializer(_connectionFactory).Initialize();
        Seed();

        _service = new AdjustmentService(new AdjustmentRepository(_connectionFactory));
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

    /// <summary>3박스 + 낱개 10 = 100개로 시작한다.</summary>
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
            VALUES ('{ProductId}', 'Amoxil 500mg Capsule', 'Capsule', 3, 4.53, 5,
                    'Active', 0, 0, {UnitsPerBox});

            INSERT INTO Inventory
                (inventory_id, facility_id, product_id, batch_number, expiry_date,
                 current_quantity, box_quantity, unit_quantity, updated_at)
            VALUES ('{InventoryId}', '{FacilityId}', '{ProductId}', 'B2401', 0, 100, 3, 10, 0);
            """;
        command.ExecuteNonQuery();
    }

    private void InsertSecondBatch(string inventoryId, string batchNumber)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Inventory
                (inventory_id, facility_id, product_id, batch_number, expiry_date,
                 current_quantity, box_quantity, unit_quantity, updated_at)
            VALUES ($id, $facility, $product, $batch, 0, 50, 1, 20, 0);
            """;
        command.Parameters.AddWithValue("$id", inventoryId);
        command.Parameters.AddWithValue("$facility", FacilityId);
        command.Parameters.AddWithValue("$product", ProductId);
        command.Parameters.AddWithValue("$batch", batchNumber);
        command.ExecuteNonQuery();
    }

    private (int Total, int Boxes, int Units, string Batch) ReadInventory(string inventoryId = InventoryId)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT current_quantity, box_quantity, unit_quantity, batch_number
            FROM Inventory WHERE inventory_id = $id;
            """;
        command.Parameters.AddWithValue("$id", inventoryId);

        using var reader = command.ExecuteReader();
        Assert.True(reader.Read());
        return (reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2), reader.GetString(3));
    }

    private List<(int Quantity, string Reason, long? Before, long? After, string Batch)> ReadLedger()
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT quantity, COALESCE(reason, ''), stock_before, stock_after, batch_number
            FROM Stock_Transaction
            WHERE transaction_type = 'Adjustment'
            ORDER BY transaction_time;
            """;

        using var reader = command.ExecuteReader();
        var rows = new List<(int, string, long?, long?, string)>();
        while (reader.Read())
        {
            rows.Add((
                reader.GetInt32(0),
                reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetInt64(2),
                reader.IsDBNull(3) ? null : reader.GetInt64(3),
                reader.GetString(4)));
        }

        return rows;
    }

    private Task<AdjustmentResult> AdjustAsync(
        int physicalBoxCount,
        int physicalUnitCount,
        string reason = "Stock count",
        int systemQuantity = 100,
        string batchNumber = "B2401",
        string originalBatchNumber = "B2401",
        bool allowZeroDelta = false) =>
        _service.SaveAdjustmentAsync(
            FacilityId, ProductId, UserId, InventoryId,
            batchNumber, originalBatchNumber, expiryDate: 0,
            systemQuantity, physicalBoxCount, physicalUnitCount, UnitsPerBox,
            reason, allowZeroDelta);

    // ── 정상 경로 ────────────────────────────────────────────────────────────

    /// <summary>실사가 전산보다 적을 때. 셋이 한 번에 맞아야 한다: 총량, 박스/낱개 내역, 원장.</summary>
    [Fact]
    public async Task ShortCount_WritesTheCountAndTheLedgerTogether()
    {
        // 3박스 + 10 = 100 이었는데 실사는 2박스 + 25 = 85.
        var result = await AdjustAsync(physicalBoxCount: 2, physicalUnitCount: 25);

        Assert.True(result.IsSuccess, result.Message);

        var inventory = ReadInventory();
        Assert.Equal(85, inventory.Total);
        Assert.Equal(2, inventory.Boxes);
        Assert.Equal(25, inventory.Units);

        var ledger = Assert.Single(ReadLedger());
        Assert.Equal(-15, ledger.Quantity);
        Assert.Equal("Stock count", ledger.Reason);
    }

    [Fact]
    public async Task OverCount_RecordsAPositiveDelta()
    {
        var result = await AdjustAsync(physicalBoxCount: 4, physicalUnitCount: 0);

        Assert.True(result.IsSuccess, result.Message);
        Assert.Equal(120, ReadInventory().Total);
        Assert.Equal(20, Assert.Single(ReadLedger()).Quantity);
    }

    /// <summary>
    /// 전후 재고는 Inventory에서 다시 읽어야 한다. quantity로 계산하면
    /// before + quantity == after가 언제나 참이 되어 검산 자체가 무의미해진다.
    /// </summary>
    [Fact]
    public async Task StockBeforeAndAfter_ComeFromInventory()
    {
        await AdjustAsync(physicalBoxCount: 2, physicalUnitCount: 25);

        var ledger = Assert.Single(ReadLedger());
        Assert.Equal(100, ledger.Before);
        Assert.Equal(85, ledger.After);
    }

    /// <summary>낱개가 박스당 개수를 넘어도 센 대로 적는다. 실사는 되돌리는 것이 아니다.</summary>
    [Fact]
    public async Task LooseUnitsBeyondABox_AreStoredAsCounted()
    {
        var result = await AdjustAsync(physicalBoxCount: 3, physicalUnitCount: 35);

        Assert.True(result.IsSuccess, result.Message);

        var inventory = ReadInventory();
        Assert.Equal(125, inventory.Total);
        Assert.Equal(3, inventory.Boxes);
        Assert.Equal(35, inventory.Units);
    }

    // ── 확인이 필요한 경우 ───────────────────────────────────────────────────

    [Fact]
    public async Task NoDifference_AsksBeforeWritingAnything()
    {
        var result = await AdjustAsync(physicalBoxCount: 3, physicalUnitCount: 10, reason: string.Empty);

        Assert.True(result.RequiresConfirmation);
        Assert.Empty(ReadLedger());
    }

    /// <summary>확인을 받으면 사유 없이도 저장된다. 차이가 없으니 적을 사유도 없다.</summary>
    [Fact]
    public async Task NoDifference_SavesOnceConfirmed()
    {
        var result = await AdjustAsync(
            physicalBoxCount: 3, physicalUnitCount: 10, reason: string.Empty, allowZeroDelta: true);

        Assert.True(result.IsSuccess, result.Message);

        var ledger = Assert.Single(ReadLedger());
        Assert.Equal(0, ledger.Quantity);
        Assert.Equal(100, ledger.Before);
        Assert.Equal(100, ledger.After);
    }

    [Fact]
    public async Task DifferenceWithoutAReason_IsRefused()
    {
        var result = await AdjustAsync(physicalBoxCount: 2, physicalUnitCount: 0, reason: "   ");

        Assert.False(result.IsSuccess);
        Assert.Equal("Please enter the adjustment reason.", result.Message);
        Assert.Empty(ReadLedger());
        Assert.Equal(100, ReadInventory().Total);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, -5)]
    public async Task NegativeCounts_AreRefused(int boxes, int units)
    {
        var result = await AdjustAsync(boxes, units);

        Assert.False(result.IsSuccess);
        Assert.Equal("Physical count cannot be negative.", result.Message);
        Assert.Equal(100, ReadInventory().Total);
    }

    // ── 동시성 ───────────────────────────────────────────────────────────────

    /// <summary>
    /// 화면이 들고 있던 전산 수량이 그사이 바뀌었으면 저장하지 않는다.
    /// 덮어썼다면 그동안 팔린 수량이 소리 없이 되살아난다.
    /// </summary>
    [Fact]
    public async Task StaleSystemQuantity_IsRefusedAndChangesNothing()
    {
        var result = await AdjustAsync(
            physicalBoxCount: 2, physicalUnitCount: 0, systemQuantity: 90);

        Assert.True(result.IsConcurrencyConflict);
        Assert.Equal("Inventory quantity has changed. Please try again.", result.Message);
        Assert.Equal(100, ReadInventory().Total);
        Assert.Empty(ReadLedger());
    }

    // ── 배치번호 고치기 ──────────────────────────────────────────────────────

    /// <summary>배치번호 없이 들어온 초기 재고에 나중에 번호를 붙이는 경로.</summary>
    [Fact]
    public async Task BatchNumberChange_SavesWithoutAQuantityDifference()
    {
        var result = await AdjustAsync(
            physicalBoxCount: 3, physicalUnitCount: 10, reason: string.Empty,
            batchNumber: "B2402", originalBatchNumber: "B2401");

        Assert.True(result.IsSuccess, result.Message);
        Assert.Equal("B2402", ReadInventory().Batch);

        // 이미 쌓인 입고·판매 행은 옛 번호를 그대로 달고 있다. 왜 다른지 설명할 곳이 사유뿐이다.
        var ledger = Assert.Single(ReadLedger());
        Assert.Equal("Batch number: B2401 → B2402", ledger.Reason);
        Assert.Equal("B2402", ledger.Batch);
    }

    [Fact]
    public async Task BatchNumberChange_KeepsTheTypedReasonAlongsideTheNote()
    {
        await AdjustAsync(
            physicalBoxCount: 2, physicalUnitCount: 0, reason: "Damaged",
            batchNumber: "B2402", originalBatchNumber: "B2401");

        Assert.Equal("Damaged (Batch number: B2401 → B2402)", Assert.Single(ReadLedger()).Reason);
    }

    /// <summary>
    /// Inventory는 (시설, 상품, 배치번호)로 유일하다. 겹치는 번호를 붙이려 하면
    /// DB 오류가 아니라 무엇이 문제인지 말해 주는 문구가 나와야 한다.
    /// </summary>
    [Fact]
    public async Task DuplicateBatchNumber_IsRefusedWithAReadableMessage()
    {
        InsertSecondBatch("inv-2", "B2402");

        var result = await AdjustAsync(
            physicalBoxCount: 3, physicalUnitCount: 10, reason: string.Empty,
            batchNumber: "B2402", originalBatchNumber: "B2401");

        Assert.False(result.IsSuccess);
        Assert.Equal(
            "This batch number is already used by another batch of this product.", result.Message);
        Assert.Equal("B2401", ReadInventory().Batch);
        Assert.Empty(ReadLedger());
    }

    /// <summary>번호를 비우는 것은 막지 않는다 — 번호 없이 관리하던 재고로 되돌리는 경로다.</summary>
    [Fact]
    public async Task ClearingTheBatchNumber_IsAllowed()
    {
        var result = await AdjustAsync(
            physicalBoxCount: 3, physicalUnitCount: 10, reason: string.Empty,
            batchNumber: "   ", originalBatchNumber: "B2401");

        Assert.True(result.IsSuccess, result.Message);
        Assert.Equal(string.Empty, ReadInventory().Batch);
        Assert.Equal("Batch number: B2401 → (none)", Assert.Single(ReadLedger()).Reason);
    }

    // ── 입력 검증 ────────────────────────────────────────────────────────────

    [Fact]
    public async Task MissingBatch_IsRefusedBeforeTouchingTheDatabase()
    {
        var result = await _service.SaveAdjustmentAsync(
            FacilityId, ProductId, UserId, inventoryId: "  ",
            "B2401", "B2401", expiryDate: 0,
            systemQuantity: 100, physicalBoxCount: 2, physicalUnitCount: 0,
            unitsPerBox: UnitsPerBox, reason: "Stock count");

        Assert.False(result.IsSuccess);
        Assert.Equal("Please select a batch.", result.Message);
        Assert.Equal(100, ReadInventory().Total);
    }

    /// <summary>
    /// 조정을 두 번 하면 두 번째의 before가 첫 번째의 after와 이어져야 한다.
    /// 이어지지 않으면 Stock History에서 배치를 훑어 내려가는 검산이 깨진다.
    /// </summary>
    [Fact]
    public async Task ConsecutiveAdjustments_ChainThroughBeforeAndAfter()
    {
        await AdjustAsync(physicalBoxCount: 2, physicalUnitCount: 25);
        await AdjustAsync(physicalBoxCount: 2, physicalUnitCount: 0, systemQuantity: 85);

        var ledger = ReadLedger();
        Assert.Equal(2, ledger.Count);
        Assert.Equal(ledger[0].After, ledger[1].Before);
        Assert.Equal(60, ledger[1].After);
    }
}
