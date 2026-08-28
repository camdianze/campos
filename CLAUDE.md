# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

PharmaPOS — a Windows desktop (WPF, .NET 10) inventory management and point-of-sale system for a single pharmacy/health facility. Single-machine deployment: a local SQLite file, no server, no network dependency except optional SMTP for password recovery.

Code comments and design rationale are written in Korean; all user-facing strings (validation messages, labels) are in English. Match that convention when adding code.

## Build & Run

```powershell
# Build everything
dotnet build PharmaPOS.slnx

# Run the app
dotnet run --project PharmaPOS.Wpf

# Run the tests
dotnet test PharmaPOS.Tests

# Build a single class library (faster feedback on Application/DataAccess changes)
dotnet build PharmaPOS.Application

# Publish (self-contained single-file win-x64; FolderProfile.pubxml targets the user's Desktop)
dotnet publish PharmaPOS.Wpf -p:PublishProfile=FolderProfile
```

The WPF project directory is `PharmaPOS.Wpf`, but `AssemblyName` is pinned to `PharmaPOS`, so build output and the published executable are `PharmaPOS.dll` / `PharmaPOS.exe`. `RootNamespace` is deliberately left as `Lightweight_Digital_Inventory_Management___POS_System` to match the existing C# and XAML namespaces — the folder rename did not touch namespaces.

No linter is configured. Verification is: `dotnet build`, `dotnet test`, plus running the app. The build emits `CS0105` duplicate-using warnings (`ServiceCollectionExtensions.cs`, `LoginView.xaml.cs`) — pre-existing, harmless.

`PharmaPOS.Tests` (xUnit) covers the antibiotic counselling feature only — matching, locale fallback, sheet rendering, the counselling orchestration, and SQLite schema/migration/seed-loading integration. **The rest of the codebase has no tests.** It exists because the counselling matching rules (salt-form stripping, spelling variants, the topical/systemic split) are the kind of logic that fails silently in production.

### Runtime state

The database is created on first launch at `%APPDATA%\PharmaPOS\pharmapos.db` (see [App.xaml.cs](PharmaPOS.Wpf/App.xaml.cs)). Delete that folder to reset the app to its first-run state, which forces the initial-setup screen (facility + first Administrator) instead of login.

## Architecture

Four projects in a strict clean-architecture dependency chain. **Dependencies point inward only** — Domain knows nothing about anyone else, and Application never references DataAccess.

```
WPF app ──> Application ──> Domain
   └──────> DataAccess  ──> Application (implements its repository interfaces)
                         └> Domain
```

| Project | Role |
|---|---|
| `PharmaPOS.Domain` | Entities (`Product`, `User`, `Inventory`, `StockTransaction`, `Facility`) and enums. No dependencies, no logic. |
| `PharmaPOS.Application` | Business rules. Service implementations + the `I*Repository` interfaces they depend on (`Repositories/`). Also owns password hashing/policy and SMTP abstractions (`Security/`, `PasswordPolicy/`). |
| `PharmaPOS.DataAccess` | SQLite implementations of the repository interfaces, schema creation, backup/export. |
| `PharmaPOS.Wpf` | WPF UI (Views/ViewModels), DI composition root, and the platform-specific service implementations that Application can't provide (DPAPI, receipt printing, counselling-sheet printing). Also ships the field-replaceable data files (`seeds/`, `locales/`). |
| `PharmaPOS.Tests` | xUnit. Covers the antibiotic counselling feature only; references Domain/Application/DataAccess. |

Repository interfaces live in **Application**, implementations in **DataAccess** — this is what keeps the dependency arrow inverted. When adding a data operation, add the interface next to its consumers in `PharmaPOS.Application/Repositories/`, implement it in `PharmaPOS.DataAccess/Repositories/`, then register it.

### Composition root

[Composition/ServiceCollectionExtensions.cs](PharmaPOS.Wpf/Composition/ServiceCollectionExtensions.cs) is the *only* place an interface is bound to an implementation. Lifetimes follow a deliberate rule: infrastructure and stateless policy objects are `Singleton`; repositories, services, and ViewModels are `Transient`. Any new service must be registered here or `GetRequiredService` throws at runtime.

The container is exposed as the static `App.Services`. Views resolve from it directly in their constructors — there is no view-locator or navigation framework.

