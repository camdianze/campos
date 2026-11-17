namespace CamPOS.Application.Usage;

/// <summary>
/// 기록하는 이벤트의 <b>전부</b>. 여기 없는 이름은 기록되지 않는다.
///
/// 목록을 고정해 두는 이유는 두 가지다. 하나는 약국과의 약속으로,
/// docs/usage-events-consent.md가 "무엇이 기록되는가"를 적어 두고 약국이 그것을
/// 읽고 서명한다 — 호출부에서 문자열을 자유롭게 지어낼 수 있으면 그 문장은
/// 지켜지는지 확인할 방법이 없는 약속이 된다. 다른 하나는 오타다. 이름이 한 글자
/// 틀리면 그 기능은 지표에서 사라지는데, 화면에도 서버에도 아무 표시가 없다 —
/// 집계가 0이 아니라 그냥 조금 작아질 뿐이라 몇 달 뒤에도 눈치채지 못한다.
///
/// 새 이름을 더하려면 <see cref="All"/>에도 더해야 하고, 동의서도 같이 고쳐야 한다.
/// </summary>
public static class UsageEventNames
{
    // ── 화면 열기 ───────────────────────────────────────────────────────────
    // outcome은 언제나 Shown이다. 화면은 성공하거나 실패하지 않는다.

    public const string Sales = "sales";
    public const string StockIn = "stock_in";
    public const string Inventory = "inventory";
    public const string Alerts = "alerts";
    public const string Reports = "reports";
    public const string SalesHistory = "sales_history";
    public const string StockHistory = "stock_history";
    public const string Products = "products";
    public const string DataCenter = "data_center";

    // 관리자 화면. 약국 주인만 여는 자리라, 여기가 0이면 제품이 일과에는
    // 들어갔는데 관리는 안 되고 있다는 뜻이다 — 설정이 비어 있는 채로 쓰이는
    // 약국을 찾는 길이기도 하다.
    public const string AdminDashboard = "admin_dashboard";
    public const string UserManagement = "user_management";

    /// <summary>대시보드 안의 영수증 설정 섹션을 펼쳤다. 독립 화면이 아니다.</summary>
    public const string ReceiptSettings = "receipt_settings";

    public const string CounsellingSettings = "counselling_settings";

    // ── 행동 ────────────────────────────────────────────────────────────────
    // 성공하면 Success, 실패하면 Failure. 실패를 별도 이름으로 두지 않는 이유는
    // UsageOutcome에 적어 두었다.

    public const string SaleCompleted = "sale_completed";
    public const string StockInSaved = "stock_in_saved";
    public const string AdjustmentSaved = "adjustment_saved";
    public const string RefundSaved = "refund_saved";
    public const string BackupCreated = "backup_created";
    public const string SyncManual = "sync_manual";
    public const string ReceiptPrinted = "receipt_printed";
    public const string LabelPrinted = "label_printed";
    public const string ImportCompleted = "import_completed";

    // ── 상품을 어떻게 찾았나 ────────────────────────────────────────────────
    // 스캐너를 실제로 쓰는지, 바코드가 등록돼 있는지, 검색이 더 빠른지 — 셋을
    // 한 쌍으로 답한다.
    //
    // <b>무엇을 찾았는지는 적지 않는다.</b> 찾는 일이 일어났다는 사실과 찾았는지
    // 여부만 남으므로, 상품 정보는 여전히 기록에 없다.

    /// <summary>
    /// 바코드로 찾았다. <c>failure</c>는 <b>찍었는데 없는 상품</b>이고, 그것이
    /// 바코드가 등록되지 않았다는 유일한 신호다.
    /// </summary>
    public const string ProductScanned = "product_scanned";

    /// <summary>이름이나 일부 글자로 찾았다.</summary>
    public const string ProductSearched = "product_searched";

    // ── 알림 ────────────────────────────────────────────────────────────────
    // 알림 화면은 유통기한과 안전재고를 함께 보여주고 필터로 나눈다. 화면을 연 것은
    // Alerts이고, 이 둘은 그 안에서 한쪽을 골라 본 것이다.

    public const string ExpiryAlertOpened = "expiry_alert_opened";
    public const string LowStockAlertOpened = "low_stock_alert_opened";

    // ── 기본 ────────────────────────────────────────────────────────────────

    /// <summary>앱이 켜졌다. 로그인 전이므로 이 행의 role은 비어 있다.</summary>
    public const string AppStarted = "app_started";

    public const string Login = "login";

    /// <summary>
    /// 위의 전부. 기록기가 이 집합에 없는 이름을 거르고, 테스트가 이 목록과
    /// 동의서 문서가 어긋나지 않는지 확인한다.
    /// </summary>
    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        Sales, StockIn, Inventory, Alerts, Reports,
        SalesHistory, StockHistory, Products, DataCenter,

        AdminDashboard, UserManagement, ReceiptSettings, CounsellingSettings,

        SaleCompleted, StockInSaved, AdjustmentSaved, RefundSaved,
        BackupCreated, SyncManual, ReceiptPrinted, LabelPrinted, ImportCompleted,
        ProductScanned, ProductSearched,

        ExpiryAlertOpened, LowStockAlertOpened,

        AppStarted, Login
    };
}
