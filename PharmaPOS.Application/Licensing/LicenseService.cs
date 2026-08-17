using System.Security.Cryptography;

namespace PharmaPOS.Application.Licensing;

/// <summary>
/// 오프라인 라이선스 검증. 공개키 서명 방식이다.
///
/// 프로그램에는 공개키만 들어간다. 코드를 만드는 개인키는 발급자(tools/LicenseIssuer)만
/// 갖고 있고 배포본에는 절대 포함되지 않는다. 그래서 이 exe를 아무리 뜯어봐도
/// 새 코드를 만들어낼 수 없다 — 공개키로는 "이 코드가 맞는가"에 예/아니오만 답할 수 있다.
///
/// 왜 해시 비교에서 바꿨는가:
/// 정답 코드의 해시를 내장하면 코드 후보를 대입해 맞는 걸 찾아낼 수 있고,
/// 새 코드를 발급할 때마다 재빌드·재배포가 필요했다. 서명 방식은 둘 다 없다.
///
/// 여전히 막지 못하는 것: 고객이 자기 코드를 남에게 알려주는 것.
/// 서명은 위조를 막지 공유를 막지 않는다. 공유까지 막으려면 발급 코드에
/// 그 PC의 지문을 함께 서명해 넣는 기기 바인딩이 필요하다.
/// </summary>
public class LicenseService : ILicenseService
{
    private readonly ILicenseActivationStore _activationStore;

    /// <summary>
    /// 발급용 개인키와 짝을 이루는 공개키(SubjectPublicKeyInfo, Base64).
    ///
    /// 이 값을 바꾸면 이전 개인키로 발급한 코드는 전부 무효가 된다.
    /// 이미 활성화를 마친 PC는 license.dat이 있어 영향받지 않는다.
    /// 값은 tools/LicenseIssuer의 keygen 명령이 출력해 준다.
    /// </summary>
    private const string PublicKeyBase64 =
        "MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEfaQ8ngaQCwVbt7v7R+oNxz14RCeMYyd1UH0AJaZULQl7LGl1XUiK+jslChMDEo+wdvc/dsuo4u+uZvQXqwVVwg==";

    public LicenseService(ILicenseActivationStore activationStore)
    {
        _activationStore = activationStore;
    }

    public bool IsActivated() => _activationStore.IsActivated();

    public LicenseActivationResult Activate(string licenseCode)
    {
        if (string.IsNullOrWhiteSpace(licenseCode))
        {
            return LicenseActivationResult.Failure("Please enter your license code.");
        }

        if (!LicenseCodeCodec.TryDecode(licenseCode, out var payload, out var signature))
        {
            return LicenseActivationResult.Failure("This license code is not valid.");
        }

        // 이 앱보다 나중에 만들어진 포맷이면 내용을 잘못 읽을 수 있으므로 거부한다.
        if (payload.Version != LicensePayload.CurrentVersion)
        {
            return LicenseActivationResult.Failure(
                "This license code requires a newer version of CamPOS.");
        }

        if (!IsSignatureValid(payload, signature))
        {
            return LicenseActivationResult.Failure("This license code is not valid.");
        }

        if (!payload.IsPerpetual)
        {
            var nowUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            if (nowUnixSeconds > payload.ExpiresAt)
            {
                var expiredOn = DateTimeOffset.FromUnixTimeSeconds(payload.ExpiresAt).ToLocalTime();
                return LicenseActivationResult.Failure(
                    $"This license expired on {expiredOn:yyyy-MM-dd}. Please contact your supplier.");
            }
        }

        try
        {
            _activationStore.SaveActivation(licenseCode.Trim());
        }
        catch (Exception)
        {
            return LicenseActivationResult.Failure(
                "The license code is valid but activation could not be saved. Please check that you can write to your user folder.");
        }

        return LicenseActivationResult.Success();
    }

    private static bool IsSignatureValid(LicensePayload payload, byte[] signature)
    {
        try
        {
            using var ecdsa = ECDsa.Create();
            ecdsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(PublicKeyBase64), out _);

            return ecdsa.VerifyData(
                LicenseCodeCodec.GetSignableBytes(payload),
                signature,
                HashAlgorithmName.SHA256,
                DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        }
        catch (Exception)
        {
            // 공개키 상수가 잘못 들어갔거나 서명 형식이 어긋난 경우.
            // 어느 쪽이든 이 코드는 통과시킬 수 없다.
            return false;
        }
    }
}
