using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;

namespace Lightweight_Digital_Inventory_Management___POS_System.Services;

/// <summary>
/// XAML에서 바로 쓰는 화면 문구. <c>Content="{svc:Loc ui.common.back, ← Back}"</c>
///
/// 화면마다 ViewModel에 라벨 속성을 열 개씩 다는 대신 이것을 쓴다. 속성 방식은
/// 버튼 하나 번역할 때마다 속성·알림·생성자 인자가 함께 늘어나서, 번역이 늘수록
/// 화면 코드가 번역 배선으로 뒤덮인다.
///
/// 값은 문자열이 아니라 바인딩이다. 언어 토글이 화면마다 있어서, 화면이 떠 있는
/// 채로 언어가 바뀌면 그 자리에서 글자가 갈려야 한다. 문자열을 돌려주면 화면을
/// 만들 때의 언어로 굳어 버려, 바꾸려면 화면을 나갔다 들어와야 한다 — 토글을
/// 화면마다 둔 이유가 바로 그 왕복을 없애는 것이다.
/// </summary>
[MarkupExtensionReturnType(typeof(object))]
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
        // 디자이너에서는 App.Services가 없다. 그때는 영어를 그대로 보여 준다.
        if (App.Services?.GetService(typeof(UiLanguageService)) is not UiLanguageService uiLanguage)
        {
            return English;
        }

        var binding = new Binding(nameof(LocalizedString.Value))
        {
            Source = new LocalizedString(uiLanguage, Key, English),
            Mode = BindingMode.OneWay
        };

        return binding.ProvideValue(serviceProvider);
    }
}

/// <summary>
/// 키 하나의 현재 언어 문구. 언어가 바뀌면 Value가 바뀌었다고 알린다.
///
/// 화면의 라벨 하나마다 이 객체가 하나씩 생긴다. 언어 서비스는 앱과 수명이 같으므로
/// 보통 방식으로 구독하면 닫힌 화면의 라벨들이 전부 여기에 매달려 살아남는다.
/// WeakEventManager로 구독해 두면 화면이 걷힐 때 함께 걷힌다.
/// </summary>
public sealed class LocalizedString : INotifyPropertyChanged
{
    private readonly UiLanguageService _uiLanguage;
    private readonly string _key;
    private readonly string _english;

    public LocalizedString(UiLanguageService uiLanguage, string key, string english)
    {
        _uiLanguage = uiLanguage;
        _key = key;
        _english = english;

        WeakEventManager<UiLanguageService, EventArgs>.AddHandler(
            _uiLanguage, nameof(UiLanguageService.LanguageChanged), OnLanguageChanged);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Value => _uiLanguage.Text(_key, _english);

    private void OnLanguageChanged(object? sender, EventArgs e) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
}
