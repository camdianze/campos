# 기능 사용 기록 점검표

22개 이벤트가 **실제 화면과 버튼에 연결되어 있는지**, 그리고 무엇이 테스트로 고정되어
있는지. 이름을 늘리거나 화면을 고칠 때 이 표를 같이 고친다.

약국에게 하는 약속은 [usage-events-consent.md](usage-events-consent.md)이고,
코드가 그것을 지키는지는 `UsageEventTests`가 본다.

*점검 2026-11-20 · CamdiansRX v2.1 · 28/28 연결됨*

> ⚠ **동의서가 아직 이 여섯 개를 담고 있지 않다.** `admin_dashboard` `user_management` `receipt_settings` `counselling_settings` `product_scanned` `product_searched` — 약국이 읽기 전에 반영해야 한다.


---

## 화면 열기 (9) — `outcome = shown`

| 이벤트 | 기록되는 위치 | 테스트 |
|---|---|---|
| `sales` | `Shell/MainShellView.xaml.cs:193`<br>`Views/InventoryStatusView.xaml.cs` (재고에서 바로 팔기) | 이름·형식 |
| `products` | `Shell/MainShellView.xaml.cs:111` | 이름·형식 |
| `inventory` | `Shell/MainShellView.xaml.cs:120` | 이름·형식 |
| `stock_in` | `ViewModels/InventoryStatusViewModel.cs:417` (패널 열기)<br>`ViewModels/ProductListViewModel.cs` | 이름·형식 |
| `alerts` | `Shell/MainShellView.xaml.cs:95` | 이름·형식 |
| `reports` | `Shell/MainShellView.xaml.cs` (OnAntibioticReportClick)<br>`Views/HistoryView.xaml.cs` (Analysis) | **값까지** |
| `sales_history` | `Views/HistoryView.xaml.cs:52` | 이름·형식 |
| `stock_history` | `Views/HistoryView.xaml.cs:79` | 이름·형식 |
| `data_center` | `Views/AdminDashboardView.xaml.cs:184` | 이름·형식 |

> `stock_in`은 독립 화면이 아니라 재고·상품 화면 **안의 패널**이다. 패널을 연 것이
> 화면을 연 것에 해당한다.

> `reports`는 **두 화면**이 쓴다. 리포트 화면이 항생제 리포트와 매출 분석으로
> 갈라졌지만 이름을 새로 만들지 않았다 — 지표에서 묻는 것("리포트를 보는가")은
> 갈라지기 전과 같고, 이름을 더하면 동의서를 먼저 고쳐야 한다. 둘을 가려야 할
> 이유가 생기면 그때 나눈다.

## 관리자 화면 (4) — `outcome = shown`

| 이벤트 | 기록되는 위치 | 테스트 |
|---|---|---|
| `admin_dashboard` | `Shell/MainShellView.xaml.cs` (OnAdminDashboardClick) | 이름·형식 |
| `user_management` | `Views/AdminDashboardView.xaml.cs` (직원 관리 열기) | 이름·형식 |
| `receipt_settings` | `Views/AdminDashboardView.xaml.cs` (`Expanded` 핸들러) | 이름·형식 |
| `counselling_settings` | `Shell/MainShellView.xaml.cs` (OnCounsellingSettingsClick) | 이름·형식 |

> 영수증 설정은 독립 화면이 아니라 대시보드 안의 섹션이다. **펼친 것**이 화면을
> 연 것에 해당한다.

> 이 넷이 0인 약국은 제품을 일과에는 넣었는데 관리는 안 하고 있다는 뜻이다 —
> 설정이 비어 있는 채로 쓰이는 약국을 찾는 길이기도 하다.

## 상품을 어떻게 찾았나 (2) — `success` / `failure`

| 이벤트 | 기록되는 위치 | 테스트 |
|---|---|---|
| `product_scanned` | `ViewModels/PosSaleViewModel.cs` (`RecordHowItWasFound`) | 이름·형식 |
| `product_searched` | 같은 곳 | 이름·형식 |

세 가지에 답한다:

| 질문 | 읽는 법 |
|---|---|
| 스캐너를 실제로 쓰나 | `product_scanned`가 0이면 안 쓰거나 고장 |
| 바코드가 등록돼 있나 | `product_scanned`의 `failure` 비율 |
| 검색이 더 빠른가 | 둘의 비율 |

가운데 것이 지금은 알 길이 없는 값이다 — 찍었는데 안 나오는 상품은 화면에
`Product not found`만 뜨고 아무 데도 남지 않는다.

> **무엇을 찾았는지는 적지 않는다.** 찾는 일이 일어났다는 사실과 찾았는지 여부뿐이고,
> 상품 정보는 기록에 없다.

> **못 찾았을 때 둘 중 어느 쪽이었는지는 입력의 모양으로 가른다** — 숫자 8자 이상
> 이거나 `INT-`로 시작하면 바코드로 친다. 손으로 코드를 친 경우가 섞일 수 있지만,
> 이 값은 한 줄씩이 아니라 비율로 읽는 것이라 그 정도는 견딘다. 정확히 가르려면
> 스캐너가 보냈는지를 알아야 하는데, 키보드로 들어오는 입력에서는 알 수 없다.


## 행동 (9) — `success` / `failure`

