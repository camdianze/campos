using PharmaPOS.Application.Inventory;
using Lightweight_Digital_Inventory_Management___POS_System.ViewModels.Base;

namespace Lightweight_Digital_Inventory_Management___POS_System.ViewModels;

/// <summary>
/// 환불 창의 한 줄. 판매된 줄 하나와, 이번에 얼마나 되돌릴지를 들고 있다.
/// </summary>
public class RefundLineRow : ViewModelBase
{
    private int _refundQuantity;

    public required RefundableLine Line { get; init; }

    public string ProductName => Line.ProductName;
    public string BatchNumber => Line.BatchNumber;
    public int SoldQuantity => Line.SoldQuantity;
    public int RefundedQuantity => Line.RefundedQuantity;
    public int RemainingQuantity => Line.RemainingQuantity;
    public decimal UnitPrice => Line.UnitPrice;

    /// <summary>
    /// 이번에 되돌릴 수량. 판매 수량(에서 이미 환불된 만큼을 뺀 값)을 넘는 입력은
    /// 그 자리에서 잘라 낸다 — 계산대 앞에서는 오류 메시지보다 이쪽이 빠르다.
    /// (서비스와 저장 계층에서도 같은 한도를 다시 검사한다.)
    /// </summary>
    public int RefundQuantity
    {
        get => _refundQuantity;
        set
        {
            var clamped = Math.Clamp(value, 0, RemainingQuantity);

            if (SetProperty(ref _refundQuantity, clamped))
            {
                OnPropertyChanged(nameof(Amount));
                return;
            }

            // 잘라 낸 값이 이미 들고 있던 값과 같으면 SetProperty는 아무 알림도 보내지 않는다.
            // 그대로 두면 입력칸에는 방금 친 숫자(3개 판 걸 9로 친 경우의 "9")가 남아,
            // 화면과 실제 환불 수량이 어긋난 채로 보인다. 그래서 직접 알림을 보내 되돌린다.
            if (clamped != value)
            {
                OnPropertyChanged();
            }
        }
    }

    public decimal Amount => UnitPrice * RefundQuantity;

    public void FillRemaining() => RefundQuantity = RemainingQuantity;
}
