using System.Text.Json.Serialization;

namespace PharmaPOS.Application.Sync;

/// <summary>
/// 서버로 보내는 한 묶음.
///
/// Domain이 아니라 Application에 두는 이유: 이 모양은 약국의 개념이 아니라 서버가
/// 받아들이는 형식이다. Domain은 약국의 말(상품·재고·거래)만 담고, 그것을 무엇으로
/// 포장해 보내는지는 바깥 세계와의 약속이라 Application에 속한다.
///
/// <b>무엇이 들어가고 무엇이 빠지는지가 이 파일의 전부다.</b> 빠져야 할 것이 들어가는
/// 사고는 조용하다 — JSON이 커질 뿐 화면에는 아무 표시가 없고, 보낸 뒤에는 되돌릴 수
/// 없다. 그래서 각 줄이 자기가 담을 것을 직접 적고, SyncPayloadTests가 그 밖의 것이
/// 새어 들어오면 실패한다.
/// </summary>
public sealed class SyncPayload
{
    /// <summary>이 묶음을 만든 앱 버전. 서버가 어떤 버전이 보냈는지 기록한다.</summary>
    public required string AppVersion { get; init; }

    /// <summary>묶음을 만든 시각(Unix epoch 밀리초). PC 시계 기준이다.</summary>
    public required long ClientTime { get; init; }

    public IReadOnlyList<SyncProduct> Products { get; init; } = [];
    public IReadOnlyList<SyncInventory> Inventory { get; init; } = [];
    public IReadOnlyList<SyncUser> Users { get; init; } = [];
    public IReadOnlyList<SyncTransaction> Transactions { get; init; } = [];
    public IReadOnlyList<SyncCounsellingLog> CounsellingLogs { get; init; } = [];

    /// <summary>
    /// 이 묶음을 성공적으로 보냈을 때 Sync_State에 적을 위치.
    /// 스트림 이름 → 마지막으로 담은 위치(거래·상담은 rowid).
    /// </summary>
    public IReadOnlyDictionary<string, long> Positions { get; init; }
        = new Dictionary<string, long>();

    /// <summary>
    /// 화면에 "몇 건"을 적기 위한 합계. <b>보내지 않는다.</b>
    ///
    /// 서버는 받은 행을 직접 셀 수 있으므로 이 숫자는 전송에 쓸모가 없고, 보내면
    /// 오히려 해롭다 — 아무도 대조하지 않는 숫자가 하나 늘어난다. 게다가 서버는
    /// 모르는 이름이 오면 묶음 전체를 거부하므로(그래야 열이 조용히 비지 않는다),
    /// 이 계산값이 JSON에 섞이면 업로드가 통째로 실패한다.
    /// </summary>
    [JsonIgnore]
    public int RowCount =>
        Products.Count + Inventory.Count + Users.Count + Transactions.Count + CounsellingLogs.Count;
}

/// <summary>
/// 상품 한 줄. 사진은 들어가지 않는다 — 장당 수백 KB라 한 묶음이 수백 MB가 되고,
/// 사진은 바뀌는 일이 드물어 매번 보낼 이유도 없다. 사진은 자기 단계에서 따로 올린다.
/// </summary>
public sealed class SyncProduct
{
    public required string ProductId { get; init; }
    public string? Barcode { get; init; }
    public string? InternalBarcode { get; init; }
    public string? UnitBarcode { get; init; }
    public required string ProductName { get; init; }
    public string? GenericName { get; init; }
    public string? Strength { get; init; }
    public string? DosageForm { get; init; }
    public required string Unit { get; init; }
    public required int UnitsPerBox { get; init; }
    public required bool SellsLoose { get; init; }
    public string? Manufacturer { get; init; }
    public string? CountryOfOrigin { get; init; }
    public string? AtcCode { get; init; }
    public required bool IsCombination { get; init; }
    public string? Category { get; init; }
    public required int SafetyStockLevel { get; init; }
    public required string Status { get; init; }

