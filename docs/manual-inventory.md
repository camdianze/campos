# PharmaPOS 매뉴얼 작성용 화면·문자열 인벤토리

이 문서는 사용자 매뉴얼을 쓰기 위해 코드에서 뽑아낸 목록이다. 코드는 수정하지 않았고, 읽어서 정리만 했다.
기준 시점: 작업 트리 현재 상태 (`main`, 최근 커밋 `5830ffa`).

문자열은 모두 코드에 있는 그대로다. 영어 표기·이모지·화살표(`←`)·전각 기호(`＋`)까지 원문이므로 매뉴얼에 그대로 옮겨도 된다.

---

## 1. Views 폴더의 .xaml 파일 목록과 실제 제목 텍스트

`PharmaPOS.Wpf/Views/` 에 25개, 셸이 `PharmaPOS.Wpf/Shell/` 에 1개 있다.

「제목」은 화면 안에 실제로 그려지는 큰 글씨(`Style="{StaticResource PageTitle}"`)를 말한다. `Window`인 경우 창 제목 표시줄 문자열도 함께 적었다.

| 파일 | 종류 | 화면에 보이는 제목 |
|---|---|---|
| [AddUserWindow.xaml](../PharmaPOS.Wpf/Views/AddUserWindow.xaml) | Window | 창 제목 `Add User` / 본문 제목 `Add User` |
| [AdjustmentHistoryView.xaml](../PharmaPOS.Wpf/Views/AdjustmentHistoryView.xaml) | UserControl | `Adjustment History` |
| [AdjustmentView.xaml](../PharmaPOS.Wpf/Views/AdjustmentView.xaml) | UserControl | `Inventory Adjustment` |
| [AdminDashboardView.xaml](../PharmaPOS.Wpf/Views/AdminDashboardView.xaml) | UserControl | `Administrator Dashboard` |
| [AlertsView.xaml](../PharmaPOS.Wpf/Views/AlertsView.xaml) | UserControl | `Inventory Alerts` |
| [AppDialog.xaml](../PharmaPOS.Wpf/Views/AppDialog.xaml) | Window | 제목이 호출할 때 정해진다 (아래 4번 참고) |
| [BackupExportView.xaml](../PharmaPOS.Wpf/Views/BackupExportView.xaml) | UserControl | `Backup / Export` |
| [ChangePasswordView.xaml](../PharmaPOS.Wpf/Views/ChangePasswordView.xaml) | UserControl | `Change Password` |
| [CounsellingSettingsView.xaml](../PharmaPOS.Wpf/Views/CounsellingSettingsView.xaml) | UserControl | `Antibiotic Counselling` |
| [FindUsernameView.xaml](../PharmaPOS.Wpf/Views/FindUsernameView.xaml) | UserControl | `Find ID` |
| [HistoryView.xaml](../PharmaPOS.Wpf/Views/HistoryView.xaml) | UserControl | `History` |
| [InitialSetupView.xaml](../PharmaPOS.Wpf/Views/InitialSetupView.xaml) | UserControl | 1단계 `Initial Facility Setup` / 2단계 `Login Setup` |
| [InternalBarcodeView.xaml](../PharmaPOS.Wpf/Views/InternalBarcodeView.xaml) | UserControl | `Internal Barcode` |
| [InventoryStatusView.xaml](../PharmaPOS.Wpf/Views/InventoryStatusView.xaml) | UserControl | `Inventory Status` |
| [LicenseActivationView.xaml](../PharmaPOS.Wpf/Views/LicenseActivationView.xaml) | UserControl | `Activate PharmaPOS` |
| [LoginView.xaml](../PharmaPOS.Wpf/Views/LoginView.xaml) | UserControl | PageTitle 없음 (로고/입력칸만) |
| [MyPageView.xaml](../PharmaPOS.Wpf/Views/MyPageView.xaml) | UserControl | `My Page` |
| [PasswordRecoveryView.xaml](../PharmaPOS.Wpf/Views/PasswordRecoveryView.xaml) | UserControl | 단계별로 `Find Password` → `Verify Identity` → `New Password` |
| [PosSaleView.xaml](../PharmaPOS.Wpf/Views/PosSaleView.xaml) | UserControl | `POS Sale` |
| [ProductEditView.xaml](../PharmaPOS.Wpf/Views/ProductEditView.xaml) | UserControl | `Product` |
| [ProductListView.xaml](../PharmaPOS.Wpf/Views/ProductListView.xaml) | UserControl | `Products` |
| [RecoverySettingsView.xaml](../PharmaPOS.Wpf/Views/RecoverySettingsView.xaml) | UserControl | `Recovery Settings` |
| [ReportsView.xaml](../PharmaPOS.Wpf/Views/ReportsView.xaml) | UserControl | `Reports` |
| [ResetPasswordWindow.xaml](../PharmaPOS.Wpf/Views/ResetPasswordWindow.xaml) | Window | 창 제목 `Reset Password` / 본문에 `Reset password for: {사용자명}` |
| [SalesHistoryView.xaml](../PharmaPOS.Wpf/Views/SalesHistoryView.xaml) | UserControl | `Sales History` |
| [UserManagementView.xaml](../PharmaPOS.Wpf/Views/UserManagementView.xaml) | UserControl | `User Management` |
| [Shell/MainShellView.xaml](../PharmaPOS.Wpf/Shell/MainShellView.xaml) | UserControl | 제목 대신 `Welcome, {사용자명}` |

