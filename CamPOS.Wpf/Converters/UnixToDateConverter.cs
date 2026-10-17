using System.Globalization;
using System.Windows.Data;

namespace CamPOS.Wpf.Converters;

public class UnixToDateConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is long ms && ms > 0)
        {
            var date = DateTimeOffset.FromUnixTimeMilliseconds(ms).ToLocalTime();
            return date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }
        return "-";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}