### Navigation

Hand-rolled and imperative. A single `MainWindow` has its `Content` swapped:

- [App.xaml.cs](PharmaPOS.Wpf/App.xaml.cs) picks `InitialSetupView` or `LoginView` based on `IInitialSetupRepository.IsSetupCompleteAsync()`.
- On successful login, `LoginView` builds `MainShellViewModel` (carrying the logged-in `User`) and swaps in `MainShellView`.
- [Shell/MainShellView.xaml.cs](PharmaPOS.Wpf/Shell/MainShellView.xaml.cs) is the navigation hub — one click handler per destination, each resolving services and constructing the target ViewModel by hand.

**ViewModels never navigate.** They raise C# events (`NavigateBack`, `LoginSucceeded`, `RequestAddUserDialog`, …) and the code-behind handles them. Two wiring styles coexist:

- ViewModels registered in DI (`LoginViewModel`, `InitialSetupViewModel`) are resolved in the View's constructor. `ProductListViewModel` and `InventoryStatusViewModel` are built by their own Views (`ProductListView.Create()`, the `InventoryStatusView` constructor) because their inline Stock-IN/Adjustment panels need `facilityId`/`userId`, which DI cannot supply.
- Everything else uses an `AttachViewModel(vm)` method on the View that subscribes to the events and sets `DataContext`. This exists because those ViewModels take runtime constructor args (`facilityId`, `userId`, `role`) that DI can't supply.

The logged-in user is threaded manually as `FacilityId` / `UserId` / `Role` constructor parameters — there is no ambient session or current-user service. Preserve this when adding screens.

MVVM base is minimal and local: `ViewModelBase` (`SetProperty`) and `RelayCommand` in [ViewModels/Base/](PharmaPOS.Wpf/ViewModels/Base/). No MVVM toolkit, no source generators.

### Result objects, not exceptions

Every service method returns a result type (`SaleResult`, `StockInResult`, `AuthenticationResult`, `ProductSaveResult`, …) with private constructors and static factories — `Success()`, `Failure(message)`, and sometimes `NeedsConfirmation(message)` for flows needing a user prompt (e.g. selling below cost price). Services catch repository exceptions and convert them to `Failure` with an English, user-safe message. ViewModels display `result.Message` directly. Follow this pattern rather than throwing across the service boundary.

### Data access

Raw ADO.NET over `Microsoft.Data.Sqlite` — **no ORM, no Dapper**. Repositories hand-write SQL with `$param` placeholders and map readers by ordinal index. Conventions worth knowing before writing a query:

- Table/column names are `snake_case` (`Product_Master`, `current_quantity`); C# properties are PascalCase — the mapping is manual in each repository.
- All timestamps (`created_at`, `expiry_date`, `transaction_time`, `updated_at`) are **Unix epoch milliseconds** stored as `INTEGER`. Convert with `DateTimeOffset.ToUnixTimeMilliseconds()`. The UI converts back via `UnixToDateConverter`.
- Enums (`UserRole`, `EntityStatus`, `TransactionType`, `PaymentMethod`) persist as `TEXT` via `.ToString()` and parse back by name.
- `decimal` prices are stored as SQLite `REAL`, so reads go through `(decimal)reader.GetDouble(i)`.
- [SqliteConnectionFactory](PharmaPOS.DataAccess/Database/SqliteConnectionFactory.cs) applies `PRAGMA journal_mode = WAL` and `PRAGMA foreign_keys = ON` on **every** connection — always obtain connections through it, never construct `SqliteConnection` directly (the exception is `BackupRepository`, which deliberately opens raw read-only connections against external files).
- Connections are per-operation (`using var connection = ...` inside each repository method), not shared. Multi-statement work that must be atomic opens an explicit `BeginTransaction()` — see [SaleRepository.SaveSaleAsync](PharmaPOS.DataAccess/Repositories/SaleRepository.cs), which re-checks stock inside the transaction before decrementing, guarding against stock taken between "add to cart" and "confirm sale".

### Schema and migrations

