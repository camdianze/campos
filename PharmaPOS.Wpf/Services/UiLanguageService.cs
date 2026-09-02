using System.ComponentModel;
using System.Windows.Media;
using PharmaPOS.Application.Counselling;
using PharmaPOS.Application.Repositories;

namespace Lightweight_Digital_Inventory_Management___POS_System.Services;

/// <summary>
/// 화면 글자의 언어. 영어가 기본이고, 크메르어는 덧씌우는 층이다.
///
/// 계산대에 서는 직원이 영어를 못 읽는 경우가 있어서 넣은 것이라, 번역이 없는 키는
/// 조용히 영어로 남는다 — 빈 칸이나 키 이름이 화면에 뜨는 쪽이 훨씬 나쁘다.
///
/// 복약안내 시트·영수증과 달리 검수(review_status)로 막지 않는다. 그쪽은 환자가
/// 그대로 따라 하는 글이지만 이쪽은 버튼에 적힌 낱말이고, 어색하면 현장에서 바로
/// 눈에 띈다. 대신 미검수 로케일은 붉은 글씨로 나온다.
/// </summary>
public class UiLanguageService : INotifyPropertyChanged
{
    /// <summary>고른 언어가 남는 자리. 다음에 켤 때 그대로 열린다.</summary>
    public const string SettingKey = "ui.language";

    public const string English = "en";
    public const string Khmer = "km-KH";

    /// <summary>
    /// 미검수 번역을 칠하는 색. 검수를 마치고 approved로 바꾸면 이 색이 사라진다.
    /// 배지나 각주 대신 글자색으로만 알리는 것은, 메인 화면에 표시가 덕지덕지
    /// 붙으면 정작 읽어야 할 낱말이 묻히기 때문이다.
    /// </summary>
    public static readonly Brush UnreviewedBrush = new SolidColorBrush(Color.FromRgb(0xDC, 0x26, 0x26));

    private readonly ICounsellingLocaleProvider _localeProvider;
    private readonly IAppSettingRepository _appSettingRepository;

    private CounsellingLocale? _khmer;
    private string _current = English;

    public UiLanguageService(
        ICounsellingLocaleProvider localeProvider,
        IAppSettingRepository appSettingRepository)
    {
        _localeProvider = localeProvider;
        _appSettingRepository = appSettingRepository;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>언어가 바뀌었다. 화면들이 글자를 다시 읽어 가라는 신호.</summary>
    public event Action? LanguageChanged;

    public bool IsKhmer => _current == Khmer;

    /// <summary>
    /// 크메르어를 보여 주고 있고, 그 번역이 아직 검수 전인가.
    /// 영어일 때는 언제나 false — 영어는 원문이라 검수할 것이 없다.
    /// </summary>
    public bool IsShowingUnreviewedTranslation => IsKhmer && _khmer is not null && !_khmer.IsApproved;

    /// <summary>글자에 칠할 색. 검수된 번역과 영어는 null(원래 색 그대로).</summary>
    public Brush? TextBrushOverride => IsShowingUnreviewedTranslation ? UnreviewedBrush : null;

    /// <summary>앱이 시작할 때 한 번. 로케일 파일과 지난번 선택을 읽어 둔다.</summary>
    public async Task InitializeAsync()
    {
        try
        {
            _khmer = await _localeProvider.GetLocaleAsync(Khmer);
        }
        catch (Exception)
        {
            // 로케일을 못 읽어도 앱은 떠야 한다. 영어로 간다.
            _khmer = null;
        }

        try
        {
            var saved = await _appSettingRepository.GetAsync(SettingKey);
            if (saved == Khmer && IsKhmerAvailable)
            {
                _current = Khmer;
            }
        }
        catch (Exception)
        {
            // 설정을 못 읽으면 기본값인 영어.
        }
    }

    /// <summary>
    /// 고를 만한 크메르어가 실제로 있는가.
    ///
    /// 파일이 없거나 깨졌을 때 제공자는 null이 아니라 EnglishOnly를 돌려준다.
    /// 그것을 "있다"로 세면 토글은 보이는데 눌러도 아무것도 안 바뀌고,
    /// 원인이 파일이라는 걸 화면만 봐서는 알 수 없다. 그래서 코드까지 확인한다.
    /// </summary>
    public bool IsKhmerAvailable => _khmer is not null && _khmer.LocaleCode == Khmer;

    public async Task SetLanguageAsync(string language)
    {
        var next = language == Khmer && IsKhmerAvailable ? Khmer : English;

        if (next == _current)
        {
            return;
        }

        _current = next;

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsKhmer)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsShowingUnreviewedTranslation)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TextBrushOverride)));
        LanguageChanged?.Invoke();

        try
        {
            await _appSettingRepository.SetAsync(SettingKey, _current, valueType: "enum");
        }
        catch (Exception)
        {
            // 저장에 실패해도 이번 세션은 바뀐 언어로 쓴다.
        }
    }

    /// <summary>
    /// 그 키의 화면 문구. 영어일 때, 번역이 없을 때, 로케일을 못 읽었을 때는
    /// 넘겨받은 영어를 그대로 돌려준다.
    /// </summary>
    public string Text(string key, string english)
    {
        if (!IsKhmer || _khmer is null)
        {
            return english;
        }

        return _khmer.GetInterfaceString(key) ?? english;
    }
}
