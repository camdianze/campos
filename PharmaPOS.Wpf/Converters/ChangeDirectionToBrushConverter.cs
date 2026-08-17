using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using PharmaPOS.Application.Reports;

namespace Lightweight_Digital_Inventory_Management___POS_System.Converters;

/// <summary>
/// 증감 방향을 글자색으로 바꾼다. 상승은 붉게, 하락은 파랗게, 변화 없음은 흐리게.
///
/// 색을 App.xaml에서 이름으로 찾아오는 이유: 팔레트가 한 곳에 있어야 나중에 색을 고칠 때
/// 이 파일을 열지 않아도 된다. 리포트 카드와 표가 같은 변환기를 쓰므로 두 곳의 색이 갈릴 일도 없다.
/// </summary>
public class ChangeDirectionToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = value switch
        {
            ChangeDirection.Up => "ChangeUpBrush",
            ChangeDirection.Down => "ChangeDownBrush",
            _ => "MutedTextBrush"
        };

        // 디자이너에서는 Application.Current가 없다. 그때도 화면이 뜨도록 회색으로 둔다.
        return Application.Current?.TryFindResource(key) as Brush ?? Brushes.Gray;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException("색에서 방향을 되돌릴 일은 없다.");
}
