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
- The antibiotic counselling report deliberately stays gross (`StockOut` only): it counts counselling events, which a refund does not undo.

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

Marked with `TODO` in source: label-printer hardware integration (`InternalBarcodeService.PrintLabelAsync` validates input only), receipt printing (`SimulatedReceiptPrintingService`), and sale `notes` (no column exists on `Stock_Transaction`).

One counselling rule is easy to break by accident: **a sheet is per product per sale, not per sale line.** Quantity is never consulted, and a product split across two cart lines (two batches, or box + loose) still prints one sheet — `CounsellingService.PrepareAsync` merges those lines into a single candidate. The merged lines are still logged individually (`CounsellingCandidate.TransactionIds`), because the report counts antibiotic sales and counselling coverage per sale line.

Antibiotic counselling (AMR) additionally needs:

- **Topical antibiotics are not excluded in practice.** Every row in the shipped WHO file is `is_systemic = true` (the AWaRe list only covers systemic agents), so the topical-exclusion path never fires and an ointment can get a counselling sheet. This is tolerable — the sheet's advice is not wrong for topical agents. If it needs fixing, add the few topical rows to the CSV with `is_systemic = false`; do **not** solve it by requiring an ATC code on every product. Matching works on `generic_name` alone; ATC is optional throughout.
- Two WHO entries are classified by route (Minocycline `J01AA08`, Fosfomycin `J01XX01`: IV = RESERVE, oral = WATCH). `Product_Master` has no dosage form, so lookups deliberately pick the stricter group. Two rows out of 384 — not worth a schema column.
- **QR image.** The sheet prints `[QR]` plus the configured URL as text; encoding an actual QR needs a package (none is referenced) and a decision on what the URL points to.
- Counselling sheets print through the Windows print pipeline ([WpfCounsellingSheetPrintingService](PharmaPOS.Wpf/Services/WpfCounsellingSheetPrintingService.cs)), not ESC/POS. It has not been verified against a physical thermal printer.

## Repository notes

**This project uses `main` only.** There is no branching workflow — commit directly to `main` and push to `origin main`. Do not create feature branches, and do not open pull requests, unless explicitly asked. (This overrides any default "branch before committing on the default branch" behavior.)

Pushed to the private remote `camdianze/campos`. `.gitignore` at the root excludes build output (`bin/`, `obj/`), `.vs/`, `*.user`, and — importantly — `*.db` / `*.db-wal` / `*.db-shm`, since backup-export files contain real account hashes and sales records.

`Converters.zip` in the WPF project is tracked but is a stray artifact, not a build input.
