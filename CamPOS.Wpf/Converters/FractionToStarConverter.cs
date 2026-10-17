using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Lightweight_Digital_Inventory_Management___POS_System.Converters;

/// <summary>
/// 0~1 사이의 값을 Grid 칸의 별(*) 너비로 바꾼다.
///
/// 비율 막대를 그리는 가장 단순한 방법이다. 픽셀 너비를 계산하려면 남은 공간을
/// 알아야 하는데 그건 배치가 끝나야 정해지는 값이라, 두 칸의 비율로 맡긴다.
/// 창을 줄여도 막대 비율이 그대로 유지되는 것은 덤이다.
/// </summary>
public class FractionToStarConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var fraction = value is double number && double.IsFinite(number) ? number : 0;

        // 음수 별 너비는 예외가 된다. 반올림 오차로 -0.0000001이 들어올 수 있어 잘라 둔다.
        return new GridLength(Math.Max(0, fraction), GridUnitType.Star);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException("칸 너비에서 비율을 되돌릴 일은 없다.");
}
