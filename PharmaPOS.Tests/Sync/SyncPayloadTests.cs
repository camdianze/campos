using System.Reflection;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using PharmaPOS.Application.Sync;
using PharmaPOS.DataAccess.Database;
using PharmaPOS.DataAccess.Repositories;

namespace PharmaPOS.Tests.Sync;

/// <summary>
/// 서버로 나가는 묶음에 무엇이 들어가고 무엇이 빠지는지를 고정한다.
///
/// 이것이 필요한 이유는 실패가 조용하기 때문이다. 보내면 안 되는 값이 섞여도 JSON이
/// 조금 길어질 뿐 화면에는 아무 표시가 없고, 한 번 보낸 것은 되돌릴 수 없다.
/// 금액을 제외하기로 했다가 되돌린 것처럼 <b>무엇을 보낼지는 바뀔 수 있지만</b>,
/// 비밀번호와 복구 정보는 바뀌지 않는다 — 그것들은 약국 밖으로 나갈 이유가 없다.
///
/// 같은 자리를 지키는 테스트가 이미 있다(AntibioticExportCsvTests). 거기서는 금액이,
/// 여기서는 계정 정보가 선이다.
/// </summary>
public class SyncPayloadTests : IDisposable
{
    private const string FacilityId = "fac-1";

    private readonly string _directory;
    private readonly SqliteConnectionFactory _connectionFactory;
    private readonly SyncRepository _repository;

