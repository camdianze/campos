using System.Globalization;

namespace PharmaPOS.Application.Licensing;

/// <summary>
/// 이 PC의 라이선스 상태. 시작 화면을 고르는 데도, 남은 기간 배지에도, 관리자 화면의
/// 라이선스 항목에도 같은 값이 쓰인다.
///
/// 상태를 한 곳에서 만드는 이유: 예전에는 "활성화됨"이 <c>license.dat</c>이 열리는지만
/// 보는 값이라, 기한이 지난 코드로도 앱이 계속 열렸다. 만료 판정이 코드를 입력하는
/// 순간에만 있고 그 뒤로는 아무도 다시 보지 않았기 때문이다. 세 화면이 각자 판정하면
/// 같은 일이 또 생기므로, 저장된 코드를 다시 검증해 나온 이 값 하나만 보게 한다.
/// </summary>
public class LicenseStatus
{
    private LicenseStatus(LicenseState state, uint serialNumber, DateTime? expiresOn)
    {
        State = state;
        SerialNumber = serialNumber;
        ExpiresOn = expiresOn;
    }

    public LicenseState State { get; }

    /// <summary>발급 대장과 맞춰 보는 일련번호. 문의가 오면 이것으로 찾는다.</summary>
    public uint SerialNumber { get; }

    /// <summary>만료일(현지). 영구 라이선스이거나 활성화 전이면 null.</summary>
    public DateTime? ExpiresOn { get; }

    public bool IsPerpetual => State == LicenseState.Active && ExpiresOn is null;

    /// <summary>앱을 쓸 수 있는 상태인지. 만료됐거나 활성화 전이면 false.</summary>
    public bool CanRun => State == LicenseState.Active;

    public bool IsExpired => State == LicenseState.Expired;

    /// <summary>
    /// 만료까지 남은 날. 오늘 만료면 0이고, 이미 지났으면 음수다.
    /// 영구이거나 활성화 전이면 null.
    /// </summary>
    public int? DaysRemaining => ExpiresOn is { } expiry
        ? (int)(expiry.Date - DateTime.Today).TotalDays
        : null;

    /// <summary>
    /// 남은 기간 배지를 띄우기 시작하는 시점. 30일이면 다음 방문 일정을 잡을 여유가 있고,
    /// 그보다 길면 배지가 늘 떠 있어 아무도 읽지 않게 된다.
    /// </summary>
    public const int WarningDays = 30;

    public bool ShouldWarn => DaysRemaining is { } days && days <= WarningDays;

    /// <summary>화면에 적는 만료일. 영구면 그렇게 적는다.</summary>
    public string ExpiryDisplay => ExpiresOn is { } expiry
        ? expiry.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
        : State == LicenseState.Active ? "No expiry date" : "—";

    public string SerialDisplay => State == LicenseState.NotActivated
        ? "—"
        : SerialNumber.ToString("D6", CultureInfo.InvariantCulture);

    /// <summary>
    /// 배지와 관리자 화면에 함께 쓰는 한 줄. 날짜만 적으면 며칠 남았는지 세어야 하고,
    /// 날수만 적으면 언제까지인지 알 수 없어 둘을 같이 적는다.
    /// </summary>
    public string Summary => State switch
    {
        LicenseState.NotActivated => "Not activated",
        LicenseState.Expired => $"Expired on {ExpiryDisplay}",
        _ when IsPerpetual => "Activated · no expiry date",
        _ => DaysRemaining switch
        {
            0 => $"Expires today ({ExpiryDisplay})",
            1 => $"Expires tomorrow ({ExpiryDisplay})",
            { } days => $"{days} days left · expires {ExpiryDisplay}"
        }
    };

    public static LicenseStatus NotActivated() =>
        new(LicenseState.NotActivated, 0, null);

    public static LicenseStatus Active(uint serialNumber, DateTime? expiresOn) =>
        new(LicenseState.Active, serialNumber, expiresOn);

    public static LicenseStatus Expired(uint serialNumber, DateTime expiresOn) =>
        new(LicenseState.Expired, serialNumber, expiresOn);
}

public enum LicenseState
{
    /// <summary>코드를 넣은 적이 없거나, 저장된 코드를 더는 신뢰할 수 없다.</summary>
    NotActivated,

    Active,

    /// <summary>코드 자체는 진짜인데 기한이 지났다. 연장 코드를 넣으면 다시 열린다.</summary>
    Expired
}