[DatabaseInitializer](PharmaPOS.DataAccess/Database/DatabaseInitializer.cs) runs on every startup. `CREATE TABLE IF NOT EXISTS` handles fresh installs; because that does nothing for already-deployed databases, `ApplyMigrations()` adds new columns via `AddColumnIfMissing` (a `PRAGMA table_info` check then `ALTER TABLE ADD COLUMN`). **Adding a column to an existing table requires editing both the `CREATE TABLE` statement and `ApplyMigrations()`** — there is no migration history table or version number.

Inventory is tracked per batch: `Inventory` is unique on `(facility_id, product_id, batch_number)` with its own `expiry_date`. Batch lists are ordered `expiry_date ASC` to support first-expired-first-out picking.

### Sales and refunds

There is no sale header table. A "sale" is the set of `Stock_Transaction` rows sharing `(transaction_time, user_id)` — that pairing is how the sales history, refunds, and the report's transaction count all group lines into one sale.

Refunds are appended, never edited: a refund writes a `TransactionType.Refund` row whose `quantity` and `total_amount` are **negative** and whose `related_transaction_id` points at the original `StockOut` line. Consequences to preserve when touching sales queries:

- **Any new revenue/quantity aggregate must filter `transaction_type IN ('StockOut', 'Refund')`**, not `= 'StockOut'`. The negative rows then net out on their own. Filtering to `StockOut` alone silently reports gross sales.
- Counts of *sales* (rows or distinct transactions) must still be limited to `StockOut` — a refund is not another sale.
- How much of a line has been refunded is never stored; it is counted from the refund rows pointing at it ([RefundRepository](PharmaPOS.DataAccess/Repositories/RefundRepository.cs)), and re-checked inside the write transaction so two open windows cannot over-refund.
- Returned stock goes back as loose units, never as sealed boxes (`BoxUnitMath.AddUnits`), and the "return to stock" checkbox may be off entirely — in that case the money is refunded, stock is untouched, and the only trace is the `(not returned to stock)` marker appended to `reason`.
- The antibiotic counselling report deliberately stays gross (`StockOut` only): it counts counselling events, which a refund does not undo. The 12-month trend chart on the same screen follows the identical rule (gross, `UNMATCHED` excluded) — if the two diverge, the table total and the chart total stop agreeing and neither can be trusted. The reports screen splits left/right by subject — left is the whole pharmacy (product ranking, sales trend), right is antibiotics only (group share, ingredient table, antibiotic trend); keep new panels on the side they belong to. **Export CSV follows the same division**, and here the split is a data-governance boundary, not a formatting choice. It asks for a folder (like the Import/Export screen) and writes two self-contained files, `report_sales_*.csv` and `report_antibiotics_*.csv`, each repeating the period header.

**`report_antibiotics_*.csv` leaves the pharmacy** — it is a mandatory AMR research submission. `report_sales_*.csv` never does; it is the pharmacy's own book, and revenue is something a pharmacy is reluctant to hand over. So the antibiotic file **must not contain a single monetary figure** — no per-ingredient revenue, no period totals, no product names. Antimicrobial-consumption indicators are counted in units (and DDD derived from them), so the research side has no use for money; meanwhile a pharmacy that notices its takings riding along with a compulsory submission starts resisting the submission itself, and the research side loses more than it gained. Per-ingredient revenue lives in the sales file instead, since the product ranking cannot supply it (several products commonly share one ingredient).

That is why the antibiotic file is built by [AntibioticExportCsv](PharmaPOS.Application/Reports/AntibioticExportCsv.cs) in **Application**, not in the ViewModel next to the sales CSV: a comment cannot stop someone adding an `Amount` column back, so the rule sits where `AntibioticExportCsvTests` can hold it — those tests fail if any money figure, monetary column name, or product name reaches the file.