보조 설명 문구 (제목 바로 아래 캡션):

- HistoryView — `Select a history type to view records.`
- CounsellingSettingsView — `Counselling sheets support pharmacist advice on antibiotic use. They do not provide dosing or diagnosis.`
- RecoverySettingsView — `Set up at least one recovery method in case you forget your password.`

---

## 2. 화면 간 이동 경로 (트리)

창은 `MainWindow` 하나뿐이고, `Content`를 통째로 갈아끼우는 방식이다 (뒤로가기 스택 없음).
`[Window]`로 표시한 것만 모달 대화상자로 뜬다.

```
앱 시작 (App.OnStartup)
│
├─ 라이선스 미활성화 → LicenseActivationView "Activate PharmaPOS"
│                        └─ Activate 성공 ──┐
│                                            ↓
├─ 활성화됨 + 초기설정 전 → InitialSetupView (1단계 Initial Facility Setup → 2단계 Login Setup)
│                            └─ 설정 완료 → LoginView
│
└─ 활성화됨 + 설정 완료 → LoginView
                           ├─ Forgot Username? → FindUsernameView "Find ID"
                           │                      └─ Cancel/완료 → LoginView
                           ├─ Forgot Password? → PasswordRecoveryView
                           │                      (Find Password → Verify Identity → New Password)
                           │                      └─ Cancel / 재설정 성공 → LoginView
                           └─ Login 성공 → MainShellView
```

로그인 후 (메인 셸):

```
MainShellView  ("Welcome, {사용자명}")
│
├─ [상단] My Page ──────────→ MyPageView
│                              ├─ Change Password ─→ ChangePasswordView
│                              │                      ├─ Cancel → MyPageView
│                              │                      └─ 변경 성공 → LoginView (재로그인 강제)
│                              ├─ Recovery Settings → RecoverySettingsView
│                              │                      └─ ← Back → MyPageView
│                              └─ ← Back → MainShellView
│
├─ [상단] Logout ───────────→ LoginView
│
├─ [상단] 🔔 알림 팝업
│         └─ View All Alerts → AlertsView "Inventory Alerts"
│                               ├─ View Inventory → InventoryStatusView (상품명으로 필터된 상태)
│                               ├─ Export (CSV 저장 대화상자)
│                               └─ ← Back → MainShellView
│
├─ [카드1] Products ────────→ ProductListView "Products"
│                              ├─ Add Product → ProductEditView "Product"
│                              │                 └─ Save/Cancel → ProductListView
│                              ├─ Edit → ProductEditView "Product"
│                              │          └─ Save/Cancel → ProductListView
│                              ├─ Deactivate (같은 화면에서 처리)
│                              ├─ Print Barcode → InternalBarcodeView "Internal Barcode"
│                              │                   └─ ← Back → ProductListView
│                              ├─ (행 선택 시 하단 Stock-IN 패널이 펼쳐짐 — 별도 화면 아님)
│                              └─ ← Back → MainShellView
│
├─ [카드2] Inventory ───────→ InventoryStatusView "Inventory Status"
│                              ├─ Stock-in → ProductListView (입고 패널이 있는 그 화면)
│                              ├─ Adjustment → AdjustmentView "Inventory Adjustment"
│                              ├─ Delete Batch (확인 대화상자)
│                              └─ ← Back → MainShellView
│
├─ [카드3] POS Sale ────────→ PosSaleView "POS Sale"
│                              ├─ ✓ Confirm Sale (같은 화면 유지, 영수증·복약안내 팝업)
│                              ├─ ✕ Cancel Sale → MainShellView
│                              └─ ← Back → MainShellView
│
├─ [하단] ✏️ Adjustment ────→ AdjustmentView "Inventory Adjustment"
│                              └─ ← Back → MainShellView
│
├─ [하단] 📊 History ───────→ HistoryView "History"
│                              ├─ Sale History 카드 → SalesHistoryView "Sales History"
│                              │                       ├─ View Detail (팝업)
│                              │                       ├─ Reprint Receipt (팝업)
│                              │                       ├─ Export (CSV 저장 대화상자)
│                              │                       └─ ← Back → MainShellView   ※ History가 아니라 셸로 감
│                              ├─ Adjustment History 카드 → AdjustmentHistoryView "Adjustment History"
│                              │                             └─ ← Back → HistoryView
│                              └─ ← Back → MainShellView
│
├─ [하단·관리자] ⚙️ Admin Dashboard → AdminDashboardView "Administrator Dashboard"
│                                      ├─ 📦 Product Management → ProductListView
│                                      ├─ 👥 User Management → UserManagementView
│                                      │                        ├─ Add User → [Window] AddUserWindow
│                                      │                        ├─ Reset Password → [Window] ResetPasswordWindow
│                                      │                        ├─ Edit Role / Deactivate (확인 대화상자)
│                                      │                        └─ ← Back → AdminDashboardView
│                                      ├─ 📊 Inventory Overview → InventoryStatusView
│                                      ├─ 🛒 Sales History → SalesHistoryView
│                                      ├─ 📈 Reports → ReportsView "Reports"
│                                      │                └─ ← Back → MainShellView
│                                      ├─ 💾 Backup / Export → BackupExportView "Backup / Export"
│                                      │                        └─ ← Back → MainShellView
│                                      └─ ← Back → MainShellView
│
└─ [하단·관리자] 🧫 Counselling → CounsellingSettingsView "Antibiotic Counselling"
                                   └─ ← Back → MainShellView
```

