using PharmaPOS.Domain.Entities;

namespace PharmaPOS.Application.Repositories;

/// <summary>
/// 입고(Stock-in) 데이터 저장을 담당하는 인터페이스.
/// </summary>
public interface IStockInRepository
{
    /// <summary>
    /// Stock_Transaction에 STOCK_IN 기록을 남기고, Inventory 수량을 반영한다.
    /// 동일한 facility_id + product_id + batch_number 조합이 이미 있으면 수량을 증가시키고,
    /// 없으면 새 Inventory 행을 생성한다. 하나라도 실패하면 전체 롤백된다.
    /// (Screen SCR-STOCKIN-009, 5절 "저장 원칙")
    /// </summary>
    Task SaveStockInAsync(StockTransaction transaction);
}