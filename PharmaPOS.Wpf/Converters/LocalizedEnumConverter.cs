using System.Globalization;
using System.Windows.Data;
using Lightweight_Digital_Inventory_Management___POS_System.Services;

namespace Lightweight_Digital_Inventory_Management___POS_System.Converters;

/// <summary>
/// 화면에 그대로 찍히는 열거형 값을 현재 언어로 옮긴다.
/// <c>Text="{Binding AlertType, Converter={StaticResource LocalizedEnum},
/// ConverterParameter=ui.alert.type}"</c> 는 <c>ui.alert.type.LowStock</c>을 찾는다.
///
/// 열거형 값은 화면 문구인 동시에 DB에 저장되는 값이라 이름 자체를 바꿀 수 없다.
/// 그래서 값마다 라벨 속성을 만드는 대신, 접두사 + 값 이름으로 키를 조립한다.
///
/// 번역이 없으면 열거형 이름이 그대로 나온다 — 지금까지 보이던 것과 같아서,
/// 키를 다 채우지 않은 채로도 화면이 망가지지 않는다.
/// </summary>
public class LocalizedEnumConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is null)
        {
            return string.Empty;
        }

        var name = value.ToString() ?? string.Empty;

        if (parameter is not string prefix || prefix.Length == 0 || App.Services is null)
        {
            return name;
        }

        try
        {
            var uiLanguage = (UiLanguageService?)App.Services.GetService(typeof(UiLanguageService));
            return uiLanguage?.Text($"{prefix}.{name}", name) ?? name;
        }
        catch (Exception)
        {
            // 번역을 못 읽는다고 목록이 비어 보이면 안 된다.
            return name;
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
