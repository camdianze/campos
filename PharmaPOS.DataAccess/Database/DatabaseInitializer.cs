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
        CreateImportHistoryTable(connection);
        CreateReceiptCounterTable(connection);
        CreateReceiptNumberTable(connection);

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

        // 박스/낱개 혼합 재고용.
        // 기본값 1은 "박스/낱개 구분이 없는 상품"이라, 기존 상품은 손대지 않아도 종전대로 동작한다.
        AddColumnIfMissing(connection, "Product_Master", "units_per_box", "INTEGER NOT NULL DEFAULT 1");

        // 가격 기준은 박스가 기본이고, 헐어 파는 낱개가만 따로 받는다.
        AddColumnIfMissing(connection, "Product_Master", "unit_selling_price", "REAL");

        // 약국이 직접 정하는 상품 분류. 선택 입력이라 NULL을 그대로 둔다 —
        // 기본값을 넣어 두면 "아직 분류 안 함"과 "그 분류로 정함"을 구분할 수 없다.
        AddColumnIfMissing(connection, "Product_Master", "category", "TEXT");

        // 제형(정제·시럽·연고…). 판매 단위(unit)와 다른 값이다 — unit은 "낱개를 세는 이름"이고
        // 이건 "약의 형태"다. category와 같이 선택 입력이라 NULL을 그대로 둔다.
        AddColumnIfMissing(connection, "Product_Master", "dosage_form", "TEXT");

        // 개발 중 잠깐 있었던 반대 방향 컬럼(박스가를 따로 받던 것)을 치운다.
        // 배포된 DB에는 없던 컬럼이라 지워도 잃을 데이터가 없다.
        DropColumnIfPresent(connection, "Product_Master", "box_selling_price");

        AddColumnIfMissing(connection, "Inventory", "box_quantity", "INTEGER NOT NULL DEFAULT 0");

        // unit_quantity만 추가 여부를 따로 받는 이유: 이 컬럼이 방금 생긴 DB는
        // 기존 재고가 전부 (0박스, 0낱개)로 들어가 있어 총량과 어긋난다.
        // 기존 상품은 units_per_box가 1이므로 전량을 낱개 쪽에 채워 주면 맞는다.
        var unitQuantityWasAdded =
            AddColumnIfMissing(connection, "Inventory", "unit_quantity", "INTEGER NOT NULL DEFAULT 0");

        if (unitQuantityWasAdded)
        {
            BackfillInventoryUnitQuantity(connection);
        }

        // 환불용. 환불 행이 어느 판매 줄을 되돌린 것인지 가리킨다.
        // 인덱스를 여기서 만드는 이유: CreateStockTransactionTable은 컬럼이 아직 없는
        // 기존 DB에서도 돌기 때문에, 컬럼을 추가한 뒤여야 인덱스를 걸 수 있다.
        // 영수증 설정용. 값의 종류(text/enum/bool/number)와 누가 마지막으로 바꿨는지를 남긴다.
        // 둘 다 NULL을 허용한다 — 이 컬럼들이 생기기 전에 저장된 복약안내 설정 값들이
        // 이미 들어 있고, 그 값들에는 채워 넣을 출처가 없다.
        AddColumnIfMissing(connection, "App_Setting", "value_type", "TEXT");
        AddColumnIfMissing(connection, "App_Setting", "updated_by", "TEXT");

        AddColumnIfMissing(connection, "Stock_Transaction", "related_transaction_id", "TEXT");

        using var indexCommand = connection.CreateCommand();
        indexCommand.CommandText = """
            CREATE INDEX IF NOT EXISTS idx_stock_transaction_related
                ON Stock_Transaction(related_transaction_id);
            """;
        indexCommand.ExecuteNonQuery();
    }

    /// <summary>
    /// 박스/낱개 컬럼이 막 추가된 기존 DB의 재고를 낱개 쪽으로 몰아 준다.
    /// 이 시점의 상품은 전부 units_per_box = 1이라 박스 수는 0이 맞다.
    /// </summary>
    private static void BackfillInventoryUnitQuantity(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE Inventory
            SET unit_quantity = current_quantity
            WHERE box_quantity = 0 AND unit_quantity = 0;
            """;
        command.ExecuteNonQuery();
    }

    /// <summary>
    /// 컬럼이 남아 있으면 지운다. 개발 중에만 존재했던 컬럼을 치우는 용도다.
    /// DROP COLUMN은 SQLite 3.35부터라 실패할 수 있는데, 실패해도 쓰지 않는 컬럼이
    /// 남을 뿐 동작에는 영향이 없으므로 조용히 넘어간다.
    /// </summary>
    private static void DropColumnIfPresent(SqliteConnection connection, string tableName, string columnName)
    {
        if (!ColumnExists(connection, tableName, columnName))
        {
            return;
        }

        try
        {
            using var dropCommand = connection.CreateCommand();
            dropCommand.CommandText = $"ALTER TABLE {tableName} DROP COLUMN {columnName};";
            dropCommand.ExecuteNonQuery();
        }
        catch (SqliteException)
        {
            // 지우지 못해도 그대로 둔다.
        }
    }

    private static bool ColumnExists(SqliteConnection connection, string tableName, string columnName)
    {
        using var checkCommand = connection.CreateCommand();
        checkCommand.CommandText = $"PRAGMA table_info({tableName});";
        using var reader = checkCommand.ExecuteReader();

        while (reader.Read())
        {
            var existingColumnName = reader.GetString(reader.GetOrdinal("name"));
            if (string.Equals(existingColumnName, columnName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <returns>컬럼을 실제로 추가했으면 true, 이미 있었으면 false.</returns>
    private static bool AddColumnIfMissing(SqliteConnection connection, string tableName, string columnName, string columnType)
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
                    return false;
                }
            }
        }

        using var alterCommand = connection.CreateCommand();
        alterCommand.CommandText = $"ALTER TABLE {tableName} ADD COLUMN {columnName} {columnType};";
        alterCommand.ExecuteNonQuery();
        return true;
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
                is_combination      INTEGER NOT NULL DEFAULT 0,
                units_per_box       INTEGER NOT NULL DEFAULT 1,
                unit_selling_price  REAL,
                category            TEXT,
                dosage_form         TEXT
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
                value_type    TEXT,
                updated_at    INTEGER NOT NULL,
                updated_by    TEXT
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
                box_quantity     INTEGER NOT NULL DEFAULT 0,
                unit_quantity    INTEGER NOT NULL DEFAULT 0,
                updated_at       INTEGER NOT NULL,
                FOREIGN KEY (facility_id) REFERENCES Facility(facility_id),
                FOREIGN KEY (product_id) REFERENCES Product_Master(product_id),
                UNIQUE (facility_id, product_id, batch_number)
            );
            """;
        command.ExecuteNonQuery();
    }

    /// <summary>
    /// 초기 재고 임포트 이력. 같은 파일을 두 번 넣어 재고가 두 배가 되는 사고를 막는다.
    /// (import_type, file_hash)에 UNIQUE를 거는 이유: 한 파일로 상품을 넣은 뒤
    /// 같은 파일로 재고를 넣는 것이 정상 순서라, 해시만으로 막으면 2단계가 통째로 막힌다.
    /// </summary>
    private static void CreateImportHistoryTable(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS Import_History (
                import_id     TEXT PRIMARY KEY,
                facility_id   TEXT NOT NULL,
                import_type   TEXT NOT NULL,
                file_hash     TEXT NOT NULL,
                file_name     TEXT,
                row_count     INTEGER NOT NULL,
                success_count INTEGER NOT NULL,
                failure_count INTEGER NOT NULL,
                imported_at   INTEGER NOT NULL,
                UNIQUE (import_type, file_hash)
            );
            CREATE INDEX IF NOT EXISTS idx_import_history_hash
                ON Import_History(file_hash);
            """;
        command.ExecuteNonQuery();
    }

    /// <summary>
    /// 영수증 일련번호 카운터.
    ///
    /// counter_key가 곧 "언제 0001로 되돌리는가"를 담는다 — 일별이면 접두어+날짜,
    /// 월별이면 접두어+연월, 초기화 없음이면 접두어 하나다.
    /// 주기를 바꾸면 키가 달라져 새 카운터에서 다시 시작하며, 예전 카운터 값은
    /// 그대로 남는다. 주기를 되돌렸을 때 번호가 뒤로 가지 않게 하기 위해서다.
    /// </summary>
    private static void CreateReceiptCounterTable(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS Receipt_Counter (
                counter_key TEXT PRIMARY KEY,
                last_number INTEGER NOT NULL
            );
            """;
        command.ExecuteNonQuery();
    }

    /// <summary>
    /// 판매에 붙은 영수증 번호.
    ///
    /// 판매 헤더 테이블이 없으므로 sale_key는 "{transaction_time}|{user_id}" —
    /// 판매 내역과 환불이 한 판매를 찾을 때 쓰는 것과 같은 짝이다.
    /// 이 표가 있어야 판매 내역에서 재출력한 영수증이 처음과 같은 번호를 단다.
    /// </summary>
    private static void CreateReceiptNumberTable(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS Receipt_Number (
                sale_key   TEXT PRIMARY KEY,
                receipt_no TEXT NOT NULL,
                issued_at  INTEGER NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_receipt_number_no
                ON Receipt_Number(receipt_no);
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
                related_transaction_id        TEXT,
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