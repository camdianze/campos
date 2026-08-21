# 문자열 결합 목록 (CSV 제외 · 코드 수정 필요)

런타임에 `+` 또는 보간(`$"..."`)으로 조립되는 문구다. 조각 하나만 번역해서는 문장이 되지 않으므로
CSV(`i18n-strings.csv`)에 넣지 않았다. 각 항목은 `{name}` 형태의 변수 치환을 쓰는 단일 리소스 문자열로
바꿔야 한다.

어순 문제가 특히 큰 곳은 **⚠** 로 표시했다 — 한국어/크메르어로 옮기면 변수 위치가 영어와 달라진다.

---

## 1. POS 판매

| 파일 | 줄 | 현재 | 제안 |
|---|---|---|---|
| PosSaleViewModel.cs | 400-401 | `$"Only {remaining.BoxQuantity} unopened box(es) left in this batch."` | `pos.err.onlyBoxesLeft` = "Only {count} unopened box(es) left in this batch." |
| PosSaleViewModel.cs | 422-426 | `"Only {n} loose unit(s) left in this batch.\n" + "Open {b} box(es) of {u} to sell {q}?"` ⚠ | 두 줄을 각각 `pos.dlg.openBox.line1` / `pos.dlg.openBox.line2` 로 분리. 변수 4개(`{remaining}`,`{boxes}`,`{unitsPerBox}`,`{quantity}`) |
| PosSaleViewModel.Payment.cs | 195-198 | `"This product contains an antibiotic…" + $"\n\n{ProductName}" + $"\nWHO AWaRe group: {code}"` | 본문 / 상품명 / `sheet.lbl.whoAware` + `{group}` 3개 리소스로 분리 |
| PosSaleViewModel.Payment.cs | 205 | `notice + "\n\nPrint the counselling sheet?"` | `pos.dlg.printSheet` 를 독립 키로 분리 |

## 2. 상품 목록 / 재고 현황 (입고 · 조정 패널)

| 파일 | 줄 | 현재 | 제안 |
|---|---|---|---|
| ProductListViewModel.cs | 189 | `$"{product.UnitsPerBox} units per box."` | `stockIn.hint.unitsPerBox` = "{count} units per box." |
| ProductListViewModel.cs | 192 | `$"{boxes} box(es) × {n} = {total} units."` ⚠ | `stockIn.hint.boxPreview` = "{boxes} box(es) × {unitsPerBox} = {total} units." |
| ProductListViewModel.cs | 429-430 | `$"Stock-in saved for {name}: {q} box(es), {u} units."` / `$"Stock-in saved for {name}."` ⚠ | 박스형·낱개형 두 개의 키로 분리 |
| InventoryStatusViewModel.cs | 222 | `$"Batch {n} is empty and can be removed."` | `inventory.hint.batchEmpty` |
| InventoryStatusViewModel.cs | 223 | `$"Batch {n} still has {q} left. Use Adjustment instead."` ⚠ | `inventory.hint.batchNotEmpty` |
| InventoryStatusViewModel.cs | 359 / 362 | 위 `ProductListViewModel` 189/192와 동일 문구 | 같은 키 재사용 |
| InventoryStatusViewModel.cs | 438-439 | 위 429-430과 동일 문구 | 같은 키 재사용 |
| InventoryStatusViewModel.cs | 482 | `$"{ProductName} · {(배치번호 없으면 "no batch number" 아니면 $"Batch {n}")}"` | `inventory.lbl.noBatchNumber` + `inventory.lbl.batchN` 두 키 |
| InventoryStatusViewModel.cs | 496 | `$"System: {b} box(es) of {u} + {l} loose unit(s)."` ⚠ | `inventory.hint.systemBreakdown` |
| InventoryStatusViewModel.cs | 756-757 | `$"Batch {n} still has {q} in stock. " + "Use Adjustment to write it off first."` | 한 문장으로 합쳐 `inventory.err.batchNotEmpty` |
| InventoryStatusViewModel.cs | 763-764 | `$"Remove batch {b} of {p} from the inventory list?\n\n" + "It is empty, and past sales records are kept."` ⚠ | 질문 / 부연 두 키 |
| InventoryStatusViewModel.cs | 793 | `$"Batch {n} was removed."` | `inventory.msg.batchRemoved` |
| InventoryStatusViewModel.cs | 806-819 | 재고 상세 블록 전체 (`Product:`, `Batch Number:`, `Current Quantity:`, `Boxes:`, `Loose Units:`, `Box Price:`, `Unit Price:`, `Expiry Date:`, `Last Updated:`, `Low Stock:`) | 라벨 10개를 개별 키로 뽑고 `"{label}: {value}"` 조립. 고정폭 정렬은 별도 처리 |

