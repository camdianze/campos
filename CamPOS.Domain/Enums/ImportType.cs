namespace PharmaPOS.Domain.Enums;

/// <summary>
/// 초기 재고 임포트의 종류. 같은 파일이라도 종류가 다르면 따로 기록한다 —
/// 한 파일로 상품을 넣은 뒤 같은 파일로 재고를 넣는 것이 정상 순서이기 때문이다.
/// </summary>
public enum ImportType
{
    Products,
    Inventory
}