    // 금액. 제외했다가 되돌린 결정이다 — 서버는 원본을 그대로 보관하고, 무엇을 빼고
    // 무엇을 가공할지는 나중에 읽어 가는 쪽에서 정한다.
    public required decimal CostPrice { get; init; }
    public required decimal SellingPrice { get; init; }
    public decimal? UnitSellingPrice { get; init; }

    // 리엘로 적어 넣은 가격. 달러만 보내면 약국이 정한 리엘 금액이 서버에서 사라지고,
    // 환율이 바뀐 뒤에는 어떤 계산으로도 되살릴 수 없다.
    public decimal? SellingPriceKhr { get; init; }
    public decimal? CostPriceKhr { get; init; }
    public decimal? UnitSellingPriceKhr { get; init; }

    public required long CreatedAt { get; init; }
    public long? UpdatedAt { get; init; }
}

public sealed class SyncInventory
{
    public required string InventoryId { get; init; }
    public required string ProductId { get; init; }
    public required string BatchNumber { get; init; }

    /// <summary>0은 "유통기한 모름"이다. 날짜로 바꾸지 않고 그대로 보낸다.</summary>
    public required long ExpiryDate { get; init; }

    public required int CurrentQuantity { get; init; }
    public required int BoxQuantity { get; init; }
    public required int UnitQuantity { get; init; }
    public required long UpdatedAt { get; init; }
}

/// <summary>
/// 직원. 화면에 "누가 처리했는지"를 쓰기 위한 최소한만 보낸다.
/// <b>비밀번호 해시, 보안 질문과 답, 복구 이메일, 이메일 설정은 담을 자리조차 두지 않는다.</b>
/// 속성이 없으면 실수로 채울 수도 없다.
/// </summary>
public sealed class SyncUser
{
    public required string UserId { get; init; }
    public required string Username { get; init; }
    public required string Role { get; init; }
    public required string Status { get; init; }
}

/// <summary>
/// 원장 한 줄. 금액을 포함해 원본 그대로 보낸다.
/// 환불 행의 quantity와 total_amount는 음수이며, 그 부호까지 그대로 둔다 —
/// 순판매량은 StockOut과 Refund를 함께 세야 나오고, 그 계산은 읽는 쪽이 한다.
/// </summary>
public sealed class SyncTransaction
{
    public required string TransactionId { get; init; }
    public required string ProductId { get; init; }
    public required string UserId { get; init; }
    public required string TransactionType { get; init; }
    public required string BatchNumber { get; init; }
    public required long ExpiryDate { get; init; }
    public required int Quantity { get; init; }
    public decimal? SellingPriceAtTransaction { get; init; }
    public decimal? TotalAmount { get; init; }
    public string? PaymentMethod { get; init; }
    public string? Reason { get; init; }
    public string? RelatedTransactionId { get; init; }

    /// <summary>이 컬럼이 생기기 전 행은 null이다. 0으로 바꾸면 "재고가 0이었다"로 읽힌다.</summary>
    public int? StockBefore { get; init; }

    /// <inheritdoc cref="StockBefore"/>
    public int? StockAfter { get; init; }

    public required long TransactionTime { get; init; }
}

public sealed class SyncCounsellingLog
{
    public required string LogId { get; init; }
    public required string TransactionId { get; init; }
    public required string ProductId { get; init; }
    public string? AtcCode { get; init; }
    public required string AwareGroup { get; init; }
    public required bool Printed { get; init; }
    public string? SkipReason { get; init; }
    public required string Locale { get; init; }
    public string? SourceVersion { get; init; }
    public required long CreatedAt { get; init; }
}

/// <summary>Sync_State의 스트림 이름. 문자열을 흩어 두면 오타가 조용히 동기화를 멈춘다.</summary>
public static class SyncStreams
{
    public const string Transactions = "stock_transaction";
    public const string CounsellingLogs = "counselling_log";
    public const string Photos = "product_photo";
}
