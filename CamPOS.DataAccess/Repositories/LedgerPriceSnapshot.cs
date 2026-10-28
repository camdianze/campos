using System.Globalization;
using CamPOS.Application.Settings;
using Microsoft.Data.Sqlite;

namespace CamPOS.DataAccess.Repositories;

/// <summary>
/// 원장 한 줄에 "그 순간 원가가 얼마였고 환율이 얼마였는지"를 남긴다.
///
/// 왜 이 두 값만 따로 적는가: 둘 다 <b>거래가 일어난 그 순간에만 알 수 있다.</b>
/// 상품 마스터의 cost_price와 설정의 currency.rate는 현재 값이고 언제든 덮어써지므로,
/// 1년 뒤에 "그때 마진이 얼마였나"를 물으면 채워 넣을 원본이 어디에도 없다.
/// 지나간 거래에는 손쓸 방법이 없다는 점에서 stock_before/after와 같은 종류의 값이고,
/// 그래서 같은 방식(이미 써 넣은 행을 같은 트랜잭션 안에서 갱신)을 쓴다.
///
/// 적지 않았을 때의 실패가 조용하다는 것도 같다 — 분석하는 쪽은 현재 원가로 과거
/// 마진을 내고, 오늘 환율로 작년 매출을 리엘로 환산한다. 둘 다 그럴듯한 숫자가 나와서
/// 틀린 줄을 모른다.
///
/// <b>0은 쓰지 않는다.</b> 원가 0은 "종이 장부만 쓰던 약국이라 매입가를 모른다"는 뜻으로
/// 저장되어 있는 값이고(임포트 규칙: Empty = 0), 그대로 적으면 마진이 100%로 계산된다.
/// 모르는 것은 NULL로 적어야 집계에서 눈에 띈다.
/// </summary>
internal static class LedgerPriceSnapshot
{
    /// <summary>
    /// 상품의 현재 원가. 0이거나 상품이 없으면 null — 0은 "모름"이지 "공짜"가 아니다.
    /// </summary>
    public static async Task<decimal?> ReadCostPriceAsync(
        SqliteConnection connection, SqliteTransaction dbTransaction, string productId)
    {
        using var command = connection.CreateCommand();
        command.Transaction = dbTransaction;
        command.CommandText = "SELECT cost_price FROM Product_Master WHERE product_id = $productId;";
        command.Parameters.AddWithValue("$productId", productId);

        var result = await command.ExecuteScalarAsync();

        if (result is null or DBNull)
        {
            return null;
        }

        var cost = (decimal)Convert.ToDouble(result);

        return cost > 0 ? cost : null;
    }

    /// <summary>
    /// 설정에 들어 있는 1 USD당 리엘. 없거나 0 이하면 null.
    ///
    /// ReceiptSettingsCache를 거치지 않고 App_Setting을 직접 읽는 이유는 둘이다.
    /// 캐시는 10분이라 방금 바꾼 환율이 아직 옛 값일 수 있는데, 이 열은 "그때 설정이
    /// 무엇이었나"의 기록이라 저장된 값이 맞다. 그리고 이 갱신은 판매 트랜잭션 안에서
    /// 일어나므로, 같은 연결로 읽어야 트랜잭션 바깥의 값을 섞지 않는다.
    ///
    /// InvariantCulture로 읽는 것은 쓰는 쪽(ReceiptSettingsService)이 그렇게 쓰기
    /// 때문이다. PC 지역 설정을 따르면 소수점이 천 단위 구분자로 읽혀 4.1이 41이 된다.
    /// </summary>
    public static async Task<decimal?> ReadExchangeRateAsync(
        SqliteConnection connection, SqliteTransaction dbTransaction)
    {
        using var command = connection.CreateCommand();
        command.Transaction = dbTransaction;
        command.CommandText = "SELECT setting_value FROM App_Setting WHERE setting_key = $key;";
        command.Parameters.AddWithValue("$key", AppSettingKeys.CurrencyRate);

        var result = await command.ExecuteScalarAsync();

        if (result is null or DBNull)
        {
            return null;
        }

        var parsed = decimal.TryParse(
            result.ToString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var rate);

        return parsed && rate > 0 ? rate : null;
    }

    /// <summary>이미 써 넣은 원장 행에 그 순간의 원가와 환율을 붙인다.</summary>
    public static async Task RecordAsync(
        SqliteConnection connection, SqliteTransaction dbTransaction,
        string transactionId, decimal? costPrice, decimal? exchangeRate)
    {
        using var command = connection.CreateCommand();
        command.Transaction = dbTransaction;
        command.CommandText = """
            UPDATE Stock_Transaction
            SET cost_price_at_transaction = $costPrice,
                exchange_rate_at_transaction = $exchangeRate
            WHERE transaction_id = $transactionId;
            """;
        command.Parameters.AddWithValue("$transactionId", transactionId);
        command.Parameters.AddWithValue("$costPrice", (object?)costPrice ?? DBNull.Value);
        command.Parameters.AddWithValue("$exchangeRate", (object?)exchangeRate ?? DBNull.Value);

        await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// 환불 행에는 <b>원 판매가 들고 있던 값을 그대로 옮긴다.</b> 환불은 그 판매를
    /// 되돌리는 것이므로 수량과 금액이 음수로 상쇄되듯 원가와 환율도 같은 값이라야
    /// 상쇄된다. 환불 시점의 현재 원가를 읽으면, 그 사이 매입가가 오른 상품의
    /// 마진이 환불 한 건으로 뒤틀린다.
    ///
    /// 원 거래가 이 열이 생기기 전의 행이면 양쪽 다 NULL이고, 그것이 맞다 —
    /// 짝이 없는 쪽을 지금 값으로 채우면 상쇄되지 않는다.
    /// </summary>
    public static async Task CopyFromOriginalAsync(
        SqliteConnection connection, SqliteTransaction dbTransaction,
        string refundTransactionId, string originalTransactionId)
    {
        using var command = connection.CreateCommand();
        command.Transaction = dbTransaction;
        command.CommandText = """
            UPDATE Stock_Transaction
            SET cost_price_at_transaction =
                    (SELECT cost_price_at_transaction FROM Stock_Transaction
                     WHERE transaction_id = $originalId),
                exchange_rate_at_transaction =
                    (SELECT exchange_rate_at_transaction FROM Stock_Transaction
                     WHERE transaction_id = $originalId)
            WHERE transaction_id = $refundId;
            """;
        command.Parameters.AddWithValue("$refundId", refundTransactionId);
        command.Parameters.AddWithValue("$originalId", originalTransactionId);

        await command.ExecuteNonQueryAsync();
    }
}
