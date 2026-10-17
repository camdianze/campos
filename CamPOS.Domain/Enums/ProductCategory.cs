namespace PharmaPOS.Domain.Enums;

/// <summary>
/// 상품이 의약품인지 아닌지. 다른 enum들과 같이 이름 그대로 TEXT로 저장한다.
///
/// 선택 입력이라 Product.Category는 nullable이다 — 아직 정하지 않은 상품과
/// "비의약품으로 정한" 상품은 다른 상태이고, 그 둘을 섞으면 분류 작업이
/// 어디까지 됐는지 알 수 없게 된다.
/// </summary>
public enum ProductCategory
{
    Medicine,
    NonMedicine
}
