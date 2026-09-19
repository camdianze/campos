namespace PharmaPOS.Application.Inventory;

/// <summary>
/// 재고 조정 화면(SCR-ADJ-010)에서 배치를 선택할 때 쓰는 요약 정보.
/// </summary>
public class InventoryBatchOption
{
    public required string InventoryId { get; set; }
    public required string BatchNumber { get; set; }
    public required long ExpiryDate { get; set; }

    /// <summary>낱개 기준 총 재고. 박스/낱개 상품이라도 재고 판정은 항상 이 값으로 한다.</summary>
    public required int CurrentQuantity { get; set; }

    /// <summary>아직 뜯지 않은 박스 수.</summary>
    public int BoxQuantity { get; set; }

    /// <summary>헐어 놓은 낱개 수.</summary>
    public int UnitQuantity { get; set; }

    /// <summary>계산에 그대로 넘길 수 있게 묶어 둔 것.</summary>
    public BoxUnitStock Stock => new(CurrentQuantity, BoxQuantity, UnitQuantity);

    /// <summary>
    /// 화면이 이 객체를 글자로 찍어야 할 때(콤보의 선택 항목이 목록에 없을 때 등)
    /// 클래스 이름 대신 배치번호가 나온다. 정상 경로는 DisplayMemberPath가 맡는다.
    /// </summary>
    public override string ToString() => BatchNumber;
}