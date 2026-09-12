using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using Lightweight_Digital_Inventory_Management___POS_System.Services;

namespace Lightweight_Digital_Inventory_Management___POS_System.Controls;

/// <summary>
/// EN / ខ្មែរ 토글. 크메르어가 있는 화면마다 같은 자리에 하나씩 둔다.
///
/// 메인 화면에만 있으면 영어로 잠깐 보고 싶을 때마다 나갔다 들어와야 한다.
/// 화면 안에서 바꾸면 그 자리에서 글자가 갈리므로(LocExtension이 바인딩을 돌려준다)
/// 그 왕복이 없어진다.
///
/// 라디오 둘은 서로 다른 화면의 토글과 GroupName을 공유하지 않는다 — 화면마다
/// 컨트롤이 따로라 GroupName은 이 컨트롤 안에서만 유효하다.
/// </summary>
public partial class LanguageToggle : UserControl
{
    /// <summary>
    /// 처음부터 켜 둔다. 필드 초기화는 생성자 본문보다 먼저 돌기 때문에,
    /// InitializeComponent()가 라디오를 붙이며 쏘는 Checked까지 이 플래그가 덮는다.
    /// </summary>
    private bool _suppressLanguageChange = true;

    public LanguageToggle()
    {
        InitializeComponent();

        // 디자이너에서는 서비스가 없다.
        if (App.Services is null)
        {
            _suppressLanguageChange = false;
            return;
        }

        var uiLanguage = App.Services.GetRequiredService<UiLanguageService>();

        // 크메르어 파일이 없거나 깨졌으면 고를 것이 없으므로 통째로 감춘다 —
        // 눌러도 아무 일이 없는 버튼이 더 나쁘다.
        if (!uiLanguage.IsKhmerAvailable)
        {
            Visibility = Visibility.Collapsed;
            _suppressLanguageChange = false;
            return;
        }

        // 여기서 붙이는 IsChecked는 사용자가 누른 것이 아니므로 저장을 부르지 않는다.
        EnglishOption.IsChecked = !uiLanguage.IsKhmer;
        KhmerOption.IsChecked = uiLanguage.IsKhmer;
        _suppressLanguageChange = false;

        // 다른 화면의 토글에서 바뀌어도 이 토글이 따라가야 한다. 언어 서비스는 앱과
        // 수명이 같으니 약한 구독으로 걸어, 화면이 걷힐 때 함께 걷히게 한다.
        WeakEventManager<UiLanguageService, EventArgs>.AddHandler(
            uiLanguage, nameof(UiLanguageService.LanguageChanged), OnLanguageChanged);
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        if (sender is not UiLanguageService uiLanguage)
        {
            return;
        }

        _suppressLanguageChange = true;
        EnglishOption.IsChecked = !uiLanguage.IsKhmer;
        KhmerOption.IsChecked = uiLanguage.IsKhmer;
        _suppressLanguageChange = false;
    }

    private async void OnLanguageChecked(object sender, RoutedEventArgs e)
    {
        if (_suppressLanguageChange || App.Services is null)
        {
            return;
        }

        var uiLanguage = App.Services.GetRequiredService<UiLanguageService>();

        await uiLanguage.SetLanguageAsync(
            ReferenceEquals(sender, KhmerOption) ? UiLanguageService.Khmer : UiLanguageService.English);
    }
}