## 3. 재고 조정 화면

| 파일 | 줄 | 현재 | 제안 |
|---|---|---|---|
| AdjustmentViewModel.cs | 106 | `$"Batch {batch.BatchNumber}"` | `adjustment.lbl.batchN` = "Batch {number}" |
| AdjustmentViewModel.cs | 109 | `string.Join(" · ", parts)` — 성분명·규격·배치를 붙임 | 구분자를 리소스화하거나 조립 자체를 유지하되 각 조각을 키로 |
| AdjustmentViewModel.cs | 135 | `$"System: {b} box(es) of {u} + {l} loose unit(s)."` ⚠ | 위 `inventory.hint.systemBreakdown` 재사용 |
| AdjustmentView.xaml | 89 | `StringFormat='{}{0} units per box.'` | XAML StringFormat 대신 ViewModel 속성으로 옮기고 리소스 사용 |

## 4. 상품 등록/수정 — 영어 복수형 `s` 하드코딩

| 파일 | 줄 | 현재 | 제안 |
|---|---|---|---|
| ProductEditViewModel.cs | 178 | `$"{UnitLabel}s Per Box *"` ⚠ **영어 복수형** | `productEdit.lbl.unitsPerBox` = "{unit} Per Box *" — 복수형 규칙은 언어별로 다르므로 `s` 를 코드에서 제거 |
| ProductEditViewModel.cs | 396 | `$"Enter how many {UnitLabel}s are in one box (2 or more)."` ⚠ **영어 복수형** | `productEdit.err.unitsPerBox` = "Enter how many {unit} are in one box (2 or more)." |
| ProductEditViewModel.cs | 143 | `$"One {UnitLabel} is sold at this price."` | `productEdit.hint.unitPriceSet` |
| ProductEditViewModel.cs | 150 | `$"Leave empty to sell one {u} at {price} ({box} ÷ {n})."` ⚠ | `productEdit.hint.unitPriceComputed` |
| ProductEditViewModel.cs | 153 | `$"Leave empty to sell one {u} at the box price divided by the count above."` | `productEdit.hint.unitPriceFallback` |
| ProductEditViewModel.cs | 161 | `$"Cost price and selling price above are for one {UnitLabel}."` | `productEdit.hint.boxPriceUnit` |
| ProductEditViewModel.cs | 158-160 | `"Cost price and selling price above are for one box."` (박스 분기) | 위와 짝이 되는 별도 키 |

## 5. 내부 바코드 / 라벨

| 파일 | 줄 | 현재 | 제안 |
|---|---|---|---|
| InternalBarcodeViewModel.cs | 62 | `"manufacturer barcode"` / `"internal barcode"` — 문장 안에 끼워 넣음 ⚠ | 조각 삽입 대신 완성 문장 2개를 각각 키로 |
| InternalBarcodeViewModel.cs | 64 | `$"Prints {code} ({source}), one label per copy."` ⚠ | `barcode.hint.plan.manufacturer` / `barcode.hint.plan.internal` 두 키로 분리 |
| InternalBarcodeViewModel.cs | 73-74 | `$" A second label per copy carries {code}-EA for a single {unit}."` ⚠ | `barcode.hint.unitLabel` |
| InternalBarcodeService.cs | 124 | `Caption: $"LOOSE — 1 {product.Unit}"` | `barcode.lbl.looseCaption` = "LOOSE — 1 {unit}" |
| InternalBarcodeService.cs | 132-135 | `$"Printed {q} label(s) for {code}."` / `$"… and {q} loose-unit label(s)."` ⚠ | 단일/이중 라벨 두 키 |

## 6. 영수증 (인쇄물)

