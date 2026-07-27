using System.Globalization;
using System.Windows.Data;

namespace Lightweight_Digital_Inventory_Management___POS_System.Converters;

public class UnixToDateConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is long ms && ms > 0)
        {
            var date = DateTimeOffset.FromUnixTimeMilliseconds(ms).ToLocalTime();
            return date.ToString("yyyy-MM-dd");
        }
        return "-";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}