The file identifies the pharmacy by a **pseudonymous site code** (`research.site_code`, entered on the Antibiotic Counselling screen and printed as the `Site code` line). The code reveals nothing on its own — the code-to-pharmacy register is held by the research body, the same arrangement the licence serial already uses (see the [LicensePayload](PharmaPOS.Application/Licensing/LicensePayload.cs) comment: the serial carries no customer name, the issuing ledger holds the mapping). Consequently **facility name, country, district and facility type must stay out of the file**: combined with a low-volume month they narrow the source to a handful of pharmacies and the pseudonym stops being one. Regional roll-ups belong in the research body's register, not in the export. An empty code prints `(not set)` rather than blocking the export — a pharmacy may legitimately run it for itself before enrolling — and the export message says so. The code is deliberately shown in the settings screen: a pharmacy that later discovers a hidden identifier in what it submitted stops submitting. Both bottom charts share one 12-month window and one set of plot dimensions (`TrendChartMetrics`, mirrored by the fixed 18/132/20 row heights in XAML) so their baselines and month ticks line up. The charts bucket by month in **local** time (`strftime(..., 'localtime')`) because every other date boundary in the app is local midnight; bucketing in UTC would push sales near midnight into the neighbouring month. The sales chart is **net** of refunds (`transaction_type IN ('StockOut','Refund')`, negative rows self-cancel) to agree with the Sales Amount card, while the antibiotic chart stays gross — the two charts differ here on purpose.

### Sales receipts

Everything printed on a receipt — pharmacy name, address, phone, closing line, VAT number, language, paper width, exchange rate — comes from settings. **No pharmacy detail is hard-coded**, and the code defaults for those fields are deliberately *empty* rather than a sample pharmacy.

The 21 setting keys are fixed and listed in [AppSettingKeys](PharmaPOS.Application/Settings/AppSettingKeys.cs) (`shop.*`, `print.*`, `currency.*`, `receipt.*`, `vat.*`). They live in the existing `App_Setting` table, which gained `value_type` and `updated_by` columns for them. [ReceiptSettingsService](PharmaPOS.Application/Receipts/ReceiptSettingsService.cs) reads them through a 10-minute [ReceiptSettingsCache](PharmaPOS.Application/Receipts/ReceiptSettingsCache.cs) (Singleton; the service itself stays Transient) and never throws — a key that is missing or unparseable falls back to its own default, and the rest of the settings still apply. A stored empty string is a *value*, not a missing key: clearing the address must not resurrect a default.

**Saving is refused in the Application layer for non-Administrators**, not only by hiding the button. `SaveAsync` takes the acting user's role, validates, and returns `ReceiptSettingsSaveResult` carrying per-field messages keyed by setting key, so the screen can put each message next to the input it belongs to.

- **Receipt numbers** are `{prefix}-{YYYYMMDD}-{0001}`. The sequence lives in `Receipt_Counter`, keyed by prefix plus the period implied by `receipt.resetCycle` — changing the cycle changes the key, so an old cycle's counter is left intact rather than rewound. The number issued for a sale is recorded in `Receipt_Number` under `"{transaction_time}|{user_id}"` — the same pairing that identifies a sale everywhere else — so a reprint from Sales History gets the number it got the first time and does *not* consume a new one. Both the increment and the mapping happen in one transaction.
- **Dates and reset periods are Phnom Penh time** ([PhnomPenhClock](PharmaPOS.Application/Receipts/PhnomPenhClock.cs)), never the PC's time zone. Cambodia has no DST, so a fixed +07:00 offset is the fallback when the time-zone database has neither name.
- **Riel** is rounded to `currency.rounding` (100 / 500 / 0) because small coins are not in circulation — printing an unrounded figure means the receipt disagrees with the money that changed hands.
- **VAT is treated as included in the total**, not added on top. The mockup added it, but `TotalAmount` is money already taken; adding tax to it would make the receipt total, the cash tendered and the change stop reconciling.
- **Strings come from `receipt.*` resource keys**, never from concatenation. English lives in [ReceiptStrings](PharmaPOS.Application/Receipts/ReceiptStrings.cs); Khmer comes from the same `locales/{bcp47}.json` files the counselling sheet uses, under the same rule — `review_status` must be `approved` or not one Khmer character prints. Variables are `{name}` placeholders inside a whole sentence, because Khmer word order differs from English. Medicine names are never translated (INN as-is) and figures are always Arabic numerals; only dosage form and unit are localised, and `receipt.show.unit` drops them.
- [ReceiptRenderer](PharmaPOS.Application/Receipts/ReceiptRenderer.cs) draws fixed-width text: 48 columns for 80 mm, 32 for 58 mm. The item name gets its own line and the figures a second one, because truncating a medicine name is the one thing a receipt must not do. Documents containing Khmer letters ask `ThermalTextPrinter` for 1.75× line spacing so stacked vowel marks are not clipped; `Noto Sans Khmer` heads the font fallback list.
- The admin screen is a **section inside the admin dashboard**, not a screen of its own: form on the left, live preview on the right, driven by the very same `ReceiptRenderer`, so the preview cannot drift from the paper. Leaving the dashboard or closing the window with unsaved changes prompts first.