매뉴얼 쓸 때 주의할 동선 특이점:

- 하위 화면의 `← Back`은 **거의 전부 메인 셸로 직행**한다. 들어온 화면으로 돌아가는 것은 AdjustmentHistoryView(→ History), UserManagementView(→ Admin Dashboard), InternalBarcodeView·ProductEditView(→ Products), MyPage 하위 두 화면(→ My Page)뿐이다.
  - 특히 **Sales History의 `← Back`은 History 화면이 아니라 메인 셸로 간다.** Admin Dashboard에서 들어왔을 때도 마찬가지다.
  - Inventory Overview / Product Management로 Admin Dashboard에서 들어가면, 그 화면의 `← Back`은 대시보드가 아니라 메인 셸로 나온다.
- 입고(Stock-IN) 전용 화면은 없다. Products 화면에서 상품을 선택하면 하단에 입고 패널이 펼쳐진다. Inventory Status의 `Stock-in` 버튼도 Products 화면으로 보낸다.
- DB 복원에 성공하면 앱이 **자동으로 재시작**된다 ([BackupExportViewModel.cs](../PharmaPOS.Wpf/ViewModels/BackupExportViewModel.cs)).

---

## 3. 화면별 Button Content 문자열 전체

CheckBox / RadioButton의 라벨도 구분해서 함께 적었다.

### MainShellView (메인 셸)
- `My Page`
- `Logout`
- `🔔` (알림 버튼, 배지에 건수 표시)
- `View All Alerts →` (알림 팝업 안)
- 카드 3개: `Products` / `Inventory` / `POS Sale` (각각 이모지 💊 📦 🛒)
- 하단 (관리자 4개): `⚙️  Admin Dashboard` / `✏️  Adjustment` / `📊  History` / `🧫  Counselling`
- 하단 (직원 2개): `✏️  Adjustment` / `📊  History`

### LicenseActivationView
- `Load from file…`
- `Activate`

### InitialSetupView
- 1단계: `Next`
- 2단계: `Back`, `Next` (2단계의 Next가 실제로는 설정 완료 버튼)

### LoginView
- `Login`
- `Forgot Username?`
- `Forgot Password?`
- CheckBox `Remember me`

### FindUsernameView
- `Cancel`, `Confirm`

### PasswordRecoveryView
- 1단계: `Cancel`, `Confirm`
- 보안질문 단계: `Use Email OTP instead`, `Confirm`
- 이메일 OTP 단계: `Send Code`, `Use Security Question instead`, `Confirm`
- 새 비밀번호 단계: `Reset Password`

### MyPageView
- `Change Password` (부제 `Set a new password for this account.`)
- `Recovery Settings` (부제 `Security question and recovery email, used when you forget your password.`)
- `← Back`

### ChangePasswordView
- `Cancel`, `Change Password`

### RecoverySettingsView
- `← Back`, `Save`

### ProductListView
- `← Back`, `Add Product`, `Edit`, `Deactivate`, `Print Barcode`
- 입고 패널 안: `Cancel`, `Save`

### ProductEditView
- `Cancel`, `Save`
- CheckBox `Fixed-dose combination product`
- CheckBox `Sell loose units (소분 판매)` ← **유일하게 한글이 섞인 UI 문자열**

### InternalBarcodeView
- `← Back`, `Generate`, `Print Label`

### InventoryStatusView
- `← Back`, `Delete Batch`, `Stock-in`, `Adjustment`
- CheckBox `Low Stock Only`

### AdjustmentView
- `← Back`, `Save`

### AdjustmentHistoryView
- `Search`, `Reset`, `← Back`

### PosSaleView
- `← Back`
- `＋  Add to Cart`
- `✓  Confirm Sale`
- `✕  Cancel Sale`
- 장바구니 행마다 `Remove`

### HistoryView
- 카드 `Sale History` (부제 `View all sales transactions`)
- 카드 `Adjustment History` (부제 `View all inventory adjustments`)
- `← Back`

### SalesHistoryView
- `Search`, `Reset`
- `← Back`, `View Detail`, `Reprint Receipt`, `Export`

