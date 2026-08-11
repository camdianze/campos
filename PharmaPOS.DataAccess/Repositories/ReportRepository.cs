using Microsoft.Data.Sqlite;
using PharmaPOS.Application.Reports;
using PharmaPOS.Application.Repositories;
using PharmaPOS.DataAccess.Database;

namespace PharmaPOS.DataAccess.Repositories;

/// <summary>
/// IReportRepository의 SQLite 구현체.
///
/// 세 쿼리 모두 같은 모양이다: WHERE로 "직전 기간 시작 ~ 현재 기간 끝"을 한 번에 훑고,
/// SELECT 안의 CASE WHEN으로 두 기간을 갈라 합산한다. 두 기간이 시간축에서 맞붙어 있어
/// 가능한 방식이며, 조회를 한 번만 하므로 비교값이 서로 다른 시점을 가리킬 일이 없다.
/// </summary>
public class ReportRepository : IReportRepository
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public ReportRepository(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<(SalesTotals Current, SalesTotals Previous)> GetSalesTotalsAsync(
        string facilityId, ReportRange range)
    {
        using var connection = _connectionFactory.CreateOpenConnection();

        using var command = connection.CreateCommand();

        // 거래 건수는 판매 헤더 테이블이 없어 "판매 시각 + 판매자" 조합의 가짓수로 센다
        // (판매 내역 화면이 한 거래를 묶는 방식과 같다).
        //
        // 환불 행은 수량·금액이 음수로 쌓이므로 금액·수량은 함께 더해 순매출로 만들고,
        // 건수만 판매 행으로 한정한다. 환불을 한 건으로 세면 "500건 팔았다"가 부풀고,
        // 전액 환불한 판매를 빼 버리면 그날 손님이 몇 번 왔는지가 사라진다.
        command.CommandText = """
            SELECT
                COALESCE(SUM(CASE WHEN transaction_time BETWEEN $curFrom AND $curTo
                                  THEN total_amount END), 0),
                COALESCE(SUM(CASE WHEN transaction_time BETWEEN $curFrom AND $curTo
                                  THEN quantity END), 0),
                COUNT(DISTINCT CASE WHEN transaction_time BETWEEN $curFrom AND $curTo
                                     AND transaction_type = 'StockOut'
                                    THEN transaction_time || '|' || user_id END),
                COALESCE(SUM(CASE WHEN transaction_time BETWEEN $prevFrom AND $prevTo
                                  THEN total_amount END), 0),
                COALESCE(SUM(CASE WHEN transaction_time BETWEEN $prevFrom AND $prevTo
                                  THEN quantity END), 0),
                COUNT(DISTINCT CASE WHEN transaction_time BETWEEN $prevFrom AND $prevTo
                                     AND transaction_type = 'StockOut'
                                    THEN transaction_time || '|' || user_id END)
            FROM Stock_Transaction
            WHERE facility_id = $facilityId
              AND transaction_type IN ('StockOut', 'Refund')
              AND transaction_time BETWEEN $prevFrom AND $curTo;
            """;
        AddRangeParameters(command, facilityId, range);

        using var reader = await command.ExecuteReaderAsync();
        await reader.ReadAsync();

        var current = new SalesTotals
        {
            Amount = (decimal)reader.GetDouble(0),
            ItemCount = reader.GetInt32(1),
            TransactionCount = reader.GetInt32(2)
        };

        var previous = new SalesTotals
        {
            Amount = (decimal)reader.GetDouble(3),
            ItemCount = reader.GetInt32(4),
            TransactionCount = reader.GetInt32(5)
        };

        return (current, previous);
    }

    public async Task<IReadOnlyList<ProductSalesRow>> GetProductSalesAsync(
        string facilityId, ReportRange range)
    {
        using var connection = _connectionFactory.CreateOpenConnection();

        using var command = connection.CreateCommand();

        // 상품이 지워졌을 수도 있어 LEFT JOIN이고, 이름이 없으면 product_id로 대신한다.
        // 환불 행(음수)을 함께 더해 상품별 순판매를 낸다.
        command.CommandText = """
            SELECT
                st.product_id,
                COALESCE(p.product_name, st.product_id) AS product_name,
                COALESCE(p.generic_name, '')            AS generic_name,
                COALESCE(p.strength, '')                AS strength,
                COALESCE(SUM(CASE WHEN st.transaction_time BETWEEN $curFrom AND $curTo
                                  THEN st.quantity END), 0)     AS cur_quantity,
                COALESCE(SUM(CASE WHEN st.transaction_time BETWEEN $curFrom AND $curTo
                                  THEN st.total_amount END), 0) AS cur_amount,
                COALESCE(SUM(CASE WHEN st.transaction_time BETWEEN $prevFrom AND $prevTo
                                  THEN st.quantity END), 0)     AS prev_quantity,
                COALESCE(SUM(CASE WHEN st.transaction_time BETWEEN $prevFrom AND $prevTo
                                  THEN st.total_amount END), 0) AS prev_amount
            FROM Stock_Transaction st
            LEFT JOIN Product_Master p ON p.product_id = st.product_id
            WHERE st.facility_id = $facilityId
              AND st.transaction_type IN ('StockOut', 'Refund')
              AND st.transaction_time BETWEEN $prevFrom AND $curTo
            GROUP BY st.product_id, product_name, generic_name, strength
            ORDER BY cur_amount DESC, cur_quantity DESC, product_name;
            """;
        AddRangeParameters(command, facilityId, range);

        using var reader = await command.ExecuteReaderAsync();

        var results = new List<ProductSalesRow>();
        while (await reader.ReadAsync())
        {
            results.Add(new ProductSalesRow
            {
                ProductId = reader.GetString(0),
                ProductName = reader.GetString(1),
                GenericName = reader.GetString(2),
                Strength = reader.GetString(3),
                Quantity = reader.GetInt32(4),
                Amount = (decimal)reader.GetDouble(5),
                PreviousQuantity = reader.GetInt32(6),
                PreviousAmount = (decimal)reader.GetDouble(7)
            });
        }

        return results;
    }

    public async Task<IReadOnlyList<AntibioticSalesRow>> GetAntibioticSalesAsync(
        string facilityId, ReportRange range)
    {
        using var connection = _connectionFactory.CreateOpenConnection();

        using var command = connection.CreateCommand();

        // 복약안내 로그와 판매 기록을 transaction_id로 잇는다. 로그는 판매 라인 하나당
        // 정확히 한 행이라 수량이 부풀지 않는다.
        //
        // 기간 판정을 로그의 created_at이 아니라 판매 시각으로 하는 이유: 그래야 이 표의
        // 매출이 위쪽 기간 매출의 부분집합이 된다. 둘을 섞으면 자정 근처의 판매가
        // 한쪽 표에만 잡혀 합이 맞지 않는다.
        //
        // 여기만 환불(음수 행)을 빼지 않고 StockOut만 본다. 이 표가 세는 것은 매출이 아니라
        // "항생제가 손님 손에 몇 번 나갔고 그 중 몇 번에 복약안내를 줬는가"이고,
        // 돈을 돌려줬다고 해서 이미 나간 항생제와 그때 한 안내가 없던 일이 되지는 않는다.
        // 그래서 이 표의 금액은 위쪽 순매출과 달리 총매출 기준이다.
        command.CommandText = """
            SELECT
                COALESCE(NULLIF(TRIM(COALESCE(p.generic_name, '')), ''),
                         COALESCE(p.product_name, cl.product_id)) AS ingredient,
                COALESCE(NULLIF(TRIM(COALESCE(p.strength, '')), ''), '') AS strength,
                cl.aware_group,
                COALESCE(SUM(CASE WHEN st.transaction_time BETWEEN $curFrom AND $curTo
                                  THEN st.quantity END), 0)     AS cur_quantity,
                COALESCE(SUM(CASE WHEN st.transaction_time BETWEEN $curFrom AND $curTo
                                  THEN st.total_amount END), 0) AS cur_amount,
                SUM(CASE WHEN st.transaction_time BETWEEN $curFrom AND $curTo
                         THEN 1 ELSE 0 END)                      AS cur_sales,
                SUM(CASE WHEN st.transaction_time BETWEEN $curFrom AND $curTo AND cl.printed = 1
                         THEN 1 ELSE 0 END)                      AS cur_printed,
                COALESCE(SUM(CASE WHEN st.transaction_time BETWEEN $prevFrom AND $prevTo
                                  THEN st.quantity END), 0)     AS prev_quantity,
                COALESCE(SUM(CASE WHEN st.transaction_time BETWEEN $prevFrom AND $prevTo
                                  THEN st.total_amount END), 0) AS prev_amount,
                SUM(CASE WHEN st.transaction_time BETWEEN $prevFrom AND $prevTo
                         THEN 1 ELSE 0 END)                      AS prev_sales,
                SUM(CASE WHEN st.transaction_time BETWEEN $prevFrom AND $prevTo AND cl.printed = 1
                         THEN 1 ELSE 0 END)                      AS prev_printed
            FROM Counselling_Log cl
            JOIN Stock_Transaction st ON st.transaction_id = cl.transaction_id
            LEFT JOIN Product_Master p ON p.product_id = cl.product_id
            WHERE st.facility_id = $facilityId
              AND st.transaction_type = 'StockOut'
              AND st.transaction_time BETWEEN $prevFrom AND $curTo
            GROUP BY ingredient, strength, cl.aware_group
            ORDER BY cur_quantity DESC, cur_amount DESC, ingredient;
            """;
        AddRangeParameters(command, facilityId, range);

        using var reader = await command.ExecuteReaderAsync();

        var results = new List<AntibioticSalesRow>();
        while (await reader.ReadAsync())
        {
            results.Add(new AntibioticSalesRow
            {
                Ingredient = reader.GetString(0),
                Strength = reader.GetString(1),
                AwareGroup = reader.GetString(2),
                Quantity = reader.GetInt32(3),
                Amount = (decimal)reader.GetDouble(4),
                SaleCount = reader.GetInt32(5),
                CounsellingPrinted = reader.GetInt32(6),
                PreviousQuantity = reader.GetInt32(7),
                PreviousAmount = (decimal)reader.GetDouble(8),
                PreviousSaleCount = reader.GetInt32(9),
                PreviousCounsellingPrinted = reader.GetInt32(10)
            });
        }

        return results;
    }

    private static void AddRangeParameters(SqliteCommand command, string facilityId, ReportRange range)
    {
        command.Parameters.AddWithValue("$facilityId", facilityId);
        command.Parameters.AddWithValue("$curFrom", range.FromUtc);
        command.Parameters.AddWithValue("$curTo", range.ToUtc);
        command.Parameters.AddWithValue("$prevFrom", range.PreviousFromUtc);
        command.Parameters.AddWithValue("$prevTo", range.PreviousToUtc);
    }
}