| 파일 | 줄 | 현재 | 제안 |
|---|---|---|---|
| WpfReceiptPrintingService.cs | 48 | `$" {SaleUnitLabel} ({PieceQuantity} units)"` | `receipt.lbl.boxSuffix` = " {unit} ({pieces} units)" |
| WpfReceiptPrintingService.cs | 49 | `$"{name} x{q}{suffix} @ {price} = {total}"` ⚠ | `receipt.line.item` = "{name} x{qty}{suffix} @ {price} = {total}" |
| WpfReceiptPrintingService.cs | 53 | `$"Total: {totalAmount}"` | `receipt.lbl.total` = "Total: {amount}" |
| WpfReceiptPrintingService.cs | 57 | `$"Cash Tendered: {cashTendered}"` | `receipt.lbl.cashTendered` |
| WpfReceiptPrintingService.cs | 58 | `$"Change Due: {changeDue}"` | `receipt.lbl.changeDue` |

## 7. 환불

| 파일 | 줄 | 현재 | 제안 |
|---|---|---|---|
| RefundWindow.xaml.cs | 43-44 | `$"Sold on {date} by {user}  ·  {payment}"` ⚠ | `refund.lbl.saleSummary` |
| RefundWindow.xaml.cs | 105 | `$"Total refund: {amount}"` | `refund.lbl.totalRefund` |
| RefundWindow.xaml.cs | 143 | `$"Refund {total}?\n{stockNote}"` | `refund.dlg.confirmAmount` = "Refund {amount}?" — `stockNote` 는 이미 CSV에 있음 |
| SalesHistoryView.xaml.cs | 45 | `$"Refunded {dialog.RefundedAmount}."` | `refund.msg.done` = "Refunded {amount}." |
| SalesHistoryLineItem.cs | 36 | `$"Refunded {RefundedQuantity}/{Quantity}"` | `salesHistory.sts.refundedPartial` = "Refunded {refunded}/{total}" |

## 8. 판매 내역 상세

| 파일 | 줄 | 현재 | 제안 |
|---|---|---|---|
| SalesHistoryViewModel.cs | 184 | `$"Sold by: {Username}"` | `salesHistory.lbl.soldBy` |
| SalesHistoryViewModel.cs | 185 | `$"Payment: {PaymentMethod}"` | `salesHistory.lbl.payment` |
| SalesHistoryViewModel.cs | 193 | `$"{name} x{q} @ {price} = {total}"` ⚠ | `salesHistory.line.item` |
| SalesHistoryViewModel.cs | 198 | `$"  refunded x{RefundedQuantity}"` | `salesHistory.line.refunded` |
| SalesHistoryViewModel.cs | 204 / 210 / 211 | `$"Total: {t}"`, `$"Refunded: -{r}"`, `$"Net: {n}"` | 각각 키로 |

## 9. 사용자 관리

| 파일 | 줄 | 현재 | 제안 |
|---|---|---|---|
| MainShellViewModel.cs | 38 | `$"Welcome, {loggedInUser.Username}"` ⚠ | `shell.lbl.welcome` = "Welcome, {username}" |
| UserManagementViewModel.cs | 163 | `$"Change role of '{username}' to {role}?"` ⚠ | `userMgmt.dlg.changeRole` |
| UserManagementViewModel.cs | 220 | `$"'{username}' is active again."` ⚠ | `userMgmt.msg.reactivated` |
| UserManagementView.xaml.cs | 103 | `$"Password for '{username}' has been reset."` ⚠ | `userMgmt.msg.passwordReset` |
| ResetPasswordWindow.xaml.cs | 19 | `$"Reset password for: {username}"` | `resetPassword.lbl.target` |

## 10. 리포트 / 복약안내 설정 (지표 문장)

