# CamPOS 최적화 진단 보고서

작성일 2026-09-18 · 기준 커밋 `4fec9af` · **코드 수정 없음, 분석만**

방법: 정규식 스캔으로 후보를 뽑은 뒤 후보마다 `.cs`·`.xaml` 전체를 다시 검색해 손으로 확인했다. 아래 표의 모든 줄번호는 확인 시점의 파일 기준이다. 실행 중 측정(프로파일링·메모리 스냅샷)은 하지 않았으며, 그런 확인이 필요한 항목은 **미확인**으로 남겼다.

규모: Domain 20 파일 / Application 189 / DataAccess 24 / Wpf 81 cs + 31 xaml / Tests 35. 총 약 41,000줄.

---

## 1. 유령코드 후보

확신도 기준 — **확실**: 코드·XAML·테스트 어디에도 참조가 없고 삭제해도 빌드·동작이 바뀌지 않음이 확인됨. **검토 필요**: 참조는 없으나 의도(예: 인터페이스 대칭성)가 있어 보임. **삭제 불가**: XAML 참조가 하나라도 있음.

### 1-1. 확실

| # | 위치 | 대상 | 근거 | XAML 검색 결과 | 위험도 |
|---|---|---|---|---|---|
| G1 | [PharmaPOS.Wpf/Views/AdjustmentView.xaml](../PharmaPOS.Wpf/Views/AdjustmentView.xaml), [AdjustmentView.xaml.cs](../PharmaPOS.Wpf/Views/AdjustmentView.xaml.cs), [ViewModels/AdjustmentViewModel.cs](../PharmaPOS.Wpf/ViewModels/AdjustmentViewModel.cs) — **531줄** | 별도 조정 화면 전체 | `new AdjustmentView(`·`new AdjustmentViewModel(`이 코드베이스 어디에도 없음. 참조는 이 세 파일 안의 자기 참조(`x:Class`, `AttachViewModel(AdjustmentViewModel)`, `DataContext is AdjustmentViewModel`)뿐. 조정 기능은 `72494c2`에서 [InventoryStatusViewModel.cs:458](../PharmaPOS.Wpf/ViewModels/InventoryStatusViewModel.cs#L458) 인라인 패널로 옮겨졌고, 이 화면은 그때 지워지지 않고 남은 것 | `AdjustmentView`: `AdjustmentView.xaml:1` (자기 `x:Class`)만. `AdjustmentViewModel`: XAML 0건 | **중** — 두 벌이 남아 있으면 다음에 조정을 손볼 때 죽은 쪽을 고칠 위험 |
| G2 | [PharmaPOS.Wpf/MainWindow.xaml.cs:24-27](../PharmaPOS.Wpf/MainWindow.xaml.cs#L24) | `LoginView_Loaded` 빈 핸들러 | 본문이 비어 있고, `MainWindow.xaml`에 `Loaded=` 연결이 없음 | `LoginView_Loaded`: XAML 0건 | 하 |
| G3 | [PharmaPOS.Wpf/MainWindow.xaml.cs:1-10](../PharmaPOS.Wpf/MainWindow.xaml.cs#L1) | 템플릿 잔재 `using` 6개 (`System.Text`, `System.Windows.Data`, `.Documents`, `.Media.Imaging`, `.Navigation`, `.Shapes`) | 파일 본문(28줄)에 해당 네임스페이스 타입 사용 없음 | 해당 없음 | 하 |
| G4 | [PharmaPOS.Wpf/Composition/ServiceCollectionExtensions.cs:17](../PharmaPOS.Wpf/Composition/ServiceCollectionExtensions.cs#L17) | `using PharmaPOS.Application.Security;` 중복 (12행과 동일) | 빌드마다 `CS0105` 경고 2건의 출처. CLAUDE.md가 "pre-existing, harmless"로 기록 | 해당 없음 | 하 |
| G5 | [PharmaPOS.Wpf/App.xaml:645](../PharmaPOS.Wpf/App.xaml#L645) | 리소스 `KhmerFont` | `App.xaml` 46개 `x:Key` 중 다른 파일에서 참조 0건인 유일한 것. 글꼴을 창 전체 폴백([MainWindow.xaml:10](../PharmaPOS.Wpf/MainWindow.xaml#L10))으로 옮기면서 사용처가 사라짐 | `{StaticResource KhmerFont}`: 0건 | 하 |
| G6 | [PharmaPOS.Wpf/Converters.zip](../PharmaPOS.Wpf/Converters.zip) (86,740 bytes, git 추적 중) | 흘러들어온 압축 파일 | `.csproj`에 항목 없음, 빌드 입력 아님. CLAUDE.md가 "stray artifact"로 기록 | 해당 없음 | 하 |

### 1-2. 검토 필요

| # | 위치 | 대상 | 근거 | XAML 검색 결과 | 위험도 |
|---|---|---|---|---|---|
| G7 | [PharmaPOS.Application/Repositories/IReceiptNumberRepository.cs:12](../PharmaPOS.Application/Repositories/IReceiptNumberRepository.cs#L12), 구현 [ReceiptNumberRepository.cs:22](../PharmaPOS.DataAccess/Repositories/ReceiptNumberRepository.cs#L22) | `FindAsync(saleKey)` | 호출자 0건(테스트 포함). 같은 인터페이스의 `IssueAsync`가 "이미 붙은 번호가 있으면 그것을 돌려준다"를 겸하고 있어 역할이 겹침. 다만 "재출력은 번호를 소비하지 않는다"는 규칙을 읽기 전용으로 표현하는 자리라, 의도적으로 남긴 대칭 API일 수 있음 | 0건 | 하 |
| G8 | [PharmaPOS.Application/Reports/AntibioticSalesRow.cs:44](../PharmaPOS.Application/Reports/AntibioticSalesRow.cs#L44) | `CounsellingChange` 프로퍼티 | 코드·XAML·테스트 참조 0건. 같은 행의 `QuantityChange`는 [ReportsView.xaml:312](../PharmaPOS.Wpf/Views/ReportsView.xaml#L312)에 바인딩되고 `AmountChange`·`QuantityShare`는 [ReportsViewModel.cs:448-461](../PharmaPOS.Wpf/ViewModels/ReportsViewModel.cs#L448)(CSV 내보내기)에서 쓰이는데, `CounsellingChange`만 어느 쪽에도 없음 — 화면·CSV에서 빠졌거나, 빠뜨린 것일 수 있음 | `CounsellingChange`: 0건 | 하 |
| G9 | [PharmaPOS.Wpf/ViewModels/AntibioticTrendBar.cs:32](../PharmaPOS.Wpf/ViewModels/AntibioticTrendBar.cs#L32) | `HasSales` 프로퍼티 | 참조 0건. 이름이 비슷한 `HasSalesTrendData`([ReportsViewModel.cs:193](../PharmaPOS.Wpf/ViewModels/ReportsViewModel.cs#L193))는 XAML 2곳에서 쓰이므로 혼동 주의 | `HasSales`(단독): 0건 | 하 |

### 1-3. 삭제 불가 (스캔에는 잡히지만 XAML이 쓰는 것)

정규식 스캔이 "미참조"로 올린 47개 `private` 메서드는 **전부 XAML 이벤트 핸들러**였다(`Click=`, `KeyDown=`, `PreviewMouseRightButtonDown=`, `ContextMenuOpening=`, `SelectedItemChanged=`, `Checked=`). 대표 예:

| 위치 | 대상 | XAML 참조 |
|---|---|---|
| [Shell/MainShellView.xaml.cs:86-185](../PharmaPOS.Wpf/Shell/MainShellView.xaml.cs#L86) | `OnProductsClick` 외 8개 | `MainShellView.xaml` 각 1건 |
| [Views/InventoryStatusView.xaml.cs:50,73,89](../PharmaPOS.Wpf/Views/InventoryStatusView.xaml.cs#L50) | `OnTreeSelectedItemChanged`, `OnTreeRightButtonDown`, `OnTreeContextMenuOpening` | 각 1건 |
| [Views/PosSaleView.xaml.cs:28,62](../PharmaPOS.Wpf/Views/PosSaleView.xaml.cs#L28) | `OnPreviewTextInput`, `OnSearchBoxKeyDown` | 1건 / 2건 |
| [Controls/LanguageToggle.xaml.cs:72](../PharmaPOS.Wpf/Controls/LanguageToggle.xaml.cs#L72) | `OnLanguageChecked` | 1건 |

타입 스캔에서 "다른 파일 참조 없음"으로 올라온 것들도 모두 오탐이다: `ServiceCollectionExtensions`(확장 메서드 `AddPharmaPosServices()`로 호출), `LocExtension`(XAML은 `svc:Loc`으로 줄여 부름), `LocalizedString`(같은 파일에서 생성), `SaleUnitOption`·`ProductStatusFilterOption`·`ProductSortOption`·`LocaleOption`(각 ViewModel 파일 안에서만 쓰는 열거형·레코드). 6개 Converter는 전부 XAML 참조가 있다.

### 1-4. 그 외 확인한 것

- 주석 처리된 코드 블록(`//` 3줄 이상 연속, 세미콜론·중괄호 포함): **0건**.
- `TODO`: [SaleService.cs:69](../PharmaPOS.Application/Inventory/SaleService.cs#L69) 1건 (판매 `notes` 컬럼 없음 — CLAUDE.md에 기록된 알려진 미구현).
- 도달 불가 코드(`return` 뒤 문장, `#if false`): 스캔에서 0건. 컴파일러 경고에도 `CS0162` 없음.

---

## 2. 의존성

### 2-1. NuGet

| 패키지 | 선언 위치 | 사용 파일 수 | 판단 |
|---|---|---|---|
| `BCrypt.Net-Next 4.2.0` | Application | 1 (`BCryptPasswordHasher`) | 사용 중 |
| `ClosedXML 0.105.0` | DataAccess | 2 (`BackupRepository`, `ImportFileReader`) | 사용 중 |
| `Microsoft.Data.Sqlite 10.0.9` | DataAccess | 25 | 사용 중 |
| `Microsoft.Extensions.DependencyInjection 10.0.9` | Wpf | 15 | 사용 중 |
| `xunit`, `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk`, `coverlet.collector` | Tests | — | 테스트 인프라. `coverlet.collector`는 커버리지 수집용이며 현재 커버리지를 재는 명령이 문서에 없음 — **미확인**(쓰는 절차가 있는지) |

**미사용 패키지 없음.**

### 2-2. 리소스

| 리소스 | 선언 | 참조 | 판단 |
|---|---|---|---|
| `Fonts/AbrilFatface-Regular.ttf` | csproj:34 | [App.xaml:88](../PharmaPOS.Wpf/App.xaml#L88) `#Abril Fatface` | 사용 중 (로그인 제목) |
| `Fonts/Siemreap.ttf` | csproj:38 | `App.xaml:645`(G5, 미참조 리소스 안), [MainWindow.xaml:10](../PharmaPOS.Wpf/MainWindow.xaml#L10) | 사용 중 (화면 전체 폴백) |
| `Fonts/Battambang-Regular/Bold.ttf` | csproj:39-40 | [ThermalTextPrinter.cs:32](../PharmaPOS.Wpf/Services/ThermalTextPrinter.cs#L32), [AdminDashboardView.xaml:56,324](../PharmaPOS.Wpf/Views/AdminDashboardView.xaml#L56) | 사용 중 (인쇄·미리보기) |
| `OFL*.txt` ×3 | csproj:41-43 | 라이선스 동봉 | 필요 |
| `seeds/`, `locales/` | csproj:25-26 | 런타임 로드 | 필요 |
| `Converters.zip` | 선언 없음 | 없음 | **G6** |
| 이미지(png/ico/jpg) | 없음 | — | 이미지 리소스 자체가 없음 (아이콘은 전부 이모지 텍스트) |
| ResourceDictionary | `App.xaml` 단일 | 병합 사전 없음 | 해당 없음 |

---

## 3. 데이터 접근

### 3-1. 인덱스 없이 WHERE / ORDER BY에 쓰이는 컬럼

`Stock_Transaction`에 걸린 인덱스는 셋뿐이다: `related_transaction_id`([DatabaseInitializer.cs:136](../PharmaPOS.DataAccess/Database/DatabaseInitializer.cs#L136)), `product_id`(:492), `transaction_type`(:494).

| # | 컬럼 | 조건으로 쓰이는 곳 (질의 수) | 근거 | 위험도 |
|---|---|---|---|---|
| D1 | `Stock_Transaction.transaction_time` | **36곳** — 정렬·범위 조건. [SalesHistoryRepository.cs:42,48,85](../PharmaPOS.DataAccess/Repositories/SalesHistoryRepository.cs#L42) · [StockHistoryRepository.cs:47,53,81](../PharmaPOS.DataAccess/Repositories/StockHistoryRepository.cs#L47) · [ReportRepository.cs](../PharmaPOS.DataAccess/Repositories/ReportRepository.cs) 24곳 (:41-58, :97-118, :167-188, :244, :311) · [AdminDashboardRepository.cs:32](../PharmaPOS.DataAccess/Repositories/AdminDashboardRepository.cs#L32) · [BackupRepository.cs:66-67,116](../PharmaPOS.DataAccess/Repositories/BackupRepository.cs#L66) · [CounsellingLogRepository.cs:62](../PharmaPOS.DataAccess/Repositories/CounsellingLogRepository.cs#L62) | 판매 이력·재고 이력·보고서·관리자 대시보드가 모두 `ORDER BY transaction_time DESC` 또는 기간 범위로 이 테이블을 읽는데 인덱스가 없다. 지금은 397행이라 체감이 없지만, 원장은 **삭제되지 않고 쌓이는 테이블**이라 기간 조회가 전수 스캔 + 정렬이 된다 | **상** (장기 운영 시) |
| D2 | `Stock_Transaction.facility_id` | 20곳 — 거의 모든 원장 질의의 첫 조건 | 단일 시설이라 선택도는 0이지만, 위 정렬과 결합한 `(facility_id, transaction_time)` 복합 인덱스 하나면 D1까지 함께 해결된다 | 상 (D1과 묶임) |
| D3 | `Stock_Transaction (transaction_time, user_id)` | [SalesHistoryRepository.cs:85](../PharmaPOS.DataAccess/Repositories/SalesHistoryRepository.cs#L85) `GetTransactionGroupAsync` | "한 판매"를 식별하는 쌍인데 인덱스가 없다. 상세 보기·재출력·환불 창을 열 때마다 원장 전수 스캔. (영수증 번호 자체는 [Receipt_Number.sale_key PK](../PharmaPOS.DataAccess/Repositories/ReceiptNumberRepository.cs#L28)로 찾으므로 해당 없음) | 중 |
| D4 | `Inventory.expiry_date`, `current_quantity` | [AlertRepository.cs:37,74](../PharmaPOS.DataAccess/Repositories/AlertRepository.cs#L37) 외 7곳 | 만료·부족 알림이 메인 화면 열 때마다 돈다. `UNIQUE(facility_id, product_id, batch_number)`가 앞부분만 덮고 `expiry_date`는 덮지 않음. 다만 `Inventory`는 상품 수에 비례(378행)해 원장처럼 무한히 크지 않다 | 하 |
| D5 | `Product_Master` 검색 `LOWER(col) LIKE '%term%'` | [ProductRepository.cs:44-52,67-72](../PharmaPOS.DataAccess/Repositories/ProductRepository.cs#L44) · [InventoryRepository.cs:35-38](../PharmaPOS.DataAccess/Repositories/InventoryRepository.cs#L35) · [SalesHistoryRepository.cs:55-58](../PharmaPOS.DataAccess/Repositories/SalesHistoryRepository.cs#L55) · [StockHistoryRepository.cs:60-64](../PharmaPOS.DataAccess/Repositories/StockHistoryRepository.cs#L60) · [UserRepository.cs:94](../PharmaPOS.DataAccess/Repositories/UserRepository.cs#L94) | 앞 와일드카드 + `LOWER()`라 `idx_product_name`·`idx_product_generic_name`(:275-277)이 **쓰이지 않는다**. 상품 수(300)에서는 문제없으나, 인덱스가 있다고 믿고 있으면 안 됨 | 하 |

**실측 미확인**: `EXPLAIN QUERY PLAN`을 실행 DB에 대고 돌리지 않았다. 위 판단은 인덱스 정의와 질의문 대조에 근거한다.

### 3-2. 반복 조회(N+1)

| # | 위치 | 패턴 | 근거 | 위험도 |
|---|---|---|---|---|
| D6 | [ProductListViewModel.cs:376-382](../PharmaPOS.Wpf/ViewModels/ProductListViewModel.cs#L376) → [:393-408](../PharmaPOS.Wpf/ViewModels/ProductListViewModel.cs#L393) | 상품 목록의 **행마다** `ResolveAwareGroupAsync` → [AntibioticMatchingService.MatchAsync](../PharmaPOS.Application/Counselling/AntibioticMatchingService.cs#L39) → `FindByAtcCodeAsync` + `FindByNormalizedNameAsync` (질의 최대 2회) | 캐시(`_awareGroupCache`, :51)가 있지만 **ViewModel 인스턴스 필드**라 화면을 열 때마다 비어 있다. Products 화면 진입 1회 = 상품 300개 × 최대 2질의 = **최대 600회 DB 왕복**, 각각 연결 열고 닫음. 검색·필터로 목록을 다시 채울 때는 캐시가 살아 있어 반복되지 않음 | **상** — 가장 자주 여는 화면 |
| D7 | [InitialImportService.cs:581-589](../PharmaPOS.Application/Import/InitialImportService.cs#L581) | 임포트 1단계: 행마다 `_productService.SaveProductAsync` → 내부에서 `BarcodeExistsAsync` + `InternalBarcodeExistsAsync` + `GetNextInternalBarcodeAsync` + `InsertAsync` (행당 3-4 왕복, 각각 별도 연결·커밋) | 300행 = 약 1,000회 연결. 일회성 작업이라 시간은 견딜 수 있지만, 아래 D9와 묶여 있음 | 중 |
| D8 | [CounsellingService.cs:70-86](../PharmaPOS.Application/Counselling/CounsellingService.cs#L70) | 판매 확정 시 줄마다 `MatchAsync` + `LogAsync` | 한 판매의 줄 수(보통 1-5)만큼. 판매 흐름에서 체감 없음 | 하 |
| D9 | [PhotoImportService.cs:71-121, 142-160](../PharmaPOS.Application/Import/PhotoImportService.cs#L71) | 사진 파일마다 `GetAsync`(기존 사진 확인) 후 `SaveAsync` | 파일 수만큼. BLOB 쓰기라 묶어서 한 트랜잭션에 넣으면 오히려 락이 길어져, 지금 방식이 나쁘지 않음 | 하 |

오탐으로 확인한 것: `SaleRepository.cs:26-124`와 `RefundRepository.cs:83-174`의 반복문 안 질의는 **하나의 트랜잭션 안**이며 줄마다 재고를 다시 확인하는 것이 설계 목적(동시 판매 방지)이다. `AwareClassificationRepository.cs:84-95`의 시드 적재도 단일 트랜잭션(:98 `Commit`)이다.

### 3-3. 반복문 안에서 개별 커밋하는 INSERT/UPDATE

| # | 위치 | 근거 | 위험도 |
|---|---|---|---|
| D10 | [InitialImportService.cs:581-589](../PharmaPOS.Application/Import/InitialImportService.cs#L581) (상품), [:789-813](../PharmaPOS.Application/Import/InitialImportService.cs#L789) (배치) | 행마다 서비스/리포지터리를 부르고 각각이 자기 연결에서 커밋한다. 바깥 트랜잭션이 없어 **중간에 실패하면 앞 행만 저장된 상태**로 남는다. 다만 파일 해시는 반복이 끝난 뒤 [:864](../PharmaPOS.Application/Import/InitialImportService.cs#L864)에서 기록되므로 같은 파일을 다시 넣을 수 있고, 기존 상품은 갱신 처리라 중복은 생기지 않는다 — 복구 가능한 부분 실패 | 중 |

### 3-4. UI 스레드에서의 DB 접근

| # | 위치 | 근거 | 위험도 |
|---|---|---|---|
| D11 | [App.xaml.cs:62](../PharmaPOS.Wpf/App.xaml.cs#L62) `databaseInitializer.Initialize()` | `DatabaseInitializer.Initialize()`([:19](../PharmaPOS.DataAccess/Database/DatabaseInitializer.cs#L19))는 동기 메서드이며 `OnStartup`(UI 스레드)에서 그대로 호출된다. 시작 시 1회, 스키마 생성·마이그레이션이라 보통 수십 ms. 창이 뜨기 전이라 사용자가 멈춤을 볼 수는 없음 | 하 |
| D12 | ViewModel 전체 | `.Result` / `.Wait()` / `GetAwaiter().GetResult()`: **0건**. `Task.Run`으로 DB를 다른 스레드에 보내는 곳: **0건**(`Task.Run` 2건은 SMTP 전송과 백업 파일 검사). `ConfigureAwait`: 0건. 즉 모든 리포지터리 `await`는 UI 동기화 컨텍스트에서 재개된다. **외부 사실(이 저장소에서 검증하지 않음)**: `Microsoft.Data.Sqlite`의 `*Async` 메서드는 진짜 비동기 I/O가 아니라 호출 스레드에서 동기로 실행된다는 것이 Microsoft 문서에 명시돼 있다. 그 사실이 맞다면 D1·D6의 질의 시간은 **그대로 UI 멈춤 시간**이 된다 | **중** — D1·D6가 커질수록 상으로 올라감. 실측 **미확인** |
| D13 | `async void` 16곳 (예: [ProductEditViewModel.cs:381](../PharmaPOS.Wpf/ViewModels/ProductEditViewModel.cs#L381), [SalesHistoryViewModel.cs:168](../PharmaPOS.Wpf/ViewModels/SalesHistoryViewModel.cs#L168), [LoginViewModel.cs:99](../PharmaPOS.Wpf/ViewModels/LoginViewModel.cs#L99)) | 이벤트 핸들러가 아닌 ViewModel 메서드 7곳이 `async void`다. 안에서 예외가 새면 잡을 곳이 없어 프로세스가 죽는다. 리포지터리가 예외를 결과 객체로 바꾸는 규칙이 있어 실제 경로에서는 드물겠지만, 규칙을 어긴 한 곳이 전체를 내린다 | 중 |
| D14 | 생성자 안 `_ = XxxAsync()` 20곳 (예: [MainShellViewModel.cs:62](../PharmaPOS.Wpf/Shell/MainShellViewModel.cs#L62), [ProductListViewModel.cs:78](../PharmaPOS.Wpf/ViewModels/ProductListViewModel.cs#L78)) | 생성자에서 조회를 던지고 잊는다. 예외는 `TaskScheduler.UnobservedTaskException`으로 가 조용히 사라진다(프로세스는 안 죽음). 화면이 "빈 채로" 뜨는데 원인이 남지 않는 종류 | 하 |

---

## 4. 메모리 위험

### 4-1. 해제되지 않는 이벤트 구독

`+=` 58건, `-=` 3건, `WeakEventManager` 8건을 전수 확인했다. 대부분은 **View가 자기 ViewModel의 이벤트를 구독**하는 것으로, 둘의 수명이 같아(화면 이동 시 함께 버려짐) 누수가 아니다. 문제는 **오래 사는 쪽이 짧게 사는 쪽을 붙드는** 아래 한 곳이다.

| # | 위치 | 근거 | 위험도 |
|---|---|---|---|
| M1 | [Shell/MainShellView.xaml.cs:25-32](../PharmaPOS.Wpf/Shell/MainShellView.xaml.cs#L25) | `Loaded`에서 `viewModel.LogoutRequested += OnLogoutRequested;` `viewModel.MyPageRequested += () => OnMyPageRequested(viewModel);` — 구독 대상 `viewModel`은 [App.CurrentShellViewModel](../PharmaPOS.Wpf/App.xaml.cs#L21)로 **로그인 세션 내내 사는 정적 참조**다. 반면 `MainShellView`는 화면에서 돌아올 때마다 `new MainShellView { DataContext = App.CurrentShellViewModel }`로 **새로 만들어진다(13곳)**. `-=`는 없다. 따라서 화면을 한 번 나갔다 올 때마다 셸 ViewModel의 델리게이트 목록에 이전 `MainShellView`(시각 트리·알림 팝업 포함)가 하나씩 더 매달려 **로그아웃 전까지 해제되지 않는다.** 부수 효과: 로그아웃을 누르면 `OnLogoutRequested`가 누적된 횟수만큼 실행된다 | **상** — 12시간 근무에 화면 이동 수백 회면 셸 뷰 수백 개가 살아남음. 실측 **미확인** |
| M2 | [Views/AdminDashboardView.xaml.cs:76](../PharmaPOS.Wpf/Views/AdminDashboardView.xaml.cs#L76) `window.Closing += OnWindowClosing` | 오래 사는 `MainWindow`에 짧게 사는 뷰가 구독하지만 [:84](../PharmaPOS.Wpf/Views/AdminDashboardView.xaml.cs#L84)에서 `-=`한다. 해제 경로가 화면을 떠나는 모든 길에서 실행되는지는 **미확인** | 하 |
| M3 | [ViewModels/Base/RelayCommand.cs:26-27](../PharmaPOS.Wpf/ViewModels/Base/RelayCommand.cs#L26) `CommandManager.RequerySuggested` | WPF 표준 패턴. `CommandManager`는 약한 참조로 붙든다 | 없음 |
| M4 | [Services/LocExtension.cs:72](../PharmaPOS.Wpf/Services/LocExtension.cs#L72), [Controls/LanguageToggle.xaml.cs:55](../PharmaPOS.Wpf/Controls/LanguageToggle.xaml.cs#L55), [ViewModels/PosSaleViewModel.cs:214](../PharmaPOS.Wpf/ViewModels/PosSaleViewModel.cs#L214), [Shell/MainShellViewModel.cs:44](../PharmaPOS.Wpf/Shell/MainShellViewModel.cs#L44), [Views/AlertsView.xaml.cs:39](../PharmaPOS.Wpf/Views/AlertsView.xaml.cs#L39) | 언어 서비스(싱글턴)에 대한 구독 5곳은 모두 `WeakEventManager`이고 핸들러가 인스턴스 메서드다. 화면이 걷히면 함께 걷힌다 | 없음 |

### 4-2. 정적 필드에 누적되는 컬렉션

| # | 위치 | 근거 | 위험도 |
|---|---|---|---|
| M5 | [Authentication/PasswordRecoveryService.cs:16-17](../PharmaPOS.Application/Authentication/PasswordRecoveryService.cs#L16) `_pendingOtps`, `_verifiedTokens` (static) | 항목은 사용자명 키. 성공 경로(:141, :152, :193)에서 제거되지만, **OTP를 요청하고 끝내지 않은 사용자**의 항목은 만료(10분)가 지나도 다음 시도 때까지 남는다. 상한은 "OTP를 요청해 본 서로 다른 사용자명 수"라 약국 직원 규모에서는 수십 개를 넘지 않음. CLAUDE.md가 앱 재시작 시 사라진다고 기록 | 하 |
| M6 | [Application/Import/PhotoImportService.cs:36](../PharmaPOS.Application/Import/PhotoImportService.cs#L36), [AntibioticNameNormalizer.cs:25](../PharmaPOS.Application/Counselling/AntibioticNameNormalizer.cs#L25), [BackupRepository.cs:28](../PharmaPOS.DataAccess/Repositories/BackupRepository.cs#L28) | 읽기 전용 상수 테이블(`static readonly`, 초기화 후 추가 없음) | 없음 |
| M7 | [App.xaml.cs:21](../PharmaPOS.Wpf/App.xaml.cs#L21) `CurrentShellViewModel` (static) | 로그인 세션 동안 셸 ViewModel 하나를 붙든다. 그 자체는 의도된 것이나 **M1의 누수가 여기에 매달린다** | M1 참조 |

### 4-3. 12시간 이상 연속 실행 시 누수 가능 지점

| # | 항목 | 확인 결과 | 위험도 |
|---|---|---|---|
| M8 | 타이머 | `DispatcherTimer`·`System.Timers`·`PeriodicTimer`: **0건**. 주기적으로 도는 것이 없어 시간 자체로 쌓이는 것은 없다 | 없음 |
| M9 | SQLite 연결 | `CreateOpenConnection()` 호출 전부(DataAccess·Wpf)가 `using var`로 감싸져 있음. 미해제 연결 0건 | 없음 |
| M10 | 사진 이미지 | [ProductEditViewModel.cs:640-651](../PharmaPOS.Wpf/ViewModels/ProductEditViewModel.cs#L640) `BitmapImage`를 `CacheOption=OnLoad` + `Freeze()`로 만들어 스트림을 붙들지 않음. 상품 목록에는 사진을 싣지 않음(CLAUDE.md) | 없음 |
| M11 | 화면 이동 방식 | `MainWindow.Content` 교체이며 이전 화면을 붙드는 스택이 없다. **M1을 제외하면** 떠난 화면은 GC 대상 | M1 참조 |
| M12 | `ObservableCollection` 재적재 | `Clear()` 후 `Add()` 방식(예: [MainShellViewModel.cs:74-76](../PharmaPOS.Wpf/Shell/MainShellViewModel.cs#L74), [ProductListViewModel.cs:375-378](../PharmaPOS.Wpf/ViewModels/ProductListViewModel.cs#L375)). 누적 없음 | 없음 |
| M13 | 실측 | 장시간 실행 메모리 추이·GC 스냅샷은 하지 않음 | **미확인** |

---

## 5. 요약 — 손댈 순서 제안 (제안일 뿐, 이번 작업에서 수정하지 않음)

| 순위 | 항목 | 이유 |
|---|---|---|
| 1 | **M1** 셸 뷰 누수 | 코드에서 확정되는 유일한 누수이고, 근무 시간에 비례해 커진다. 고치는 데 `-=` 두 줄 |
| 2 | **D1/D2** `Stock_Transaction(facility_id, transaction_time)` 인덱스 | 원장은 지워지지 않는 테이블이라 반드시 닿는 문제. `ApplyMigrations`에 `CREATE INDEX IF NOT EXISTS` 한 줄 |
| 3 | **D6** 상품 목록 AWaRe N+1 | 가장 자주 여는 화면에서 최대 600 왕복. AWaRe 표가 384행뿐이라 한 번 읽어 메모리에서 맞추면 끝남 |
| 4 | **G1** 죽은 조정 화면 531줄 | 위험보다 혼동 방지. 이미 한 번 "조정이 제대로 안 됐다"는 보고가 있었고, 두 벌 중 어느 쪽이 도는지 헷갈릴 소지 |
| 5 | G2-G6, D13 | 정리 수준 |

**미확인으로 남긴 것**: `EXPLAIN QUERY PLAN` 실측(D1-D5), `Microsoft.Data.Sqlite` 동기 실행의 실제 UI 멈춤 시간(D12), 장시간 메모리 추이(M1, M13), `AdminDashboardView`의 `Closing` 해제 경로 완전성(M2), `coverlet` 사용 여부(2-1).
