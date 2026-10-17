using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace Lightweight_Digital_Inventory_Management___POS_System.Converters;

/// <summary>
/// AWaRe 등급 코드를 색으로 바꾼다.
///
/// 초록 → 주황 → 빨강 → 진한 빨강. 등급 자체가 순서가 있는 값이라(ACCESS가 가장 권장,
/// NOT_RECOMMENDED가 가장 피해야 함) 색도 순서 있는 계열로 둔다. 임의의 네 색을 쓰면
/// 어느 쪽이 나쁜 쪽인지 범례를 봐야 알 수 있다.
///
/// 색을 App.xaml 팔레트에서 찾아오는 이유는 ChangeDirectionToBrushConverter와 같다 —
/// 그래프와 비율 표시가 같은 색을 쓰게 하려면 이름이 한 곳에 있어야 한다.
/// </summary>
public class AwareGroupToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // 괄호가 필요하다. as와 switch는 우선순위가 헷갈려 컴파일러가 경고한다(CS8848).
        var key = (value as string) switch
        {
            "ACCESS" => "AwareAccessBrush",
            "WATCH" => "AwareWatchBrush",
            "RESERVE" => "AwareReserveBrush",
            "NOT_RECOMMENDED" => "AwareNotRecommendedBrush",
            _ => "MutedTextBrush"
        };

        // 디자이너에서는 Application.Current가 없다. 그때도 화면이 뜨도록 회색으로 둔다.
        return Application.Current?.TryFindResource(key) as Brush ?? Brushes.Gray;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException("색에서 등급을 되돌릴 일은 없다.");
}
