namespace PharmaPOS.Application.Import;

/// <summary>
/// 임포트 파일의 컬럼 정의.
///
/// 한 파일에 상품 정보와 배치 정보가 함께 들어 있고, 같은 상품에 배치가 여러 개면
/// 행이 여러 개가 된다. 2행부터는 product_name만 있고 상품 정보 컬럼이 비어 있어도 된다.
///
/// 컬럼마다 이름을 여럿 받는 이유: 내보내기가 만드는 파일의 헤더는 DB 컬럼명
/// (safety_stock_level, unit_selling_price)이고, 손으로 적는 서식은 짧은 이름
/// (safety_stock, loose_unit_price)이 편하다. 둘 다 받아야 내보낸 파일을 고쳐서
/// 그대로 다시 넣을 수 있다.
/// </summary>
public static class InitialImportColumns
{
    // 아래 값은 전부 "정규화된" 이름이다. NormalizeHeader를 거친 결과와 비교한다.
    // 배열의 첫 번째가 대표 이름이다.

    public static readonly string[] ProductName = ["productname"];
    public static readonly string[] Unit = ["unit"];
    public static readonly string[] Barcode = ["barcode"];
    public static readonly string[] CostPrice = ["costprice"];
    public static readonly string[] SellingPrice = ["sellingprice"];
    public static readonly string[] SafetyStock = ["safetystock", "safetystocklevel"];
    public static readonly string[] UnitsPerBox = ["unitsperbox"];
    public static readonly string[] LooseUnitPrice = ["looseunitprice", "unitsellingprice"];

    // 아래는 선택 컬럼. 없으면 신규 상품에서는 비워 두고, 기존 상품에서는 건드리지 않는다.
    public static readonly string[] GenericName = ["genericname"];
    public static readonly string[] Strength = ["strength"];

    /// <summary>제형. 손으로 적는 서식에서는 form 한 단어로 쓰는 일이 많아 함께 받는다.</summary>
    public static readonly string[] DosageForm = ["dosageform", "form"];

    public static readonly string[] AtcCode = ["atccode"];
    public static readonly string[] IsCombination = ["iscombination"];
    public static readonly string[] Manufacturer = ["manufacturer"];
    public static readonly string[] CountryOfOrigin = ["countryoforigin"];
    public static readonly string[] Status = ["status"];

    public static readonly string[] BatchNumber = ["batchnumber"];
    public static readonly string[] ExpiryDate = ["expirydate"];
    public static readonly string[] Quantity = ["quantity"];

    /// <summary>expiry_date에 이 값이 들어오면 "유효기간 모름"으로 본다.</summary>
    public const string NoExpiryMarker = "N";

    /// <summary>파일에 적는 그대로의 헤더. 서식 안내와 내보내기가 함께 쓴다.</summary>
    public static readonly IReadOnlyList<string> ProductHeaderLine =
    [
        "product_name", "unit", "barcode", "cost_price", "selling_price", "safety_stock",
        "units_per_box", "loose_unit_price", "batch_number", "expiry_date", "quantity"
    ];

    /// <summary>
    /// 파일에 반드시 있어야 하는 컬럼.
    ///
    /// 상품명 하나뿐인 이유: 이 임포트는 새 상품을 만들기도 하고 이미 있는 상품의
    /// 값을 고치기도 한다. 안전재고만 적은 파일로 안전재고만 고치는 것도 정상 사용이라,
    /// 단가·단위까지 요구하면 그런 파일이 통째로 막힌다.
    /// 신규 상품에 필요한 값은 행 단위로 검사한다.
    /// </summary>
    public static readonly IReadOnlyList<string[]> RequiredForProducts = [ProductName];

    /// <summary>Inventory 임포트에 반드시 있어야 하는 컬럼.</summary>
    public static readonly IReadOnlyList<string[]> RequiredForInventory = [ProductName, Quantity];

    /// <summary>
    /// 헤더 이름에서 대소문자와 구분자를 없앤다.
    /// "ProductName" / "product_name" / "Product Name"은 같은 컬럼을 가리킨다.
    /// 앞에 붙는 BOM도 여기서 같이 털어낸다.
    /// </summary>
    public static string NormalizeHeader(string header) =>
        new(header.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

    /// <summary>
    /// 빠진 필수 컬럼을 알려준다. 없으면 null.
    /// 실제로 읽힌 헤더를 함께 붙이는 이유: 이름이 어긋난 경우 그 목록만 보면 원인을 알 수 있다.
    /// </summary>
    public static string? DescribeMissingColumns(
        IReadOnlyCollection<string> normalizedHeaders, IReadOnlyList<string[]> required)
    {
        var missing = required
            .Where(aliases => !aliases.Any(normalizedHeaders.Contains))
            .Select(DisplayName)
            .ToList();

        if (missing.Count == 0)
        {
            return null;
        }

        return $"The file is missing required columns: {string.Join(", ", missing)}. "
             + $"Columns found: {string.Join(", ", normalizedHeaders.Where(h => h.Length > 0))}.";
    }

    /// <summary>
    /// 오류 문구에 쓸 이름. 정규화된 이름(productname)이 아니라 파일에 적는 이름(product_name)을
    /// 보여줘야 사용자가 파일에서 그 칸을 찾을 수 있다.
    /// </summary>
    public static string DisplayName(string[] aliases) => aliases[0] switch
    {
        "productname" => "product_name",
        "costprice" => "cost_price",
        "sellingprice" => "selling_price",
        "safetystock" => "safety_stock",
        "unitsperbox" => "units_per_box",
        "looseunitprice" => "loose_unit_price",
        "batchnumber" => "batch_number",
        "expirydate" => "expiry_date",
        "genericname" => "generic_name",
        "dosageform" => "dosage_form",
        "atccode" => "atc_code",
        "iscombination" => "is_combination",
        "countryoforigin" => "country_of_origin",
        var other => other
    };

    /// <summary>상품명 매칭 규칙: 앞뒤 공백 제거 + 대소문자 무시.</summary>
    public static string NormalizeProductName(string productName) => productName.Trim();

    /// <summary>상품명 비교에 쓰는 비교자. 사전과 검색 양쪽이 같은 규칙을 써야 한다.</summary>
    public static StringComparer ProductNameComparer => StringComparer.OrdinalIgnoreCase;
}
