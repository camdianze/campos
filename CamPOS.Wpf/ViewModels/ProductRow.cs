using System.Windows.Media;
using PharmaPOS.Domain.Entities;

// 속성 이름(AwareGroup)이 타입 이름과 같아 네임스페이스로 풀어 쓰면 가려진다.
// 별칭을 두어 switch에서 열거형 값을 명확히 가리킨다.
using Group = PharmaPOS.Domain.Enums.AwareGroup;

namespace Lightweight_Digital_Inventory_Management___POS_System.ViewModels;

/// <summary>
/// 상품 목록의 한 줄. 상품 자체와, 그 상품이 어느 AWaRe 그룹으로 판별됐는지를 함께 들고 있다.
///
/// 분류를 상품에 저장하지 않고 화면에서 계산하는 이유: AWaRe 분류는 WHO 참조 파일에
/// 딸린 값이라 개정판으로 교체하면 바뀐다. 저장해 두면 그 순간부터 낡은 값이 남는다.
/// </summary>
public class ProductRow
{
    public required Product Product { get; init; }

    /// <summary>참조 데이터에서 찾지 못했으면 null (항생제가 아니거나 목록에 없는 항목).</summary>
    public Group? AwareGroup { get; init; }

    public bool HasAwareGroup => AwareGroup is not null;

    /// <summary>
    /// 상품명 옆 점 색. 신호등 순서를 따른다 — 초록(ACCESS)에서 빨강(RESERVE)으로 갈수록
    /// 처방 없이 나가면 안 되는 계열이고, 검정은 WHO가 권장하지 않는 복합제다.
    /// </summary>
    public Brush DotBrush => AwareGroup switch
    {
        Group.Access => AccessBrush,
        Group.Watch => WatchBrush,
        Group.Reserve => ReserveBrush,
        Group.NotRecommended => NotRecommendedBrush,
        _ => Brushes.Transparent
    };

    /// <summary>점에 마우스를 올렸을 때 보여줄 설명. 색만으로는 어느 그룹인지 알 수 없다.</summary>
    public string AwareTooltip => AwareGroup switch
    {
        Group.Access => "ACCESS - first choice, lowest resistance risk",
        Group.Watch => "WATCH - higher resistance potential; prescription strongly recommended",
        Group.Reserve => "RESERVE - last resort; prescription strongly recommended",
        Group.NotRecommended => "NOT RECOMMENDED - fixed-dose combination not recommended by WHO",
        _ => string.Empty
    };

    // 순수 노랑(#FFFF00)은 흰 배경에서 거의 안 보여 호박색을 쓴다.
    private static readonly Brush AccessBrush = Freeze("#16A34A");
    private static readonly Brush WatchBrush = Freeze("#F59E0B");
    private static readonly Brush ReserveBrush = Freeze("#DC2626");
    private static readonly Brush NotRecommendedBrush = Freeze("#111827");

    private static Brush Freeze(string hex)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        brush.Freeze();
        return brush;
    }
}
