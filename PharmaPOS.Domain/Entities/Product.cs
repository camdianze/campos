using PharmaPOS.Domain.Enums;

namespace PharmaPOS.Domain.Entities;

/// <summary>
/// PRD의 Product Master 테이블에 대응하는 엔티티.
/// F-03 Product Master Management에서 사용한다.
/// </summary>
public class Product
{
    public required string ProductId { get; set; }

    /// <summary>
    /// 제조사 바코드. 없을 수 있다 (nullable) — 이 경우 InternalBarcode를 사용한다.
    /// </summary>
    public string? Barcode { get; set; }

    /// <summary>
    /// 자동 생성되는 내부 바코드. 형식: INT-XXXXXXXX.
    /// 제조사 바코드가 없는 상품에 한해 생성된다.
    /// </summary>
    public string? InternalBarcode { get; set; }

    public required string ProductName { get; set; }

    public string? GenericName { get; set; }

    public string? Strength { get; set; }

    /// <summary>
    /// 제형(정제·시럽·연고…). 선택 입력이라 비어 있을 수 있다.
    /// 아래 Unit과 다른 값이다 — DosageForm 열거형의 주석을 참고.
    /// </summary>
    public DosageForm? DosageForm { get; set; }

    /// <summary>
    /// 낱개 하나를 세는 단위(Tablet, Bottle, Tube…). 제형이 아니다.
    /// 화면이 "Tablets Per Box"처럼 복수형으로 쓰므로 셀 수 있는 이름이어야 한다.
    /// </summary>
    public required string Unit { get; set; }

    public string? Manufacturer { get; set; }

    public string? CountryOfOrigin { get; set; }

    /// <summary>
    /// 매입 원가. 박스로 파는 상품(UnitsPerBox > 1)이면 <b>박스 하나</b>의 원가다 —
    /// 매입이 박스 단위로 이뤄지므로 송장에 적힌 값을 그대로 넣을 수 있게 했다.
    /// 낱개 원가가 필요한 곳은 UnitCostPrice로 나눠 쓴다.
    /// </summary>
    public required decimal CostPrice { get; set; }

    /// <summary>
    /// 판매가. 박스로 파는 상품이면 <b>박스 하나</b>의 판매가다.
    /// 낱개 판매가는 UnitSellingPrice에 따로 넣는다.
    /// </summary>
    public required decimal SellingPrice { get; set; }

    public required int SafetyStockLevel { get; set; }

    public required EntityStatus Status { get; set; }

    public required long CreatedAt { get; set; }

    /// <summary>
    /// WHO ATC 코드. 항생제 복약안내(AMR) 판별에 쓴다.
    /// 값이 있으면 성분명 매칭보다 우선한다 — 표기 흔들림이 없기 때문이다.
    /// 항생제가 아닌 상품은 비워 둔다.
    /// </summary>
    public string? AtcCode { get; set; }

    /// <summary>
    /// 복합제 여부. 성분은 여럿이어도 AWaRe 분류는 하나이므로,
    /// 조합 자체의 ATC 코드를 우선 사용한다는 점을 표시해 둔다.
    /// </summary>
    public bool IsCombination { get; set; }

    /// <summary>
    /// 박스 하나에 들어 있는 낱개 수. 기본값 1 = 박스/낱개 구분이 없는 상품.
    /// 1을 넘으면 박스째 팔거나 낱개로 헐어 팔 수 있는 상품이다.
    /// 재고 수량(current_quantity)은 이 값과 무관하게 항상 낱개 기준으로 센다.
    /// </summary>
    public int UnitsPerBox { get; set; } = 1;

    /// <summary>
    /// 헐어서 파는 낱개 하나의 판매가. 비워 두면 박스가 ÷ UnitsPerBox로 계산한다.
    /// 별도 컬럼을 둔 이유는 낱개로 사면 대개 더 비싸게 받기 때문이다 —
    /// 박스가에서 그냥 나눠서는 그 값을 표현할 수 없다.
    /// </summary>
    public decimal? UnitSellingPrice { get; set; }

    /// <summary>
    /// 의약품 / 비의약품 구분. 선택 입력이라 비어 있을 수 있고,
    /// 비어 있다고 해서 상품 등록이 막히지 않는다.
    /// </summary>
    public ProductCategory? Category { get; set; }

    /// <summary>
    /// 한 박스에 여러 개가 든 상품인지. <b>포장 단위</b>에 관한 것이다 —
    /// 입고를 박스로 세고, 재고를 "3박스 + 7개"로 보여주는 근거가 이 값이다.
    /// </summary>
    public bool IsBoxedProduct => UnitsPerBox > 1;