### AlertsView
- `← Back`, `View Inventory`, `Export`

### AdminDashboardView
- `📦  Product Management`
- `👥  User Management`
- `📊  Inventory Overview`
- `🛒  Sales History`
- `📈  Reports`
- `💾  Backup / Export`
- `← Back`

### UserManagementView
- `← Back`, `Add User`, `Edit Role`, `Deactivate`, `Reset Password`

### AddUserWindow
- `Cancel`, `Create`

### ResetPasswordWindow
- `Cancel`, `Reset`

### ReportsView
- `Apply`, `This Month`, `Last Month`
- `← Back`, `Export CSV`

### BackupExportView
- `Browse` ×3 (Import / Backup / Restore 각 구역마다 하나씩)
- `📥  Import Products`
- `💾  Create DB Backup`
- `📤  Export Data`
- `🔄  Restore DB`
- `← Back`
- RadioButton `CSV`, `Excel`

### CounsellingSettingsView
- `Browse` (파일 출력 폴더 선택, File 출력일 때만 보임)
- `← Back`, `Save`

### AppDialog (공용 대화상자)
- 알림형: `OK`
- 확인형: 기본값 `Yes` / `No`. 호출부에서 바꾸는 경우 — `Delete`, `Open` / `Skip`, `Print` / `Skip`

---

## 4. 대화상자 / 검증 실패 / 오류 메시지 문자열 전체

### 4-1. 대화상자 (AppDialog — 시스템 MessageBox는 쓰지 않는다)

`MessageBox.Show`는 코드에 하나도 없다. 전부 테마를 맞춘 [AppDialog](../PharmaPOS.Wpf/Views/AppDialog.xaml.cs)로 통일돼 있다.

| 제목 | 본문 | 버튼 | 띄우는 곳 |
|---|---|---|---|
| `Error` | `Error: {예외 메시지}` | OK | History 화면에서 하위 화면 열기 실패 |
| `Delete Batch` | `Remove batch {배치번호} of {상품명} from the inventory list?` + 빈 줄 + `It is empty, and past sales records are kept.` | Delete / No | Inventory Status |
| `Inventory Detail` | 재고 상세 (고정폭) | OK | Inventory Status |
| `Confirm` | 서비스가 돌려준 확인 문구 (아래 4-3 참고) | Yes / No | Adjustment 저장, POS 판매 확정, 상품 저장 |
| `Confirm Restore` | `Current data will be replaced. Continue?` | Yes / No | Backup / Export |
| `Restart Required` | `Database restored successfully. The application will now restart.` | OK | Backup / Export |
| `Open a Box` | `Only {n} loose unit(s) left in this batch.` + 줄바꿈 + `Open {n} box(es) of {n} to sell {n}?` | Open / Skip | POS Sale |
| `Antibiotic Counselling` | `This product contains an antibiotic. Print the counselling sheet?` + 빈 줄 + `{상품명}` | Print / Skip | POS Sale (설정이 Ask일 때) |
| `Receipt (Simulated Print)` | 영수증 본문 (고정폭) | OK | 판매 확정 후 / 영수증 재출력 |
| `Sale Detail` | 판매 상세 (고정폭) | OK | Sales History |
| `Confirm` | `Change role of '{사용자명}' to {새 역할}?` | Yes / No | User Management |

### 4-2. 로그인 / 계정 / 비밀번호

- `Please enter your username.`
- `Please enter your password.`
- `Invalid username or password.` ← 아이디가 없든 비밀번호가 틀리든 **같은 문구**를 낸다 (의도된 설계)
- `This account is inactive. Please contact the administrator.`
- `This facility is inactive. Please contact the administrator.`
- `An unexpected error occurred.`
- `Internal error: password box not found.`
- `Please enter your current password.`
- `Please enter a new password.`
- `Please confirm your new password.`
- `Current password is incorrect.`
- `New password and confirmation do not match.`
- `Password changed successfully. Please log in again.`
- `Password does not meet the required rules.` ← 8자 미만/규칙 위반 모두 같은 문구
- `Password cannot be the same as username.`
- `New password must be different from the current password.`
- `Password could not be changed. Please try again.`

### 4-3. 초기 설정 (Initial Setup)

- `Please enter the facility name.`
- `Please enter the country.`
- `Please enter the province or district.`
- `Please enter the administrator username.`
- `Please enter a password.`
- `Please confirm the password.`
- `Password and confirmation do not match.`
- `Please select a security question.`
- `Please enter the answer to your security question.`
- `This username is already in use.`
- `Initial setup completed successfully.`
- `Initial setup could not be completed. Please try again.`

### 4-4. 라이선스 활성화

- `Please enter your license code.`
- `This license code is not valid.`
- `This license code requires a newer version of PharmaPOS.`
- `This license expired on {yyyy-MM-dd}. Please contact your supplier.`
- `The selected file could not be read.` (파일에서 코드 불러오기 실패)

### 4-5. 아이디 찾기 / 비밀번호 찾기

