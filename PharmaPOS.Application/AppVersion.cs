namespace PharmaPOS.Application;

/// <summary>
/// 앱 버전. 화면에 찍는 것과 내보낸 파일에 남기는 것이 갈라지지 않도록 한 곳에 둔다.
///
/// 전에는 App.xaml의 문자열 리소스에만 있어서 Application 계층이 알 수 없었고,
/// 그래서 내보낸 파일에는 어느 버전이 만든 것인지 아무 표시가 없었다.
/// 몇 달 뒤 받은 CSV가 어느 버전에서 나온 것인지 물어볼 곳이 파일 이름밖에 없다.
/// </summary>
public static class AppVersion
{
    public const string Number = "1.10";

    /// <summary>화면에 그대로 찍는 표기.</summary>
    public const string Display = "CamPOS v." + Number;

    /// <summary>
    /// 파일 이름에 넣는 형태. 공백과 점 앞의 v를 붙여 두어
    /// products_v1.10_20260819.csv 처럼 시각과 나란히 읽힌다.
    /// </summary>
    public const string FileTag = "v" + Number;
}