| 이벤트 | 기록되는 위치 | 테스트 |
|---|---|---|
| `sale_completed` | `ViewModels/PosSaleViewModel.Payment.cs:282` (성공)<br>같은 파일 (실패) | **값까지** |
| `receipt_printed` | `PosSaleViewModel.Payment.cs:344`<br>`SalesHistoryViewModel.cs` (재출력) | 이름·형식 |
| `stock_in_saved` | `ViewModels/InventoryStatusViewModel.cs:474` | 이름·형식 |
| `adjustment_saved` | `ViewModels/InventoryStatusViewModel.cs:675` | 이름·형식 |
| `refund_saved` | `Views/RefundWindow.xaml.cs:162` | 이름·형식 |
| `label_printed` | `ViewModels/InternalBarcodeViewModel.cs:144` | 이름·형식 |
| `import_completed` | `ViewModels/BackupExportViewModel.cs:863` | 이름·형식 |
| `backup_created` | `ViewModels/BackupExportViewModel.cs:939` | 이름·형식 |
| `sync_manual` | `ViewModels/BackupExportViewModel.cs:360` | 이름·형식 |

> `receipt_printed`에는 `skipped`도 있다. 설정이 `never`이거나 `ask`에서 거절한
> 경우이고, **실패가 아니므로 오류율에 섞이면 안 된다.**

> `import_completed`는 한 줄도 못 넣었을 때만 `failure`다. 임포트는 부분 성공이
> 보통이라, 몇 줄 실패했다고 실패로 적으면 오류율이 임포트 한 번에 휘둘린다.

## 알림 (2) — `outcome = shown`

| 이벤트 | 기록되는 위치 | 테스트 |
|---|---|---|
| `expiry_alert_opened` | `ViewModels/AlertsViewModel.cs:40` (Type 필터 = Expiry) | 이름·형식 |
| `low_stock_alert_opened` | `ViewModels/AlertsViewModel.cs:45` (Type 필터 = LowStock) | 이름·형식 |

> 알림은 한 화면의 두 필터다. 화면을 연 것은 `alerts`이고, 이 둘은 그 안에서
> 한쪽을 골라 본 것이다. `All`은 둘 다 본 것이라 적지 않는다.

## 기본 (2)

| 이벤트 | 기록되는 위치 | 테스트 |
|---|---|---|
| `app_started` | `App.xaml.cs:100` — DB가 만들어진 직후, 로그인 전 | **값까지** (역할이 비어 있음) |
| `login` | `Views/LoginView.xaml.cs:46` — 역할을 기억하는 자리와 같다 | **값까지** |

---

## 테스트가 고정하는 것

`CamPOS.Tests/Usage/UsageEventTests.cs`

| | |
|---|---|
| 칸이 **여섯 개뿐** | `TheTableHasExactlyTheSixColumnsThePharmacyWasTold` |
| 사람을 담을 자리가 없음 | `TheEventTypeHasNowhereToPutAPerson` · `NothingIdentifyingCanBeStored` |
| 목록에 없는 이름은 안 적힘 | `AnUnknownNameOrOutcomeIsNotWritten` |
| 상수와 `All`이 어긋나지 않음 | `EveryDeclaredNameIsInTheAllowedSet` |
| 이름이 전부 snake_case | `NamesAreLowerCaseSnakeCase` |
| 기록 실패가 판매를 막지 않음 | `AStorageFailureNeverReachesTheCaller` |
| 로그인 전 역할은 비어 있음 | `BeforeAnyoneLogsInTheRoleIsEmpty` |
| 로그아웃하면 역할을 잊음 | `LoggingOutForgetsTheRole` |

`CamPOS.Tests/Sync/SyncServiceTests.cs`

| | |
|---|---|
| **`Auto`가 꺼져도 사용 기록은 간다** | `WithAutomaticOff_TheTimerStillSendsUsageEvents` |
| 약국 데이터를 꺼도 사용 기록은 간다 | `WithPharmacyDataOff_OnlyUsageEventsAreSent` |
| 보낼 것이 없으면 요청하지 않음 | `WithPharmacyDataOffAndNothingToSend_NoRequestIsMade` |

`CamPOS.Tests/Startup/AppDataMigrationTests.cs`

| | |
|---|---|
| **이름이 바뀌어도 기록과 워터마크가 함께 온다** | `UsageEventsAndTheirWatermarkSurviveTheMove` |

> 마지막 것은 이름을 바꿀 때마다 다시 봐야 한다. 기록만 오고 워터마크가 0으로
> 돌아가면 서버에 이미 있는 것을 전부 다시 보내고, 워터마크만 오고 기록이 비면
> 그 사이의 사용이 영영 올라가지 않는다 — 둘 다 조용히 틀린다.

### 테스트 열의 뜻

- **이름·형식** — 그 이름이 `All`에 있고 snake_case이며, 기록기가 받아들인다는 것까지.
  화면의 그 버튼이 실제로 부르는지는 테스트가 보지 않는다
- **값까지** — 실제로 기록된 줄의 이름·결과·역할·버전을 확인한다

**버튼과 이벤트의 연결은 자동으로 확인되지 않는다.** ViewModel을 고치다 호출을
지워도 테스트는 전부 통과하고, 지표에서 그 기능만 0이 된다. 이 표가 그 자리를
메우는 종이이고, 실제 확인은 아래 쿼리로 한다.

---

## 배포 뒤 확인

앱에서 한 바퀴 돌고(로그인 → 판매 → 재고 → 알림 Expiry → Data Center → Synchronize),
서버에서 [`explore.sql`](../supabase/explore.sql)의 **11b**를 실행한다. 22줄이 나오고,
방금 누른 것들이 0이 아니어야 한다.

0으로 남은 줄은 셋 중 하나다 — 그 기능을 안 썼거나, **연결이 끊겼거나**, 눌렀는데
실패했거나. 세 번째는 `실패` 칸이 말해 준다.