| 파일 | 줄 | 현재 | 제안 |
|---|---|---|---|
| ReportsViewModel.cs | 110-111 | `$"compared with the {what}: {label}"` + `"previous month"` / `"previous period"` ⚠ | 조각 삽입 폐기. 완성 문장 2개(`reports.lbl.comparedMonth` / `reports.lbl.comparedPeriod`)로 |
| ReportsViewModel.cs | 124 | `$"{printed} printed / {sales} antibiotic sales"` ⚠ | `reports.lbl.counsellingSummary` |
| ReportsViewModel.cs | 203 | `$"No sales in {label}."` | `reports.msg.noSalesInPeriod` |
| PeriodChange.cs | 66 | `arrow + " new"` | `reports.lbl.changeNew` |
| PeriodChange.cs | 72 | `arrow + " " + magnitude + "%"` | 숫자 포맷은 로케일 의존 — `CultureInfo.InvariantCulture` 고정 여부 재검토 필요 |
| PeriodChange.cs | 96-98 | `"(" + sign + abs + ")"` | 동일 |
| CounsellingSettingsViewModel.cs | 166-178 | `$"{name} - {status}"`, `"approved"`, `"not reviewed - English only"`, `$"{code} ({langName})"` ⚠ | 상태 문구를 완성 문장으로. 로케일 목록 표시 형식도 키로 |
| CounsellingSettingsViewModel.cs | 192 | `$"{count} antibiotics loaded ({source})."` ⚠ + `"unknown source"` | `counselling.msg.referenceLoaded` |
| CounsellingSettingsViewModel.cs | 211-218 | 지표 요약 문장 전체 (8개 조각 연결) ⚠ | 한 문장씩 별도 키로 쪼개고 `{n}` 치환. 현재 형태로는 번역 불가 |

## 11. 가져오기 / 내보내기 (미리보기 · 결과 대화상자)

| 파일 | 줄 | 현재 | 제안 |
|---|---|---|---|
| BackupExportViewModel.cs | 175-180 | `$"Rows in file          : {n}"` 형태 6줄 — 라벨을 공백으로 고정폭 정렬 ⚠ | 라벨을 키로 분리하고 정렬은 코드가 계산. 한글/크메르어는 글자폭이 달라 현재 방식으로는 줄이 어긋남 |
| BackupExportViewModel.cs | 229-233 | 위와 동일 (재고 5줄) | 동일 |
| BackupExportViewModel.cs | 325 | `$"{title}:"` — `title` 은 `"Errors"` / `"Product not found"` / `"Failures"` | 완성 문장 3개를 각각 키로 |
| BackupExportViewModel.cs | 329 | `$"  {issue}"` (→ `ImportIssue.ToString()`) | 아래 참조 |
| BackupExportViewModel.cs | 334 | `$"  … and {n} more"` | `import.msg.andMore` |
| BackupExportViewModel.cs | 341-342 | `$"Imported : {n} {unitLabel}"`, `$"Failed   : {n}"` ⚠ | `unitLabel` 이 `"products"` / `"batches"` 로 주입됨. 단위별 완성 문장으로 분리 |
| BackupExportViewModel.cs | 354-355 | `$"Import complete — {n} {unit} added."` / `$"Import complete — Success: {s}, Failed: {f}."` ⚠ | 동일 |
| BackupExportViewModel.cs | 291 / 310 | `$"Import error: {ex.Message}"` | `import.err.generic` = "Import error: {detail}" — `ex.Message` 는 번역 불가(그대로 노출) |
| ImportIssue.cs | 10 | `$"Line {LineNumber}: {Reason}"` ⚠ | `import.lbl.line` = "Line {n}: {reason}" |
| InitialImportService.cs | 403 | `$"{column[0]} must be a number greater than zero."` | 컬럼명 주입 — `import.err.mustBeNumber` = "{column} must be a number greater than zero." |
| InitialImportService.cs | 512 | `$"dosage_form must be one of: {목록}."` | `import.err.dosageFormInvalid` |
| InitialImportService.cs | 679 | `$"'{productName}' is not registered. Import products first."` ⚠ | `import.err.productNotRegistered` |
| InitialImportService.cs | 686 | `$"'{productName}' is inactive."` ⚠ | `import.err.productInactive` |
| InitialImportService.cs | 738 | `$"expiry_date is empty. Use '{N}' if the expiry date is unknown."` | `import.err.expiryEmpty` |
| InitialImportService.cs | 763-764 | `$"expiry_date '{text}' is not a valid date. Use yyyy-MM-dd, yyyy-MM, " + $"or '{N}' if unknown."` ⚠ | `import.err.expiryInvalid` |
| InitialImportService.cs | 770 | `$"expiry_date '{text}' is not in the future."` | `import.err.expiryNotFuture` |
| InitialImportService.cs | 818 | `$"'{ProductName}' could not be saved."` ⚠ | `import.err.rowSaveFailed` |
| InitialImportColumns.cs | 93-94 | `$"The file is missing required columns: {…}. " + $"Columns found: {…}."` | 두 문장을 각각 키로 |
| BackupService.cs | 46 | `$"Backup created: {fileName}"` | `backup.msg.backupCreatedAt` |
| BackupService.cs | 85 | `$"{datasets.Count} file(s) exported successfully."` | `backup.msg.filesExported` |

