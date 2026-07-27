using System.Globalization;
using System.Windows.Data;

namespace Lightweight_Digital_Inventory_Management___POS_System.Converters;

/// <summary>
/// bool 값을 반전시키는 컨버터. CanEditUnitPrice(true=편집가능)를
/// TextBox.IsReadOnly(true=읽기전용, 즉 반대 의미)에 연결할 때 사용한다.
/// </summary>
public class InverseBooleanConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is bool b && !b;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is bool b && !b;
    }
}