    public SyncPayloadTests()
    {
        _directory = Path.Combine(
            Path.GetTempPath(), "pharmapos-sync-tests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_directory);

        _connectionFactory = new SqliteConnectionFactory(Path.Combine(_directory, "test.db"));
        new DatabaseInitializer(_connectionFactory).Initialize();
        Seed();

        _repository = new SyncRepository(_connectionFactory);
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

    /// <summary>비밀번호와 복구 정보를 <b>일부러</b> 넣어 둔다. 새지 않는지가 요점이다.</summary>
    private void Seed()
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = $"""
            INSERT INTO Facility (facility_id, facility_name, country, district, facility_type, status)
            VALUES ('{FacilityId}', 'F', 'KH', 'D', 'Pharmacy', 'Active');

            INSERT INTO Users (user_id, facility_id, username, password_hash, role, status, created_at)
            VALUES ('user-1', '{FacilityId}', 'pharmacist',
                    '$2a$11$SECRETHASHSECRETHASHSECRETHASH', 'Administrator', 'Active', 0);

            INSERT INTO Product_Master
                (product_id, product_name, unit, cost_price, selling_price,
                 safety_stock_level, status, created_at, units_per_box,
                 selling_price_khr, barcode)
            VALUES ('prod-1', 'Amoxil 500mg Capsule', 'Capsule', 3.0, 9.0,
                    5, 'Active', 100, 30, 36900, '8801234567890');

            INSERT INTO Inventory
                (inventory_id, facility_id, product_id, batch_number, expiry_date,
                 current_quantity, box_quantity, unit_quantity, updated_at)
            VALUES ('inv-1', '{FacilityId}', 'prod-1', 'B2401', 0, 48, 1, 18, 200);

            INSERT INTO Stock_Transaction
                (transaction_id, facility_id, product_id, user_id, transaction_type,
                 batch_number, expiry_date, quantity, selling_price_at_transaction,
                 total_amount, payment_method, transaction_time)
            VALUES ('tx-1', '{FacilityId}', 'prod-1', 'user-1', 'StockOut',
                    'B2401', 0, 2, 9.0, 18.0, 'Cash', 300);
            """;
        command.ExecuteNonQuery();
    }

    private async Task<string> BuildJsonAsync()
    {
        var payload = await new SyncPayloadBuilder(_repository).BuildAsync(FacilityId);
        return JsonSerializer.Serialize(payload, SyncJson.Options);
    }

    // ── 나가면 안 되는 것 ───────────────────────────────────────────────────

    /// <summary>
    /// 비밀번호 해시는 어떤 모양으로도 묶음에 들어가면 안 된다.
    /// 값으로 찾는다 — 컬럼 이름만 보면 다른 이름으로 담았을 때 놓친다.
    /// </summary>
    [Fact]
    public async Task ThePasswordHashNeverLeaves()
    {
        var json = await BuildJsonAsync();

        Assert.DoesNotContain("SECRETHASH", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password", json, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>복구 수단은 계정을 빼앗는 데 그대로 쓰이는 값이다.</summary>
    [Theory]
    [InlineData("security_question")]
    [InlineData("securityAnswer")]
    [InlineData("recovery")]
    [InlineData("smtp")]
    [InlineData("email")]
    public async Task NoRecoveryInformationLeaves(string forbidden)
    {
        Assert.DoesNotContain(forbidden, await BuildJsonAsync(), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 담을 자리가 없으면 실수로 채울 수도 없다 — SyncUser에 계정 보안 속성이
    /// 아예 없는지를 타입 쪽에서도 확인한다.
    /// </summary>
    [Fact]
    public async Task TheUserTypeHasNowhereToPutASecret()
    {
        var names = typeof(SyncUser)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .ToList();

        Assert.Equal(new[] { "UserId", "Username", "Role", "Status" }.Order(), names.Order());

        await Task.CompletedTask;
    }

    /// <summary>
    /// 이름이 서버 컬럼과 같아야 한다. 맞춰 두면 Edge Function의 허용 목록이 곧
    /// 컬럼 목록이 되고, 이름을 바꿔 주는 표가 필요 없어진다 — 그 표는 한 줄만
    /// 틀려도 그 컬럼만 조용히 비어서 들어가는 종류의 코드다.
    /// </summary>
    [Theory]
    [InlineData("product_id")]
    [InlineData("internal_barcode")]
    [InlineData("units_per_box")]
    [InlineData("selling_price_khr")]
    [InlineData("safety_stock_level")]
    [InlineData("transaction_time")]
    [InlineData("stock_before")]
    public async Task PropertiesAreNamedLikeTheServerColumns(string column)
    {
        Assert.Contains($"\"{column}\"", await BuildJsonAsync(), StringComparison.Ordinal);
    }

    /// <summary>C# 쪽 이름이 그대로 나가면 서버가 못 알아본다.</summary>
    [Fact]
    public async Task NoPascalCaseNamesGoOut()
    {
        var json = await BuildJsonAsync();

        Assert.DoesNotContain("\"ProductId\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"UnitsPerBox\"", json, StringComparison.Ordinal);
    }

    // ── 나가야 하는 것 ─────────────────────────────────────────────────────

    /// <summary>
    /// 금액은 제외했다가 되돌린 결정이다. 서버는 원본을 그대로 보관하고,
    /// 무엇을 빼고 무엇을 가공할지는 읽어 가는 쪽이 정한다.
    /// </summary>
    [Fact]
    public async Task MoneyIsIncluded()
    {
        var payload = await new SyncPayloadBuilder(_repository).BuildAsync(FacilityId);

        var product = Assert.Single(payload.Products);
        Assert.Equal(3.0m, product.CostPrice);
        Assert.Equal(9.0m, product.SellingPrice);

        var transaction = Assert.Single(payload.Transactions);
        Assert.Equal(18.0m, transaction.TotalAmount);
        Assert.Equal("Cash", transaction.PaymentMethod);
    }

    /// <summary>
    /// 리엘로 적어 넣은 가격은 달러와 함께 간다. 달러만 보내면 약국이 정한 리엘 금액이
    /// 서버에서 사라지고, 환율이 바뀐 뒤에는 어떤 계산으로도 되살릴 수 없다.
    /// </summary>
    [Fact]
    public async Task TheRielPriceGoesWithTheDollarOne()
    {
        var payload = await new SyncPayloadBuilder(_repository).BuildAsync(FacilityId);

        Assert.Equal(36900m, Assert.Single(payload.Products).SellingPriceKhr);
    }

    /// <summary>앱이 바코드로 상품을 찾으려면 세 코드가 모두 있어야 한다.</summary>
    [Fact]
    public async Task BarcodesAreIncluded()
    {
        var payload = await new SyncPayloadBuilder(_repository).BuildAsync(FacilityId);

        Assert.Equal("8801234567890", Assert.Single(payload.Products).Barcode);
    }

    /// <summary>직원 이름은 "누가 처리했는지"를 보여주기 위해 보낸다.</summary>
    [Fact]
    public async Task TheStaffNameIsIncluded()
    {
        var payload = await new SyncPayloadBuilder(_repository).BuildAsync(FacilityId);

        var user = Assert.Single(payload.Users);
        Assert.Equal("pharmacist", user.Username);
        Assert.Equal("Administrator", user.Role);
    }

    /// <summary>유통기한 0은 "모름"이다. 날짜로 바꾸지 않고 그대로 보낸다.</summary>
    [Fact]
    public async Task AnUnknownExpiryStaysZero()
    {
        var payload = await new SyncPayloadBuilder(_repository).BuildAsync(FacilityId);

        Assert.Equal(0, Assert.Single(payload.Inventory).ExpiryDate);
    }

    // ── 어디까지 보냈는지 ──────────────────────────────────────────────────

    /// <summary>처음에는 아무것도 보낸 적이 없으므로 전부 담긴다.</summary>
    [Fact]
    public async Task TheFirstBatchCarriesEverything()
    {
        var payload = await new SyncPayloadBuilder(_repository).BuildAsync(FacilityId);

        Assert.Single(payload.Transactions);
        Assert.True(payload.Positions[SyncStreams.Transactions] > 0);
    }

    /// <summary>위치를 적은 뒤에는 그 다음 행부터 담긴다.</summary>
    [Fact]
    public async Task AfterSavingThePositionTheRowIsNotSentAgain()
    {
        var first = await new SyncPayloadBuilder(_repository).BuildAsync(FacilityId);
        await _repository.SavePositionAsync(
            SyncStreams.Transactions, first.Positions[SyncStreams.Transactions]);

        var second = await new SyncPayloadBuilder(_repository).BuildAsync(FacilityId);

        Assert.Empty(second.Transactions);

        // 상품과 재고는 매번 전부 간다 — 전체 교체가 삭제를 다루는 방법이다.
        Assert.Single(second.Products);
        Assert.Single(second.Inventory);
    }

    /// <summary>
    /// 이것이 rowid를 기준으로 삼은 이유다. 입고 날짜는 사용자가 고르므로 지난
    /// 날짜로 오늘 들어온 행이 생기는데, 시각을 기준으로 고르면 그 행은 영영 빠진다.
    /// </summary>
    [Fact]
    public async Task ARowEnteredWithAnOlderDateIsStillSent()
    {
        var first = await new SyncPayloadBuilder(_repository).BuildAsync(FacilityId);
        await _repository.SavePositionAsync(
            SyncStreams.Transactions, first.Positions[SyncStreams.Transactions]);

        // 지난주 날짜로 오늘 입력한 입고. transaction_time이 이미 보낸 행보다 이르다.
        using (var connection = _connectionFactory.CreateOpenConnection())
        using (var command = connection.CreateCommand())
        {
            command.CommandText = $"""
                INSERT INTO Stock_Transaction
                    (transaction_id, facility_id, product_id, user_id, transaction_type,
                     batch_number, expiry_date, quantity, transaction_time)
                VALUES ('tx-backdated', '{FacilityId}', 'prod-1', 'user-1', 'StockIn',
                        'B2401', 0, 300, 100);
                """;
            command.ExecuteNonQuery();
        }

        var second = await new SyncPayloadBuilder(_repository).BuildAsync(FacilityId);

        Assert.Equal("tx-backdated", Assert.Single(second.Transactions).TransactionId);
    }
}
