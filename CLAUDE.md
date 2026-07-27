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

# Build a single class library (faster feedback on Application/DataAccess changes)
dotnet build PharmaPOS.Application

# Publish (self-contained single-file win-x64; FolderProfile.pubxml targets the user's Desktop)
dotnet publish PharmaPOS.Wpf -p:PublishProfile=FolderProfile
```

The WPF project directory is `PharmaPOS.Wpf`, but `AssemblyName` is pinned to `PharmaPOS`, so build output and the published executable are `PharmaPOS.dll` / `PharmaPOS.exe`. `RootNamespace` is deliberately left as `Lightweight_Digital_Inventory_Management___POS_System` to match the existing C# and XAML namespaces — the folder rename did not touch namespaces.

There is **no test project** and no linter configured. Verification is: `dotnet build` plus running the app. The build currently emits 4 `CS0105` duplicate-using warnings (`ServiceCollectionExtensions.cs:12`, `LoginView.xaml.cs:8`) — pre-existing, harmless.

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
| `PharmaPOS.Wpf` | WPF UI (Views/ViewModels), DI composition root, and the platform-specific service implementations that Application can't provide (DPAPI, receipt printing). |

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

- ViewModels registered in DI (`LoginViewModel`, `InitialSetupViewModel`, `ProductListViewModel`, `InventoryStatusViewModel`) are resolved in the View's constructor.
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

### Security

- Passwords and security-question answers: bcrypt via `BCryptPasswordHasher`. Answers are normalized (`Trim().ToLowerInvariant()`) before hashing.
- Login deliberately returns the same `InvalidCredentials` error for unknown username and wrong password.
- The SMTP app password for recovery email is encrypted with **Windows DPAPI** (`DataProtectionScope.CurrentUser`) — this is why the recovery data protector lives in the WPF project rather than Application, and why a copied database is undecryptable on another machine or Windows account.
- `PasswordRecoveryService` holds pending OTPs and verified tokens in `static` in-memory dictionaries, so recovery state does not survive an app restart.

## Known incomplete areas

Marked with `TODO` in source: label-printer hardware integration (`InternalBarcodeService.PrintLabelAsync` validates input only), receipt printing (`SimulatedReceiptPrintingService`), and sale `notes` (no column exists on `Stock_Transaction`).

## Repository notes

Git repository on branch `master`, pushed to the private remote `camdianze/campos`. `.gitignore` at the root excludes build output (`bin/`, `obj/`), `.vs/`, `*.user`, and — importantly — `*.db` / `*.db-wal` / `*.db-shm`, since backup-export files contain real account hashes and sales records.

`Converters.zip` in the WPF project is tracked but is a stray artifact, not a build input.