### Import / Export screen

The screen (reached from the admin dashboard) is split by direction of data flow: **left = import** (file → app), **right = export/backup** (app → file), and **restore** sits alone in a danger zone below because it is the only one that throws the current data away. There is exactly one importer, one exporter, and one restore — an earlier version had two overlapping product importers and three muddled file features.

**Import** ([InitialImportService](PharmaPOS.Application/Import/InitialImportService.cs)) takes one CSV/XLSX file and is run twice: step 1 products, step 2 their batches.

- Existing products are **updated, not skipped**: only the columns the file fills are written, so a row carrying just a product name (the second batch row of the same product) changes nothing. That also means the import can never blank a value — clearing a field is done in the product screen.
- Column names accept both spellings (`safety_stock`/`safety_stock_level`, `loose_unit_price`/`unit_selling_price`), so an exported products file can be edited and imported straight back.

**Export** writes one file per checked dataset — products, inventory, sales history — via per-dataset queries in [BackupRepository](PharmaPOS.DataAccess/Repositories/BackupRepository.cs), not raw table dumps: raw tables carry product IDs no one can read, and `Users` holds password hashes that have no business leaving the machine. The products query's column aliases are deliberately the import's header names. Export files cannot be restored; only the full `.db` backup can.

- The file is parsed to `ImportSourceRow` in the WPF layer ([ImportFileReader](PharmaPOS.Wpf/Services/ImportFileReader.cs)); every rule lives in Application, so CSV and Excel behave identically and the rules are unit-tested.
- Nothing is written until the user confirms a preview. `Plan…` computes what would happen, `Apply…` performs exactly that plan — never recompute between the two.
- **`quantity` in the file is loose units, not boxes** (unlike the Stock-IN screen, where quantity means boxes). Counting a shelf can produce a box and a half; the import splits units into boxes + loose via `BoxUnitMath.Split`.
- Products are saved through `ProductService` and batches through `IStockInRepository` with `TransactionType.StockIn` — the import has no private write path, and initial stock is ordinary stock-in in the ledger.
- Re-importing the same file is blocked by a SHA-256 of the file contents recorded in `Import_History`, keyed `(import_type, file_hash)` so the same file can be used for step 1 and then step 2.

**`expiry_date` 0 means "unknown", not 1970-01-01.** Paper-managed stock often has no expiry date left, so `N` in the file stores `Inventory.NoExpiryDate` (0), and the inventory export writes `N` back out. Any query or check that compares expiry to a date must exclude 0 explicitly — otherwise every such batch reads as long expired: the alert query, the inventory `Expired` filter, and the POS expiry block all filter it out, and batch pickers sort it last so FEFO still picks dated stock first.

### Security

- Passwords and security-question answers: bcrypt via `BCryptPasswordHasher`. Answers are normalized (`Trim().ToLowerInvariant()`) before hashing.
- Login deliberately returns the same `InvalidCredentials` error for unknown username and wrong password.
- The SMTP app password for recovery email is encrypted with **Windows DPAPI** (`DataProtectionScope.CurrentUser`) — this is why the recovery data protector lives in the WPF project rather than Application, and why a copied database is undecryptable on another machine or Windows account.
- `PasswordRecoveryService` holds pending OTPs and verified tokens in `static` in-memory dictionaries, so recovery state does not survive an app restart.

## Known incomplete areas

Marked with `TODO` in source: sale `notes` (no column exists on `Stock_Transaction`).

The app version shown on screen and stamped into exported file names comes from one place, [AppVersion](PharmaPOS.Application/AppVersion.cs). It used to live only as a XAML resource, which Application could not read, so exported files carried no version at all. Reports carry it inside the file as well; products and inventory exports carry it only in the file name, because those files must re-import and the importer requires the header on the first line.

