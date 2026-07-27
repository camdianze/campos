using Microsoft.Data.Sqlite;

namespace PharmaPOS.DataAccess.Database;

/// <summary>
/// 앱 최초 실행 시 필요한 테이블을 생성하고, 기존 DB에는 스키마 마이그레이션을 적용한다.
/// </summary>
public class DatabaseInitializer
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public DatabaseInitializer(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public void Initialize()
    {
        using var connection = _connectionFactory.CreateOpenConnection();

        CreateFacilityTable(connection);
        CreateUsersTable(connection);
        CreateProductMasterTable(connection);
        CreateInternalBarcodeSequenceTable(connection);
        CreateInventoryTable(connection);
        CreateStockTransactionTable(connection);

        ApplyMigrations(connection);
    }

    // ... CreateFacilityTable, CreateProductMasterTable, CreateInternalBarcodeSequenceTable,
    //     CreateInventoryTable, CreateStockTransactionTable은 기존과 동일 (변경 없음) ...

    private static void CreateUsersTable(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS Users (
                user_id       TEXT PRIMARY KEY,
                facility_id   TEXT NOT NULL,
                username      TEXT NOT NULL COLLATE NOCASE UNIQUE,
                password_hash TEXT NOT NULL,
                role          TEXT NOT NULL,
                status        TEXT NOT NULL,
                created_at    INTEGER NOT NULL,
                FOREIGN KEY (facility_id) REFERENCES Facility(facility_id)
            );
            """;
        command.ExecuteNonQuery();
    }

    /// <summary>
    /// 이미 배포되어 실행 중인 기존 DB 파일에도 안전하게 새 컬럼을 추가한다.
    /// CREATE TABLE IF NOT EXISTS는 "테이블이 아예 없을 때"만 동작하고,
    /// 이미 있는 테이블에 컬럼을 추가해주지는 않기 때문에 별도 처리가 필요하다.
    /// </summary>
    private static void ApplyMigrations(SqliteConnection connection)
    {
        AddColumnIfMissing(connection, "Users", "security_question", "TEXT");
        AddColumnIfMissing(connection, "Users", "security_answer_hash", "TEXT");
        AddColumnIfMissing(connection, "Users", "recovery_email", "TEXT");
        AddColumnIfMissing(connection, "Users", "email_provider", "TEXT");
        AddColumnIfMissing(connection, "Users", "email_app_password_encrypted", "TEXT");
        AddColumnIfMissing(connection, "Users", "smtp_host", "TEXT");
        AddColumnIfMissing(connection, "Users", "smtp_port", "INTEGER");
    }

    private static void AddColumnIfMissing(SqliteConnection connection, string tableName, string columnName, string columnType)
    {
        // PRAGMA table_info로 그 테이블에 이미 이 컬럼이 있는지 먼저 확인한다.
        // (SQLite 버전에 따라 "ADD COLUMN IF NOT EXISTS" 문법 지원이 다를 수 있어,
        //  버전에 상관없이 항상 동작하는 이 방식을 택했다.)
        using (var checkCommand = connection.CreateCommand())
        {
            checkCommand.CommandText = $"PRAGMA table_info({tableName});";
            using var reader = checkCommand.ExecuteReader();

            while (reader.Read())
            {
                var existingColumnName = reader.GetString(reader.GetOrdinal("name"));
                if (string.Equals(existingColumnName, columnName, StringComparison.OrdinalIgnoreCase))
                {
                    // 이미 컬럼이 있음 — 마이그레이션을 다시 실행할 필요 없음.
                    return;
                }
            }
        }

        using var alterCommand = connection.CreateCommand();
        alterCommand.CommandText = $"ALTER TABLE {tableName} ADD COLUMN {columnName} {columnType};";
        alterCommand.ExecuteNonQuery();
    }

    private static void CreateFacilityTable(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS Facility (
                facility_id   TEXT PRIMARY KEY,
                facility_name TEXT NOT NULL,
                country       TEXT NOT NULL,
                district      TEXT NOT NULL,
                facility_type TEXT NOT NULL,
                status        TEXT NOT NULL
            );
            """;
        command.ExecuteNonQuery();
    }

    private static void CreateProductMasterTable(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS Product_Master (
                product_id          TEXT PRIMARY KEY,
                barcode             TEXT,
                internal_barcode    TEXT,
                product_name        TEXT NOT NULL,
                generic_name        TEXT,
                strength            TEXT,
                unit                TEXT NOT NULL,
                manufacturer        TEXT,
                country_of_origin   TEXT,
                cost_price          REAL NOT NULL,
                selling_price       REAL NOT NULL,
                safety_stock_level  INTEGER NOT NULL,
                status              TEXT NOT NULL,
                created_at          INTEGER NOT NULL
            );
            CREATE UNIQUE INDEX IF NOT EXISTS idx_product_barcode
                ON Product_Master(barcode) WHERE barcode IS NOT NULL;
            CREATE UNIQUE INDEX IF NOT EXISTS idx_product_internal_barcode
                ON Product_Master(internal_barcode) WHERE internal_barcode IS NOT NULL;
            CREATE INDEX IF NOT EXISTS idx_product_name
                ON Product_Master(product_name);
            CREATE INDEX IF NOT EXISTS idx_product_generic_name
                ON Product_Master(generic_name);
            """;
        command.ExecuteNonQuery();
    }

    private static void CreateInternalBarcodeSequenceTable(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS Internal_Barcode_Sequence (
                id          INTEGER PRIMARY KEY CHECK (id = 1),
                last_number INTEGER NOT NULL
            );
            INSERT OR IGNORE INTO Internal_Barcode_Sequence (id, last_number)
            VALUES (1, 0);
            """;
        command.ExecuteNonQuery();
    }

    private static void CreateInventoryTable(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS Inventory (
                inventory_id     TEXT PRIMARY KEY,
                facility_id      TEXT NOT NULL,
                product_id       TEXT NOT NULL,
                batch_number     TEXT NOT NULL,
                expiry_date      INTEGER NOT NULL,
                current_quantity INTEGER NOT NULL,
                updated_at       INTEGER NOT NULL,
                FOREIGN KEY (facility_id) REFERENCES Facility(facility_id),
                FOREIGN KEY (product_id) REFERENCES Product_Master(product_id),
                UNIQUE (facility_id, product_id, batch_number)
            );
            """;
        command.ExecuteNonQuery();
    }

    private static void CreateStockTransactionTable(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS Stock_Transaction (
                transaction_id                TEXT PRIMARY KEY,
                facility_id                   TEXT NOT NULL,
                product_id                    TEXT NOT NULL,
                user_id                       TEXT NOT NULL,
                transaction_type              TEXT NOT NULL,
                batch_number                  TEXT NOT NULL,
                expiry_date                   INTEGER NOT NULL,
                quantity                      INTEGER NOT NULL,
                selling_price_at_transaction   REAL,
                payment_method                TEXT,
                total_amount                  REAL,
                reason                        TEXT,
                transaction_time              INTEGER NOT NULL,
                FOREIGN KEY (facility_id) REFERENCES Facility(facility_id),
                FOREIGN KEY (product_id) REFERENCES Product_Master(product_id),
                FOREIGN KEY (user_id) REFERENCES Users(user_id)
            );
            CREATE INDEX IF NOT EXISTS idx_stock_transaction_product
                ON Stock_Transaction(product_id);
            CREATE INDEX IF NOT EXISTS idx_stock_transaction_type
                ON Stock_Transaction(transaction_type);
            """;
        command.ExecuteNonQuery();
    }
}