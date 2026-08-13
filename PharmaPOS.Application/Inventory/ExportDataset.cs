namespace PharmaPOS.Application.Inventory;

/// <summary>
/// 내보낼 수 있는 데이터 묶음.
///
/// DB 테이블을 그대로 열거하지 않는 이유가 둘 있다. 첫째, Users에는 비밀번호 해시와
/// 메일 설정이 들어 있어 내보내기 목록에 있을 이유가 없다. 둘째, 원본 테이블은 상품 ID만
/// 들고 있어 사람이 열어 봐야 알아볼 수가 없다 — 그래서 이름을 붙여 내보낸다.
/// </summary>
public enum ExportDataset
{
    /// <summary>상품 목록. 헤더가 임포트 서식과 같아 고쳐서 다시 넣을 수 있다.</summary>
    Products,

    /// <summary>배치별 현재 재고.</summary>
    Inventory,

    /// <summary>판매와 환불 내역.</summary>
    SalesHistory
}
