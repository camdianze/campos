using System.Text.Json.Serialization;

namespace PharmaPOS.Tools.LicenseIssuer;

/// <summary>
/// 발급 1건. 로컬 대장(licenses.csv), 업로드 대기열, 클라우드 문서가 모두 이 모양을 공유한다.
///
/// LicensePayload를 그대로 쓰지 않는 이유: payload에는 서명 대상이 되는 값만 들어간다.
/// 고객명처럼 코드에는 없지만 대장에는 꼭 필요한 정보를 담으려면 별도 타입이 필요하다.
/// </summary>
public sealed class IssuanceRecord
{
    public uint SerialNumber { get; init; }

    public string CustomerName { get; init; } = string.Empty;

    /// <summary>Unix 초.</summary>
    public uint IssuedAt { get; init; }

    /// <summary>Unix 초. 0이면 무기한.</summary>
    public uint ExpiresAt { get; init; }

    public string Code { get; init; } = string.Empty;

    /// <summary>발급한 PC 이름. 나중에 발급 PC가 여러 대가 되면 누가 냈는지 구분할 근거가 된다.</summary>
    public string IssuedBy { get; init; } = string.Empty;

    /// <summary>ExpiresAt에서 나오는 값이라 대기열 파일에는 쓰지 않는다.</summary>
    [JsonIgnore]
    public bool IsPerpetual => ExpiresAt == 0;
}
