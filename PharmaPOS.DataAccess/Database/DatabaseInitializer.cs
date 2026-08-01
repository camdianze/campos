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
        CreateAppSettingTable(connection);
        CreateAwareClassificationTable(connection);
        CreateCounsellingLogTable(connection);

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

        // AMR 복약안내 기능용. 상품이 어떤 항생제인지 판별하는 데 쓴다.
        // generic_name은 처음부터 있던 컬럼이라 여기 없다.
        AddColumnIfMissing(connection, "Product_Master", "atc_code", "TEXT");
        AddColumnIfMissing(connection, "Product_Master", "is_combination", "INTEGER NOT NULL DEFAULT 0");
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
                created_at          INTEGER NOT NULL,
                atc_code            TEXT,
                is_combination      INTEGER NOT NULL DEFAULT 0
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

    /// <summary>
    /// 앱 전역 설정 보관용 키-값 테이블.
    /// 이 제품은 단일 시설 배포이므로 시설별로 나누지 않고 전역 키 하나로 관리한다.
    /// </summary>
    private static void CreateAppSettingTable(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS App_Setting (
                setting_key   TEXT PRIMARY KEY,
                setting_value TEXT NOT NULL,
                updated_at    INTEGER NOT NULL
            );
            """;
        command.ExecuteNonQuery();
    }

    /// <summary>
    /// WHO AWaRe 분류 참조 테이블. 시드 파일에서 적재되며, 앱이 직접 수정하지 않는다.
    ///
    /// atc_code를 PK로 두지 않은 이유: 고유 ATC 코드가 없는 고정용량복합제(FDC)가
    /// 존재하고 그것들이 바로 NOT_RECOMMENDED 그룹이라, PK로 삼으면 정작
    /// 안내가 가장 필요한 행들을 적재할 수 없다. UNIQUE 제약도 걸지 않는다 —
    /// 시드 파일에 중복이 있으면 적재를 실패시키는 대신 로더가 건수로 보고한다.
    /// </summary>
    private static void CreateAwareClassificationTable(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS Aware_Classification (
                aware_id        TEXT PRIMARY KEY,
                atc_code        TEXT,
                antibiotic_name TEXT NOT NULL,
                normalized_name TEXT NOT NULL,
                aware_group     TEXT NOT NULL,
                is_systemic     INTEGER NOT NULL,
                source_version  TEXT NOT NULL,
                updated_at      INTEGER NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_aware_atc_code
                ON Aware_Classification(atc_code);
            CREATE INDEX IF NOT EXISTS idx_aware_normalized_name
                ON Aware_Classification(normalized_name);
            """;
        command.ExecuteNonQuery();
    }

    /// <summary>
    /// 복약안내 출력 이력. 스튜어드십 지표(ACCESS 비중, 출력률, unmatched 건수) 집계용이다.
    /// 환자 식별 정보는 어떤 형태로도 저장하지 않는다.
    /// </summary>
    private static void CreateCounsellingLogTable(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS Counselling_Log (
                log_id         TEXT PRIMARY KEY,
                transaction_id TEXT NOT NULL,
                product_id     TEXT NOT NULL,
                atc_code       TEXT,
                aware_group    TEXT NOT NULL,
                printed        INTEGER NOT NULL,
                skip_reason    TEXT,
                locale         TEXT NOT NULL,
                source_version TEXT,
                created_at     INTEGER NOT NULL,
                FOREIGN KEY (transaction_id) REFERENCES Stock_Transaction(transaction_id),
                FOREIGN KEY (product_id) REFERENCES Product_Master(product_id)
            );
            CREATE INDEX IF NOT EXISTS idx_counselling_log_created_at
                ON Counselling_Log(created_at);
            CREATE INDEX IF NOT EXISTS idx_counselling_log_aware_group
                ON Counselling_Log(aware_group);
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