- `Please enter your username.`
- `Please enter your email address.`
- `If this email is registered, we've sent your username to it. Please check your inbox.`
- `No recovery method is available for this account. Please contact your administrator.`
- `Email recovery is not available for this account.`
- `Please verify your identity first.`
- `Incorrect code. Please try again.`
- `This code has expired. Please request a new one.`
- `Please request a new recovery code.`
- `Recovery session has expired. Please start over.`
- `Recovery could not be verified.`
- `The recovery code could not be sent. Please try again.`
- `Password and confirmation do not match.`
- `Password could not be reset. Please try again.`

발송되는 메일 본문 (SMTP):

- 제목 `PharmaPOS Password Recovery Code` / 본문 `Your password recovery code is: {코드}` + `This code will expire in 10 minutes.`
- 제목 `PharmaPOS Username Recovery` / 본문 `Your PharmaPOS username is: {아이디}`

### 4-6. 복구 설정 (Recovery Settings)

- `Please set up at least one recovery method.`
- `Please enter the SMTP server address.`
- `Please enter a valid SMTP port number.`
- `SMTP host is required for 'Other' provider.`
- `Recovery settings could not be saved.`

### 4-7. 사용자 관리

- `Please enter the username.`
- `Please enter and confirm the password.`
- `Password and confirmation do not match.`
- `Please select a role.`
- `This username is already in use.`
- `Please select a user.`
- `You cannot deactivate your own account.`
- `User could not be created.`
- `User could not be updated.`
- `User could not be deactivated.`
- `Password could not be reset.`

### 4-8. 상품 (Products / Product Edit / Internal Barcode)

- `Please enter the product name.`
- `Please enter the unit.`
- `Cost price must be greater than zero.`
- `Selling price must be greater than zero.`
- `Safety stock level cannot be negative.`
- `Loose unit price must be greater than zero.`
- `Loose unit price must be a number.`
- `Units per box must be at least 1.`
- `Enter how many {단위}s are in one box (2 or more).`
- `Selling price is lower than cost price. Continue?` ← 확인 대화상자로 뜬다
- `This barcode is already registered.`
- `Please select a product.`
- `Product not found.`
- `This product is already inactive.`
- `Product could not be saved.`
- `Product could not be deactivated.`
- `Product list could not be loaded.`
- `Internal barcode already exists.`
- `Internal barcode could not be generated.`
- `Internal barcode could not be saved.`
- `Please enter the label quantity.`
- `Label quantity must be greater than zero.`
- `Please select a printer.`

### 4-9. 입고 (Stock-IN, Products 화면 하단 패널)

- `Please select a product.`
- `Please enter the batch number.`
- `Expiry date must be a future date.`
- `Quantity must be greater than zero.`
- `This product is inactive.`
- `Stock-in could not be saved.`

### 4-10. 재고 / 조정 (Inventory Status, Adjustment)

- `Inventory data could not be loaded.`
- `Please select a batch to delete.`
- `Batch {배치번호} still has {수량} in stock. …` (비어 있지 않은 배치는 삭제 불가)
- `The batch could not be deleted.`
- `Please select a product.`
- `Please select a batch number.`
- `Please enter the physical count.`
- `Physical count cannot be negative.`
- `Please enter the adjustment reason.`
- `No quantity difference was found.` ← 확인 대화상자로 뜬다
- `Adjustment saved successfully.`
- `Adjustment could not be saved.`
- `No adjustment records found.`
- `Error: {예외 메시지}`

### 4-11. POS 판매

- `Please scan a barcode or enter a product name.`
- `Product not found.`
- `No available stock for this product.`
- `Please select a batch number.`
- `This batch is expired and cannot be sold.`
- `Please enter the quantity.`
- `Quantity must be a whole number.`
- `Quantity must be greater than zero.`
- `Selling price must be greater than zero.`
- `Stock-out quantity cannot exceed current inventory quantity.`
- `Sale cancelled — no box was opened.`
- `Please add at least one product to the sale.`
- `Please select a payment method.`
- `Please enter the cash tendered.`
- `Cash tendered is less than total amount.`
- `Selling price is lower than cost price. Continue?` ← 확인 대화상자로 뜬다
- `Some products do not have enough stock.`
- `Sale completed successfully.`
- `Sale completed, but receipt could not be printed.`
- `Sale completed, but the antibiotic counselling sheet could not be printed.`
- `Sale could not be completed. Please try again.`

### 4-12. 판매 이력 / 알림 / 리포트

- `Please select a sales record.`
- `Sales history could not be loaded.`
- `Alerts could not be loaded.`
- `No alerts found.`
- `No alerts 🎉` (알림 팝업의 빈 상태)
- `Start date cannot be later than end date.`
- `The report could not be loaded.`
- `No data to export.`
- `Export completed successfully.`
- `Export failed. Please try again.`
- `Admin dashboard could not be loaded.`

### 4-13. 백업 / 내보내기 / 복원