Receipts, counselling sheets and barcode labels all print through [ThermalTextPrinter](PharmaPOS.Wpf/Services/ThermalTextPrinter.cs) — the Windows print pipeline (`FixedDocument`) rather than ESC/POS, sized from what the driver reports so the paper width is not hard-coded. (`print.width` only decides how many characters a receipt line holds — 48 or 32 — not the physical page size, which stays whatever the driver says.) It swallows every printer failure and returns `false`: the receipt and counselling callers run *after* the sale is committed, so a missing printer must never throw into a finished transaction. No path opens a print dialog or asks which printer to use — everything goes to the default. **Not verified against a physical thermal printer.**

Labels draw their own bars: [Code128Encoder](PharmaPOS.Application/Products/Code128Encoder.cs) turns a code into module widths and [WpfLabelPrintingService](PharmaPOS.Wpf/Services/WpfLabelPrintingService.cs) renders them as rectangles. No barcode font and no package — the same reasoning that kept ESC/POS out. Code 128-B specifically, because internal barcodes look like `INT-00000146` (and `-EA` for loose units), which digit-only symbologies cannot hold. The pattern table and the check-digit rule are covered by `Code128EncoderTests`; a wrong check digit prints bars that look fine and simply never scan.

One counselling rule is easy to break by accident: **a sheet is per product per sale, not per sale line.** Quantity is never consulted, and a product split across two cart lines (two batches, or box + loose) still prints one sheet — `CounsellingService.PrepareAsync` merges those lines into a single candidate. The merged lines are still logged individually (`CounsellingCandidate.TransactionIds`), because the report counts antibiotic sales and counselling coverage per sale line.

**A counselling notice is shown for every matched antibiotic sale and cannot be switched off.** The print setting only decides whether paper follows: `always` prints after the notice, `ask` offers Print/Skip on it. The old `never` value is gone — it silenced the notice along with the printing, which left the feature with nothing to do. A database still holding `never` fails to parse and falls back to `always`, which is the intended outcome of removing it.

Antibiotic counselling (AMR) additionally needs:

- **Counselling is keyed off the antibiotic match alone.** `Product_Master.dosage_form` exists ([DosageForm](PharmaPOS.Domain/Enums/DosageForm.cs), a fixed list, optional) but is deliberately **not** consulted anywhere in the counselling path — a sheet is decided by whether `generic_name`/`atc_code` matches the AWaRe list, nothing else. Do not wire dosage form into it without an explicit decision: it is a late, optional column, so most existing products are still blank, and treating blank as a route would silently downgrade warnings.
- **Topical antibiotics are not excluded in practice.** Every row in the shipped WHO file is `is_systemic = true` (the AWaRe list only covers systemic agents), so the topical-exclusion path never fires and an ointment can get a counselling sheet. This is tolerable — the sheet's advice is not wrong for topical agents. If it needs fixing, add the few topical rows to the CSV with `is_systemic = false`; do **not** solve it by requiring an ATC code on every product, and do **not** solve it with `dosage_form` (see the bullet above). Matching works on `generic_name` alone; ATC is optional throughout.
- Two WHO entries are classified by route (Minocycline `J01AA08`, Fosfomycin `J01XX01`: IV = RESERVE, oral = WATCH). Lookups deliberately pick the stricter group instead of reading `dosage_form`, for the reason in the first bullet. Two rows out of 384.
- **QR image.** The sheet prints `[QR]` plus the configured URL as text; encoding an actual QR needs a package (none is referenced) and a decision on what the URL points to.
- Counselling sheets print through the Windows print pipeline ([WpfCounsellingSheetPrintingService](PharmaPOS.Wpf/Services/WpfCounsellingSheetPrintingService.cs)), not ESC/POS. It has not been verified against a physical thermal printer.

## Product photos

`Product_Master` carries the photo itself (`photo` BLOB, `photo_updated_at`). Files on disk were the obvious alternative and are the wrong one here: a backup is a copy of `pharmapos.db` and nothing else, so a restored backup would come back with every photo missing and no sign of why.

The cost of that choice is size, so it is paid at the door — [ProductPhotoService](PharmaPOS.Application/Products/ProductPhotoService.cs) shrinks the longest edge to 800px and re-encodes as JPEG before storing, and rejects source files over 20MB before decoding them. A phone photo is 3–5MB; 300 of those stored raw would outgrow what a single-file backup can carry.

