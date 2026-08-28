using Microsoft.Data.Sqlite;
using PharmaPOS.Application.Repositories;
using PharmaPOS.DataAccess.Database;
using PharmaPOS.Domain.Entities;
using PharmaPOS.Domain.Enums;

namespace PharmaPOS.DataAccess.Repositories;

/// <summary>
/// IProductRepository의 SQLite 구현체.
/// </summary>
public class ProductRepository : IProductRepository
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public ProductRepository(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    /// <summary>
    /// LIKE에서 뜻을 가지는 글자(% _ \)를 글자 그대로 찾도록 앞에 \를 붙인다.
    /// 상품명에 %가 든 경우(예: "Dextrose 50%")를 검색할 수 있어야 한다.
    /// </summary>
    private static string EscapeLikePattern(string value) =>
        value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");

    public async Task<IReadOnlyList<Product>> SearchAsync(string searchTerm, EntityStatus? statusFilter)
    {
        using var connection = _connectionFactory.CreateOpenConnection();

        using var command = connection.CreateCommand();

        var whereClauses = new List<string>();

        // 검색어가 없으면 정렬은 이름순 하나뿐이다(목록 화면의 기본 상태).
        var orderBySql = "product_name";

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            // 비교는 양쪽을 소문자로 맞춰서 한다. 파라미터는 여기서 미리 소문자로 만들어
            // 넘기므로 SQL 쪽은 컬럼만 LOWER() 하면 된다.
            //
            // LIKE의 와일드카드(% _)를 글자 그대로 찾도록 이스케이프한다.
            // "50%"를 치면 %가 "무엇이든"으로 읽혀 엉뚱한 상품이 전부 걸리던 문제를 막는다.
            var term = EscapeLikePattern(searchTerm.Trim().ToLowerInvariant());

            whereClauses.Add("""
                (LOWER(product_name) LIKE $contains ESCAPE '\'
                 OR LOWER(generic_name) LIKE $contains ESCAPE '\'
                 OR LOWER(barcode) LIKE $contains ESCAPE '\'
                 OR LOWER(internal_barcode) LIKE $contains ESCAPE '\')
                """);

            command.Parameters.AddWithValue("$exact", term);
            command.Parameters.AddWithValue("$prefix", $"{term}%");
            command.Parameters.AddWithValue("$wordStart", $"% {term}%");
            command.Parameters.AddWithValue("$contains", $"%{term}%");

            // 글자가 얼마나 정확히 맞는지로 줄을 세운다. 이름순으로만 정렬하면
            // "nano"를 쳤을 때 이름에 nano가 들어 있기만 한 A로 시작하는 상품이
            // 정작 "Nano…"라는 상품보다 앞에 온다 — 계산대에서 매번 눈으로 찾아야 한다.
            orderBySql = """
                CASE
                    WHEN LOWER(product_name) = $exact THEN 0
                    WHEN LOWER(barcode) = $exact OR LOWER(internal_barcode) = $exact THEN 1
                    WHEN LOWER(product_name) LIKE $prefix ESCAPE '\' THEN 2
                    WHEN LOWER(product_name) LIKE $wordStart ESCAPE '\' THEN 3
                    WHEN LOWER(generic_name) = $exact THEN 4
                    WHEN LOWER(generic_name) LIKE $prefix ESCAPE '\' THEN 5
                    WHEN LOWER(product_name) LIKE $contains ESCAPE '\' THEN 6
                    WHEN LOWER(generic_name) LIKE $contains ESCAPE '\' THEN 7
                    ELSE 8
                END,
                LENGTH(product_name),
                product_name
                """;
        }

        if (statusFilter is not null)
        {
            whereClauses.Add("status = $status");
            command.Parameters.AddWithValue("$status", statusFilter.Value.ToString());
        }

        var whereSql = whereClauses.Count > 0
            ? "WHERE " + string.Join(" AND ", whereClauses)
            : "";

        command.CommandText = $"""
            SELECT product_id, barcode, internal_barcode, product_name, generic_name,
                   strength, unit, manufacturer, country_of_origin, cost_price,
                   selling_price, safety_stock_level, status, created_at,
                   atc_code, is_combination, units_per_box, unit_selling_price, category,
                   dosage_form
            FROM Product_Master
            {whereSql}
            ORDER BY {orderBySql};
            """;

        using var reader = await command.ExecuteReaderAsync();

        var results = new List<Product>();
        while (await reader.ReadAsync())
        {
            results.Add(MapToProduct(reader));
        }

        return results;
    }

    public async Task<Product?> GetByIdAsync(string productId)
    {
        using var connection = _connectionFactory.CreateOpenConnection();

        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT product_id, barcode, internal_barcode, product_name, generic_name,
                   strength, unit, manufacturer, country_of_origin, cost_price,
                   selling_price, safety_stock_level, status, created_at,
                   atc_code, is_combination, units_per_box, unit_selling_price, category,
                   dosage_form
            FROM Product_Master
            WHERE product_id = $productId;
            """;
        command.Parameters.AddWithValue("$productId", productId);

        using var reader = await command.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
        {
            return null;
        }

        return MapToProduct(reader);
    }

    public async Task<bool> BarcodeExistsAsync(string barcode, string? excludeProductId = null)
    {
        using var connection = _connectionFactory.CreateOpenConnection();

        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(*) FROM Product_Master
            WHERE barcode = $barcode
              AND ($excludeProductId IS NULL OR product_id != $excludeProductId);
            """;
        command.Parameters.AddWithValue("$barcode", barcode);
        command.Parameters.AddWithValue("$excludeProductId", (object?)excludeProductId ?? DBNull.Value);

        var count = (long)(await command.ExecuteScalarAsync())!;
        return count > 0;
    }

    public async Task<bool> InternalBarcodeExistsAsync(string internalBarcode, string? excludeProductId = null)
    {
        using var connection = _connectionFactory.CreateOpenConnection();

        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(*) FROM Product_Master
            WHERE internal_barcode = $internalBarcode
              AND ($excludeProductId IS NULL OR product_id != $excludeProductId);
            """;
        command.Parameters.AddWithValue("$internalBarcode", internalBarcode);
        command.Parameters.AddWithValue("$excludeProductId", (object?)excludeProductId ?? DBNull.Value);

        var count = (long)(await command.ExecuteScalarAsync())!;
        return count > 0;
    }

    public async Task InsertAsync(Product product)
    {
        using var connection = _connectionFactory.CreateOpenConnection();

        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Product_Master
                (product_id, barcode, internal_barcode, product_name, generic_name,
                 strength, unit, manufacturer, country_of_origin, cost_price,
                 selling_price, safety_stock_level, status, created_at,
                 atc_code, is_combination, units_per_box, unit_selling_price, category,
                 dosage_form)
            VALUES
                ($productId, $barcode, $internalBarcode, $productName, $genericName,
                 $strength, $unit, $manufacturer, $countryOfOrigin, $costPrice,
                 $sellingPrice, $safetyStockLevel, $status, $createdAt,
                 $atcCode, $isCombination, $unitsPerBox, $unitSellingPrice, $category,
                 $dosageForm);
            """;
        AddProductParameters(command, product);
        await command.ExecuteNonQueryAsync();
    }

    public async Task UpdateAsync(Product product)
    {
        using var connection = _connectionFactory.CreateOpenConnection();

        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE Product_Master
            SET barcode = $barcode,
                internal_barcode = $internalBarcode,
                product_name = $productName,
                generic_name = $genericName,
                strength = $strength,
                unit = $unit,
                manufacturer = $manufacturer,
                country_of_origin = $countryOfOrigin,
                cost_price = $costPrice,
                selling_price = $sellingPrice,
                safety_stock_level = $safetyStockLevel,
                status = $status,
                atc_code = $atcCode,
                is_combination = $isCombination,
                units_per_box = $unitsPerBox,
                unit_selling_price = $unitSellingPrice,
                category = $category,
                dosage_form = $dosageForm
            WHERE product_id = $productId;
            """;
        AddProductParameters(command, product);
        await command.ExecuteNonQueryAsync();
    }

    public async Task DeactivateAsync(string productId)
    {
        using var connection = _connectionFactory.CreateOpenConnection();

        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE Product_Master
            SET status = $status
            WHERE product_id = $productId;
            """;
        command.Parameters.AddWithValue("$status", EntityStatus.Inactive.ToString());
        command.Parameters.AddWithValue("$productId", productId);
        await command.ExecuteNonQueryAsync();
    }

    private static void AddProductParameters(SqliteCommand command, Product product)
    {
        command.Parameters.AddWithValue("$productId", product.ProductId);
        command.Parameters.AddWithValue("$barcode", (object?)product.Barcode ?? DBNull.Value);
        command.Parameters.AddWithValue("$internalBarcode", (object?)product.InternalBarcode ?? DBNull.Value);
        command.Parameters.AddWithValue("$productName", product.ProductName);
        command.Parameters.AddWithValue("$genericName", (object?)product.GenericName ?? DBNull.Value);
        command.Parameters.AddWithValue("$strength", (object?)product.Strength ?? DBNull.Value);
        command.Parameters.AddWithValue("$unit", product.Unit);
        command.Parameters.AddWithValue("$manufacturer", (object?)product.Manufacturer ?? DBNull.Value);
        command.Parameters.AddWithValue("$countryOfOrigin", (object?)product.CountryOfOrigin ?? DBNull.Value);
        command.Parameters.AddWithValue("$costPrice", product.CostPrice);
        command.Parameters.AddWithValue("$sellingPrice", product.SellingPrice);
        command.Parameters.AddWithValue("$safetyStockLevel", product.SafetyStockLevel);
        command.Parameters.AddWithValue("$status", product.Status.ToString());
        command.Parameters.AddWithValue("$createdAt", product.CreatedAt);
        command.Parameters.AddWithValue("$atcCode", (object?)product.AtcCode ?? DBNull.Value);
        command.Parameters.AddWithValue("$isCombination", product.IsCombination ? 1 : 0);
        command.Parameters.AddWithValue("$unitsPerBox", product.UnitsPerBox);
        command.Parameters.AddWithValue("$unitSellingPrice", (object?)product.UnitSellingPrice ?? DBNull.Value);
        command.Parameters.AddWithValue("$category", (object?)product.Category?.ToString() ?? DBNull.Value);
        command.Parameters.AddWithValue("$dosageForm", (object?)product.DosageForm?.ToString() ?? DBNull.Value);
    }

    private static Product MapToProduct(SqliteDataReader reader)
    {
        return new Product
        {
            ProductId = reader.GetString(0),
            Barcode = reader.IsDBNull(1) ? null : reader.GetString(1),
            InternalBarcode = reader.IsDBNull(2) ? null : reader.GetString(2),
            ProductName = reader.GetString(3),
            GenericName = reader.IsDBNull(4) ? null : reader.GetString(4),
            Strength = reader.IsDBNull(5) ? null : reader.GetString(5),
            Unit = reader.GetString(6),
            Manufacturer = reader.IsDBNull(7) ? null : reader.GetString(7),
            CountryOfOrigin = reader.IsDBNull(8) ? null : reader.GetString(8),
            CostPrice = (decimal)reader.GetDouble(9),
            SellingPrice = (decimal)reader.GetDouble(10),
            SafetyStockLevel = reader.GetInt32(11),
            Status = Enum.Parse<EntityStatus>(reader.GetString(12)),
            CreatedAt = reader.GetInt64(13),
            AtcCode = reader.IsDBNull(14) ? null : reader.GetString(14),
            IsCombination = reader.GetInt32(15) != 0,
            UnitsPerBox = reader.GetInt32(16),
            UnitSellingPrice = reader.IsDBNull(17) ? null : (decimal)reader.GetDouble(17),
            // 알 수 없는 값(자유 입력이던 시절의 잔재 등)은 "정하지 않음"으로 읽는다.
            // 여기서 예외를 던지면 상품 목록 전체가 열리지 않는다.
            Category = reader.IsDBNull(18) || !Enum.TryParse<ProductCategory>(reader.GetString(18), out var category)
                ? null
                : category,
            // 제형도 같은 이유로 관대하게 읽는다. 목록에 없는 값이 들어와 있어도
            // "정하지 않음"으로 넘어가야 상품 목록이 열린다.
            DosageForm = reader.IsDBNull(19) || !Enum.TryParse<DosageForm>(reader.GetString(19), out var dosageForm)
                ? null
                : dosageForm
        };
    }

    /// <summary>
    /// 사진만 따로 읽는다. SearchAsync/GetByIdAsync의 SELECT 목록에 photo를 넣지 않는 이유는
    /// 상품 목록이 수백 줄을 한 번에 읽기 때문이다 — 거기에 이미지가 딸려 오면 검색이 느려진다.
    /// </summary>
    public async Task<ProductPhoto?> GetPhotoAsync(string productId)
    {
        using var connection = _connectionFactory.CreateOpenConnection();

        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT photo, photo_updated_at
            FROM Product_Master
            WHERE product_id = $productId;
            """;
        command.Parameters.AddWithValue("$productId", productId);

        using var reader = await command.ExecuteReaderAsync();

        if (!await reader.ReadAsync() || reader.IsDBNull(0))
        {
            return null;
        }

        var bytes = (byte[])reader.GetValue(0);

        // 사진은 있는데 시각이 비어 있는 경우(수동으로 넣은 DB 등)는 0으로 읽는다.
        // 화면에서 "언제 바꿨는지 모름"으로 다룬다.
        var updatedAt = reader.IsDBNull(1) ? 0L : reader.GetInt64(1);

        return new ProductPhoto(bytes, updatedAt);
    }

    public async Task SavePhotoAsync(string productId, byte[]? photo, long? updatedAt)
    {
        using var connection = _connectionFactory.CreateOpenConnection();

        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE Product_Master
            SET photo = $photo,
                photo_updated_at = $updatedAt
            WHERE product_id = $productId;
            """;
        command.Parameters.AddWithValue("$productId", productId);
        command.Parameters.AddWithValue("$photo", (object?)photo ?? DBNull.Value);
        command.Parameters.AddWithValue("$updatedAt", (object?)updatedAt ?? DBNull.Value);

        await command.ExecuteNonQueryAsync();
    }
}