- `Please select a file to import.`
- `File not found.`
- `Only .csv and .xlsx are supported.`
- `No rows with a product name were found in the file.`
- `Import error: {예외 메시지}`
- `Please select a backup location.`
- `Backup location is not available.`
- `Cannot access the selected location.`
- `Backup created: {파일명}`
- `Database backup failed.`
- `Please select data to export.`
- `{n} table(s) exported successfully.`
- `CSV export failed.`
- `Excel export failed.`
- `Please select a backup file.`
- `Invalid backup file.`
- `Please confirm database restore.`
- `Database is currently in use. Please try again.`
- `Database restore failed.`

### 4-14. 항생제 복약안내 (Counselling)

- `Settings saved.`
- `Settings could not be saved. Please try again.`
- `The AWaRe seed file is empty.`
- `The AWaRe reference file could not be read.`
- `The AWaRe reference file contains no usable rows.`
- `The AWaRe reference data could not be saved.`
- `The counselling sheet could not be printed.`
- `The counselling sheet could not be saved to the output folder.`

설정 화면의 설명 문구 (매뉴얼에 그대로 인용 가능):

- `Always prints without asking. Ask prompts each time. Never stops printing but still records stewardship figures.`
- `Printer sends the sheet to the default printer. File saves it as a text file instead - useful for checking the sheet when no printer is attached.`
- `Full is about 20 cm on 58 mm paper. Compact is about 10 cm and drops the signature and QR block.`
- `English is always printed. A local language is added only after its translation has been reviewed and approved.`
- `Leave empty to omit the information block from the sheet.`

---

## 5. 권한별 접근 가능 화면과 기능

역할은 `Administrator` 와 `FacilityStaff` 둘뿐이다 (`UserRole` 열거형).

권한 분기는 코드상 **딱 두 군데**에만 있다:

1. [MainShellViewModel.cs:47](../PharmaPOS.Wpf/Shell/MainShellViewModel.cs#L47) — `IsAdministrator`. 메인 셸 하단 버튼 줄을 관리자용 4개 / 직원용 2개로 가른다.
2. [PosSaleViewModel.cs:163](../PharmaPOS.Wpf/ViewModels/PosSaleViewModel.cs#L163) — `CanEditUnitPrice`. POS 판매 화면의 판매가 입력칸을 관리자만 수정할 수 있게 한다.

### 화면 접근

| 화면 | Administrator | FacilityStaff | 비고 |
|---|---|---|---|
| Login / Find ID / Find Password | ✔ | ✔ | 로그인 전 |
| Initial Facility Setup | — | — | 최초 1회, 이때 만들어지는 계정이 첫 관리자 |
| Activate PharmaPOS | ✔ | ✔ | 로그인 전, 최초 1회 |
| MainShell (Welcome) | ✔ | ✔ | |
| My Page → Change Password / Recovery Settings | ✔ | ✔ | 본인 계정만 |
| Inventory Alerts (🔔 → View All) | ✔ | ✔ | |
| Products (+ 입고 패널) | ✔ | ✔ | |
| Product (등록/수정) | ✔ | ✔ | |
| Internal Barcode | ✔ | ✔ | |
| Inventory Status | ✔ | ✔ | |
| Inventory Adjustment | ✔ | ✔ | 셸 하단 버튼이 직원에게도 있다 |
| POS Sale | ✔ | ✔ | |
| History → Sales History / Adjustment History | ✔ | ✔ | 셸 하단 버튼이 직원에게도 있다 |
| **Administrator Dashboard** | ✔ | ✘ | 셸 하단 버튼 자체가 안 보임 |
| **User Management** (+ Add User, Reset Password) | ✔ | ✘ | 대시보드를 거쳐야만 갈 수 있음 |
| **Reports** | ✔ | ✘ | 대시보드 경유 |
| **Backup / Export** | ✔ | ✘ | 대시보드 경유 |
| **Antibiotic Counselling 설정** | ✔ | ✘ | 셸 하단 버튼 자체가 안 보임 |

### 기능 차이

| 기능 | Administrator | FacilityStaff |
|---|---|---|
| POS 판매 시 판매가(Selling Price) 수정 | 가능 | **읽기 전용** |
| 사용자 추가 / 역할 변경 / 비활성화 / 비밀번호 초기화 | 가능 | 불가 |
| 리포트 조회·CSV 내보내기 | 가능 | 불가 |
| DB 백업 / 복원 / 상품 가져오기 / 테이블 내보내기 | 가능 | 불가 |
| 복약안내 설정 (출력 방식, 언어, 용지 길이, QR 주소) | 가능 | 불가 |
| 상품 등록·수정·비활성화, 입고, 재고 조정, 배치 삭제 | 가능 | **가능** |

주의할 점: 관리자 전용 화면은 **버튼을 숨기는 방식**으로만 막혀 있다. 화면 클래스 자체에는 역할 검사가 없다. 매뉴얼에는 "직원 계정에서는 해당 버튼이 보이지 않는다"로 쓰는 것이 정확하다.

또 하나 — 자기 계정은 비활성화할 수 없다 (`You cannot deactivate your own account.`). 마지막 관리자를 남기는 별도 보호 장치는 없다.

---

## 6. 앱이 생성하거나 읽는 파일 경로 전부

### 6-1. 고정 경로 — `%APPDATA%\PharmaPOS\`

전체 경로는 보통 `C:\Users\{사용자}\AppData\Roaming\PharmaPOS\` 다. 앱 시작 시 폴더가 없으면 만든다.

| 경로 | 읽기/쓰기 | 내용 |
|---|---|---|
| `%APPDATA%\PharmaPOS\` | 생성 | 앱 시작 시 자동 생성 |
| `%APPDATA%\PharmaPOS\pharmapos.db` | 읽기/쓰기 | SQLite 본 데이터베이스. 첫 실행 시 생성 |
| `%APPDATA%\PharmaPOS\pharmapos.db-wal`<br>`%APPDATA%\PharmaPOS\pharmapos.db-shm` | 읽기/쓰기 | WAL 저널 모드 부산물. SQLite가 자동 관리 |
| `%APPDATA%\PharmaPOS\license.dat` | 읽기/쓰기 | 라이선스 활성화 기록. **DPAPI(CurrentUser)로 암호화** — 다른 PC·다른 Windows 계정에서는 복호화되지 않음 |
| `%APPDATA%\PharmaPOS\remember_me.txt` | 읽기/쓰기 | 로그인 화면 "Remember me"로 저장한 아이디. 체크 해제 시 삭제 |
| `%APPDATA%\PharmaPOS\seeds\aware_2025.csv` | 읽기 | WHO AWaRe 분류 참조 데이터 **교체본**. 있으면 설치 폴더보다 **우선** |
| `%APPDATA%\PharmaPOS\locales\{언어코드}.json` | 읽기 | 복약안내 번역 **교체본**. 있으면 설치 폴더보다 **우선** |
| `%APPDATA%\PharmaPOS\counselling-sheets\` | 생성/쓰기 | 복약안내를 파일로 저장할 때의 **기본** 폴더 |
| `…\counselling-sheets\counselling_{yyyyMMdd-HHmmss}_{거래ID앞8자}_{상품명}.txt` | 쓰기 | 복약안내 용지 1장 = 파일 1개. UTF-8 BOM |

### 6-2. 설치 폴더 (`PharmaPOS.exe`가 있는 곳)

| 경로 | 읽기/쓰기 | 내용 |
|---|---|---|
| `{설치폴더}\seeds\aware_2025.csv` | 읽기 | 기본 동봉된 WHO AWaRe 분류 데이터 |
| `{설치폴더}\locales\km-kh.json` | 읽기 | 기본 동봉된 크메르어 복약안내 번역 |

`seeds` / `locales`는 **`%APPDATA%` → 설치 폴더 순서로 찾는다.** 재빌드 없이 현장에서 파일만 갈아 끼울 수 있게 한 구조다.

### 6-3. 사용자가 대화상자로 고르는 경로

| 화면 · 기능 | 대화상자 | 파일 필터 / 기본 파일명 |
|---|---|---|
| Activate PharmaPOS → `Load from file…` | 열기 | `License file (*.txt;*.lic)` / `All files (*.*)` |
| Backup/Export → `📥 Import Products` | 열기 | `CSV/Excel (*.csv;*.xlsx)`, `CSV (*.csv)`, `Excel (*.xlsx)` |
| Backup/Export → `🔄 Restore DB` | 열기 | `SQLite Database (*.db)` / `All files (*.*)` |
| Backup/Export → Backup 구역 `Browse` | 폴더 선택 | 백업·내보내기 대상 폴더 |
| Counselling 설정 → `Browse` | 폴더 선택 | 복약안내 파일 출력 폴더 |
| Alerts → `Export` | 저장 | `CSV files (*.csv)` / 기본명 `inventory_alerts_{yyyyMMdd}.csv` |
| Sales History → `Export` | 저장 | `CSV files (*.csv)` / 기본명 `sales_history_{yyyyMMdd}.csv` |
| Reports → `Export CSV` | 저장 | `CSV files (*.csv)` / 기본명 `report_{시작일}_{종료일}.csv` |

### 6-4. 앱이 자동으로 만드는 파일명 규칙 (선택한 폴더 안)

| 파일명 | 언제 |
|---|---|
| `pharmapos_backup_{yyyyMMdd_HHmmss}.db` | `💾 Create DB Backup` |
| `pharmapos_pre_restore_backup_{yyyyMMdd_HHmmss}.db` | **복원 직전 자동 백업.** 백업 폴더를 지정하지 않았으면 `%APPDATA%\PharmaPOS\`에 떨어진다 |
| `{테이블명}_{yyyyMMdd_HHmmss}.csv` 또는 `.xlsx` | `📤 Export Data`. `All`을 고르면 테이블 수만큼 파일이 생성된다 |

---

## 7. 코드에는 있으나 아직 UI에 연결되지 않은 / 미완성 기능

### 7-1. `TODO`로 명시된 것

| 기능 | 실제 동작 | 위치 |
|---|---|---|
| **영수증 인쇄** | 실제 프린터로 나가지 않는다. 영수증 내용을 `Receipt (Simulated Print)` 팝업으로 보여주고 성공으로 처리한다. ESC/POS 58mm 프린터 기종이 미정 | [SimulatedReceiptPrintingService.cs](../PharmaPOS.Wpf/Services/SimulatedReceiptPrintingService.cs) |
| **라벨 프린터 출력** | `Print Label` 버튼은 입력값 검증만 하고 "출력 준비 완료"를 돌려준다. 실제 하드웨어 연동 없음 | [InternalBarcodeService.cs:81](../PharmaPOS.Application/Products/InternalBarcodeService.cs#L81) |
| **판매 메모(Notes)** | POS Sale 화면에 `Notes` 입력칸이 있고 값도 잘 들어가지만, **저장되지 않는다.** `Stock_Transaction` 테이블에 컬럼이 없다 | [SaleService.cs:69](../PharmaPOS.Application/Inventory/SaleService.cs#L69) |

> 매뉴얼 관점에서 가장 위험한 건 **Notes**다. 사용자는 적으면 남는다고 믿을 텐데 실제로는 사라진다. 매뉴얼에서 이 칸을 설명하지 않거나, "현재 버전에서는 저장되지 않습니다"를 명시하는 편이 낫다.

### 7-2. 항생제 복약안내 (AMR) 관련 미완성

| 항목 | 현재 상태 |
|---|---|
| **QR 코드 이미지** | 용지에 `[QR]`이라는 글자와 주소 텍스트만 찍힌다. 실제 QR 이미지 인코딩 없음 (외부 패키지 미참조) — [WpfCounsellingSheetPrintingService.cs:155](../PharmaPOS.Wpf/Services/WpfCounsellingSheetPrintingService.cs#L155) |
| **국소(외용) 항생제 제외** | 제외 로직은 구현돼 있으나(`ExcludedTopical`), 동봉된 WHO 파일의 모든 행이 `is_systemic = true`라 **실제로는 한 번도 발동하지 않는다.** 연고에도 복약안내가 나갈 수 있다 |
| **투여 경로별 분류** | Minocycline / Fosfomycin은 정맥·경구에 따라 등급이 갈리지만, 복약안내 판정에 제형(`dosage_form`)을 쓰지 않기로 해서 **더 엄격한 쪽으로 통일**해 처리한다 (384행 중 2행). 제형 컬럼 자체는 존재하지만 표시·집계 전용이다 |
| **열전사 프린터 검증** | 복약안내는 Windows 인쇄 파이프라인으로 나간다. ESC/POS가 아니며, 실물 58mm 열전사 프린터에서 검증된 적 없다 |

### 7-3. 만들어졌지만 화면에 안 나오는 것

| 항목 | 내용 |
|---|---|
| `MainShellViewModel.RoleDescription` | `[Placeholder] Administrator Dashboard` / `[Placeholder] Main Dashboard / POS Screen` / `[Placeholder] Unknown Role` 문자열을 만들지만 **어떤 XAML에도 바인딩되어 있지 않다.** 화면에 나오지 않으므로 매뉴얼에서 무시해도 된다 — [MainShellViewModel.cs:40-45](../PharmaPOS.Wpf/Shell/MainShellViewModel.cs#L40-L45) |
| `UnixToDateConverter.ConvertBack` | `NotImplementedException`. 단방향 표시 전용이라 문제는 없다 |

### 7-4. 설계상 의도된 제약 (버그가 아니지만 매뉴얼에 적어야 할 것)

| 항목 | 내용 |
|---|---|
| **라이선스 기기 바인딩 없음** | 서명 검증만 한다. 고객이 자기 라이선스 코드를 남에게 알려주는 것은 막지 못한다. 코드 주석에 "공유까지 막으려면 기기 바인딩이 필요하다"로 명시 — [LicenseService.cs](../PharmaPOS.Application/Licensing/LicenseService.cs) |
| **비밀번호 복구 상태가 재시작을 못 넘긴다** | OTP·검증 토큰이 `static` 메모리 딕셔너리에 있어, 복구 도중 앱을 끄면 처음부터 다시 해야 한다 |
| **DPAPI 암호화 범위** | SMTP 앱 비밀번호와 `license.dat`은 Windows 사용자 계정에 묶여 있다. DB를 다른 PC로 복사해도 이 둘은 복호화되지 않는다 |
| **뒤로가기 스택 없음** | 2번 트리에 적은 대로 `← Back`이 들어온 화면으로 돌아가지 않는 경우가 많다 |
| **AWaRe 시드 적재 실패는 조용히 넘어간다** | 참조 데이터가 없으면 복약안내가 안 나올 뿐 판매는 정상 동작한다. 적재 상태는 Counselling 설정 화면의 `WHO AWaRe REFERENCE DATA` 항목에서 확인한다 |