## 12. 복약안내지 (인쇄물)

| 파일 | 줄 | 현재 | 제안 |
|---|---|---|---|
| CounsellingSheetRenderer.cs | 107 | `$"[{DisplayLabel(group)}]  {Pattern(group)}"` | 분류명은 번역 금지(주석 206-209행). 조립 형식만 키로 |
| CounsellingSheetRenderer.cs | 114 | `$"({request.SourceVersion})"` | 그대로 |
| CounsellingSheetRenderer.cs | 188 | `$"{caption} / More information"` ⚠ | 이중언어 조립 |
| CounsellingSheetRenderer.cs | 198 | `$"Source: {SourceVersion}. "` + 면책문 | `sheet.lbl.source` 분리 |
| CounsellingSheetRenderer.cs | 236 | `Bilingual()` → `$"{english} / {localText}"` | 이중언어 구분자(` / `)를 키로. 크메르어처럼 공백 없는 문자에서 가독성 확인 필요 |
| CounsellingSheetRenderer.cs | 242-258 | `AppendField()` — 라벨을 13칸 고정폭으로 패딩 ⚠ | **레이아웃 버그 위험**: `LengthInTextElements` 기준 패딩이라 한글/크메르어처럼 폭 2인 글자에서 열이 어긋난다. 폭 계산을 문자 수가 아니라 표시폭 기준으로 바꿔야 함 |

## 13. 복구 이메일

| 파일 | 줄 | 현재 | 제안 |
|---|---|---|---|
| SmtpEmailSendingService.cs | 29 | `$"Your password recovery code is: {otpCode}\n\nThis code will expire in 10 minutes."` | `email.body.otp` = "Your password recovery code is: {code}" + 만료 안내(이미 CSV에 있음) |
| SmtpEmailSendingService.cs | 60 | `$"Your CamPOS username is: {username}"` | `email.body.username` |

## 14. 기타 예외 메시지

| 파일 | 줄 | 현재 | 제안 |
|---|---|---|---|
| AdjustmentHistoryViewModel.cs | 88 | `$"Error: {ex.Message}"` | `common.err.generic` = "Error: {detail}" |
| HistoryView.xaml.cs | 50, 72 | `$"Error: {ex.Message}"` | 동일 |

## 15. XAML `StringFormat` (리소스 밖에서 조립됨)

| 파일 | 줄 | 현재 |
|---|---|---|
| InventoryStatusView.xaml | 356 | `StringFormat=' / {0}'` (재고 / 기준재고) |
| InventoryStatusView.xaml | 364, 437 | `StringFormat='📦 {0}'` |
| InventoryStatusView.xaml | 366, 439 | `StringFormat='💊 {0}'` |
| InventoryStatusView.xaml | 374 | `StringFormat='${0:F2}'` — **통화기호 `$` 하드코딩**. 캄보디아 배포 시 통화 표시가 틀림 |
| ProductListView.xaml | 144, 148 | `StringFormat={}{0:N2}` — 숫자 서식은 현재 로케일을 따름 |
| AdjustmentView.xaml | 89 | `StringFormat='{}{0} units per box.'` |

---

## 우선순위 제안

1. **6·7·12** (영수증 · 환불 · 복약안내지) — 손님에게 나가는 인쇄물이고 금액·복약 지시가 걸려 있다.
2. **4** (`{unit}s` 영어 복수형) — 코드에서 `s` 를 떼지 않으면 어떤 언어로도 옳게 나오지 않는다.
3. **12 (CounsellingSheetRenderer 242-258)** 과 **15 (`${0:F2}`)** — 번역이 아니라 **버그**다. 각각 열 정렬 깨짐과 통화기호 오류를 일으킨다.
4. 나머지 — 관리자/리포트 영역이라 후순위.