    /// <summary>
    /// 박스를 헐어 낱개로도 파는지. 위의 IsBoxedProduct와 <b>다른 값</b>이다.
    ///
    /// 한동안 둘이 같은 값이었다 — 박스에 30정이 들었으면 낱개로도 판다고 본 것이다.
    /// 그런데 한 박스에 30정이 들었지만 박스째로만 파는 상품이 있고, 그런 상품까지
    /// 낱개 판매로 잡히면 낱개가가 없는 채로 "낱개 판매 가능"한 상태가 된다.
    /// 포장은 제조사가 정하고 낱개 판매는 약국이 정하므로, 두 값이어야 한다.
    /// </summary>
    public bool SellsLooseUnits { get; set; }

    /// <summary>
    /// 낱개에 실제로 인쇄돼 있는 바코드. 블리스터 한 알, 시럽 한 병처럼
    /// 제조사가 낱개에도 따로 코드를 찍어 두는 상품이 있고, 그런 상품은
    /// 그 코드를 찍는 것이 자연스럽다 — 라벨을 뽑아 붙일 이유가 없다.
    ///
    /// 비워 두면 내부 바코드 + "-EA"가 쓰인다. 대부분의 상품은 그쪽이다.
    /// </summary>
    public string? UnitBarcodeOverride { get; set; }

    /// <summary>
    /// 낱개 판매용 바코드. 낱개에 실제로 인쇄된 코드가 있으면 그것을 쓰고,
    /// 없으면 내부 바코드에 -EA(Each)를 붙여 만든다.
    /// 낱개로 팔지 않는 상품에는 없다 — 찍을 일이 없는 코드를 만들 이유가 없다.
    /// </summary>
    public string? UnitBarcode
    {
        get
        {
            if (!SellsLooseUnits)
            {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(UnitBarcodeOverride))
            {
                return UnitBarcodeOverride.Trim();
            }

            return string.IsNullOrWhiteSpace(InternalBarcode)
                ? null
                : InternalBarcode + UnitBarcodeSuffix;
        }
    }

    /// <summary>낱개 바코드 접미사. 스캔 입력을 되돌려 읽을 때도 이 값을 쓴다.</summary>
    public const string UnitBarcodeSuffix = "-EA";

    /// <summary>
    /// 내부 바코드를 자동으로 만들 때 쓰는 접두사(INT-00000146 꼴).
    ///
    /// 손으로 입력하는 바코드는 이 접두사를 쓸 수 없다. 자동 채번은 다음 번호가
    /// 비어 있다고 보고 발급하므로, 사람이 INT-00000200을 미리 적어 두면 채번이
    /// 언젠가 그 번호에 닿아 저장이 거절된다 — 그때 원인은 몇 달 전에 만들어진 셈이라
    /// 현장에서 알 길이 없다. 번호 공간을 채번에만 맡겨 그 경우를 없앤다.
    /// </summary>
    public const string GeneratedBarcodePrefix = "INT-";

    /// <summary>
    /// 낱개 가격이 가질 수 있는 소수 자릿수.
    ///
    /// 네 자리인 이유는 가격이 리엘로 정해지기 때문이다 — 500리엘짜리 알약은 $0.125라
    /// 센트로 적을 수 없고, 1리엘은 환율 4,000에서 $0.00025다.
    /// </summary>
    public const int LooseUnitPriceDecimals = 4;

    // 낱개가를 박스가에서 자동으로 계산하던 기능은 없앴다.
    //
    // 비어 있으면 박스가 ÷ 개수를 썼는데, 그 값은 원가에 가까운 숫자이지 파는 가격이
    // 아니다 — 박스를 헐어 낱개로 파는 데에는 마진이 붙고, 얼마를 붙일지는 약국이
    // 정한다. 그런데 화면 어디에도 "이 가격은 앱이 지어낸 것"이라는 표시가 없어서,
    // 계산대에서는 약국이 정한 가격과 구별되지 않았다.
    //
    // 대신 낱개 판매를 켜려면 낱개가를 반드시 입력하게 했다(ProductService).
    // 값이 없는 상태 자체를 만들 수 없으므로 계산해서 메울 일도 없다.

    /// <summary>
    /// 낱개 하나의 원가. 원가는 판매가와 달리 박스가에서 나누기만 한다 —
    /// 낱개로 헐어 판다고 <b>매입</b> 단가가 달라지지는 않기 때문이다.
    /// 값을 지어내는 것이 아니라 이미 치른 돈을 개수로 배분하는 것이라 자동 계산이 맞다.
    /// </summary>
    public decimal UnitCostPrice => IsBoxedProduct
        ? decimal.Round(CostPrice / UnitsPerBox, LooseUnitPriceDecimals, MidpointRounding.AwayFromZero)
        : CostPrice;
}