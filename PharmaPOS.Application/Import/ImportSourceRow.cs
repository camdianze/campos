namespace PharmaPOS.Application.Import;

/// <summary>
/// 파일에서 읽어낸 한 행. 파일 형식(CSV/Excel)은 여기까지만 관여하고,
/// 그 뒤의 판정은 전부 이 타입 위에서 이뤄진다.
///
/// 값은 정규화된 헤더 이름으로 찾는다 (product_name / ProductName / "Product Name" → productname).
/// LineNumber는 파일에서 사람이 보는 행 번호다 — 오류 목록에 그대로 찍어야 하므로
/// 헤더를 1행으로 세고 첫 데이터 행이 2가 된다.
/// </summary>
public sealed class ImportSourceRow
{
    public required int LineNumber { get; init; }

    public required IReadOnlyDictionary<string, string> Values { get; init; }

    /// <summary>없는 컬럼은 빈 문자열. 파일마다 컬럼이 빠져 있을 수 있어 예외를 던지지 않는다.</summary>
    public string Get(string normalizedColumn) =>
        Values.TryGetValue(normalizedColumn, out var value) ? value.Trim() : string.Empty;

    /// <summary>
    /// 같은 뜻의 헤더 이름 여러 개 중 값이 있는 첫 번째를 돌려준다
    /// (safety_stock / safety_stock_level 처럼 파일마다 이름이 다른 컬럼용).
    /// </summary>
    public string Get(IReadOnlyList<string> aliases)
    {
        foreach (var alias in aliases)
        {
            var value = Get(alias);

            if (value.Length > 0)
            {
                return value;
            }
        }

        return string.Empty;
    }

    /// <summary>상품 정보 컬럼이 전부 비어 있는 행인지. 2행부터는 배치만 적힐 수 있다.</summary>
    public bool IsEmpty => Values.Values.All(string.IsNullOrWhiteSpace);
}
