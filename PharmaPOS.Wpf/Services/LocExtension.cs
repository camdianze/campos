using System.Windows.Markup;

namespace Lightweight_Digital_Inventory_Management___POS_System.Services;

/// <summary>
/// XAML에서 바로 쓰는 화면 문구. <c>Content="{svc:Loc ui.common.back, ← Back}"</c>
///
/// 화면마다 ViewModel에 라벨 속성을 열 개씩 다는 대신 이것을 쓴다. 속성 방식은
/// 버튼 하나 번역할 때마다 속성·알림·생성자 인자가 함께 늘어나서, 번역이 늘수록
/// 화면 코드가 번역 배선으로 뒤덮인다.
///
/// 값은 화면을 만들 때 한 번 정해진다. 언어 토글은 메인 화면에만 있어서 다른 화면이
/// 떠 있는 동안 언어가 바뀔 수 없고, 화면은 이동할 때마다 새로 만들어진다.
/// 메인 화면의 카드는 그 자리에서 바뀌어야 하므로 종전대로 ViewModel 속성을 쓴다.
/// </summary>
[MarkupExtensionReturnType(typeof(string))]
public class LocExtension : MarkupExtension
{
    public LocExtension()
    {
    }

    public LocExtension(string key, string english)
    {
        Key = key;
        English = english;
    }

    /// <summary>로케일 파일의 키.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>번역이 없을 때 그대로 쓰는 영어. 빈 버튼보다 영어 버튼이 낫다.</summary>
    public string English { get; set; } = string.Empty;

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        // 디자이너에서는 App.Services가 없다. 그때는 영어를 보여 준다.
        if (App.Services is null)
        {
            return English;
        }

        try
        {
            var uiLanguage = (UiLanguageService?)App.Services.GetService(typeof(UiLanguageService));
            return uiLanguage?.Text(Key, English) ?? English;
        }
        catch (Exception)
        {
            // 번역을 못 읽는다고 버튼 글자가 사라지면 안 된다.
            return English;
        }
    }
}
