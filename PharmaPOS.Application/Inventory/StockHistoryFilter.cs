namespace PharmaPOS.Application.Inventory;

/// <summary>
/// 재고 이력 화면의 종류 필터.
///
/// 기본값이 All인 것은 화면을 채우기 위해서가 아니다. stock_before/stock_after는
/// 한 배치를 시간순으로 훑으면서 앞 줄의 After와 다음 줄의 Before가 어긋나는 지점을
/// 찾는 값인데, 종류별로 목록을 갈라 놓으면 중간 줄이 빠져서 어느 쪽에서도
/// 그 어긋남이 보이지 않는다. 좁혀 보는 건 그다음 일이다.
/// </summary>
public enum StockHistoryFilter
{
    All,
    StockIn,
    Adjustment,

    /// <summary>판매와 환불. 환불은 판 것을 되돌린 줄이라 판매와 떨어지면 읽을 수 없다.</summary>
    Sale
}