**The photo is never on the `Product` entity.** The product list reads hundreds of rows and would drag the images along with them, so `IProductRepository` exposes `GetPhotoAsync`/`SavePhotoAsync` separately and the detail screen loads the image after the screen is up. `UpdateAsync` does not touch the photo columns either, which is why editing a product cannot wipe its photo.

Encoding lives in WPF (`WpfPhotoEncoder`) for the same reason DPAPI and printing do — decoding and resizing an image is `PresentationCore`, which Application must not know about.

Photos are set on the product screen one at a time, or in bulk from the Import / Export screen (step ③). The bulk import takes a **folder**, not a file: a CSV cell cannot hold an image (a 200KB photo is 270KB of base64 against Excel's 32,767-character cell limit), and naming each file after the barcode means the survey sheet needs no new column. Matching goes manufacturer barcode → internal barcode → the same minus `-EA` → product name; barcodes win because they are unique and names are not. Unlike steps ① and ②, re-importing the same folder is **not** blocked — that block exists to stop stock doubling, and photos overwrite rather than accumulate. Formats Windows cannot decode without an extra codec (HEIC above all, which is what an iPhone produces by default) are reported by name rather than skipped in silence, because otherwise a folder full of photos imports nothing and says nothing.

## Tracing stock

Every `Stock_Transaction` row records `stock_before` and `stock_after` — that batch's balance either side of the transaction. Sales, stock-in, adjustments and refunds all write them; leaving any one out would break the chain during normal operation and make the whole thing useless.

**Both values are read from `Inventory`, never computed.** Writing `after = before - quantity` makes `before + quantity == after` a tautology that can never fail, and the thing worth catching is precisely when the ledger's quantity and what actually happened to stock disagree. [StockLedgerTrace](PharmaPOS.DataAccess/Repositories/StockLedgerTrace.cs) re-reads the balance after the update and patches the ledger row inside the same transaction — the four writers order their inventory work and ledger insert differently, and reordering them all was the riskier change.

Reading down a batch's rows in time order, the first place `stock_after` of one row disagrees with `stock_before` of the next is where stock moved without a ledger row. Rows written before these columns existed are NULL and export as blank; **zero would read as "stock was zero".**

## Licensing

Activation is offline and signature-based: the app carries only the **public** key (`LicenseService.PublicKeyBase64`) and answers yes/no; the private key that mints codes is never in this repo or in any build.

**The issuing tool lives in a separate repository** — `campos-license-issuer` (was `tools/LicenseIssuer`). It signs codes, keeps the ledger, and uploads issuances to Firestore. It moved out so that handing this source to anyone — a contractor, a reviewer, a successor — does not hand over the tool that mints licences for it; the Firestore dependency left the product solution with it. Nothing became safer by moving: the private key already lived in `%APPDATA%\PharmaPOS.Issuer`, outside both repos, and losing that file permanently ends new issuance (existing customers keep working from their `license.dat`).

**Three files are shared between the two repos as byte-identical copies** — `Base32.cs`, `LicensePayload.cs`, `LicenseCodeCodec.cs` under `PharmaPOS.Application/Licensing/`, namespace included so a plain diff answers whether they have drifted. Each carries a header comment saying so.

Drift here fails in the worst possible way: the issuer still produces a well-formed code, the ledger still records it, and nothing looks wrong until a customer types it in and is refused — at the counter, in front of them, with only new customers affected. So both repos ship the same `license-vectors.json` and assert the same things against it (payload byte layout, decode round-trip, signature verification with the *test* key, and that a tampered code fails). Change any of the three files and you must change the other copy too; whichever side you forget breaks its own build first. `LicenseService.cs` is deliberately **not** shared — only the issuer signs, only the app verifies.

## Repository notes

**This project uses `main` only.** There is no branching workflow — commit directly to `main` and push to `origin main`. Do not create feature branches, and do not open pull requests, unless explicitly asked. (This overrides any default "branch before committing on the default branch" behavior.)

Pushed to the private remote `camdianze/campos`. `.gitignore` at the root excludes build output (`bin/`, `obj/`), `.vs/`, `*.user`, and — importantly — `*.db` / `*.db-wal` / `*.db-shm`, since backup-export files contain real account hashes and sales records.

`Converters.zip` in the WPF project is tracked but is a stray artifact, not a build input.
