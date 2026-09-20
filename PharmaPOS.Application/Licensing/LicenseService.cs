using System.Security.Cryptography;

namespace PharmaPOS.Application.Licensing;

/// <summary>
/// 오프라인 라이선스 검증. 공개키 서명 방식이다.
///
/// 프로그램에는 공개키만 들어간다. 코드를 만드는 개인키는 발급자(별도 저장소 campos-license-issuer)만
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
    /// 값은 발급 저장소(campos-license-issuer)의 keygen 명령이 출력해 준다.
    ///
    /// 이 파일은 발급 저장소와 공유하지 않는다 — 검증만 하는 쪽이라 앱에만 있으면 된다.
    /// 공유하는 것은 Base32/LicensePayload/LicenseCodeCodec 세 개뿐이다.
    /// </summary>
    private const string PublicKeyBase64 =
        "MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEfaQ8ngaQCwVbt7v7R+oNxz14RCeMYyd1UH0AJaZULQl7LGl1XUiK+jslChMDEo+wdvc/dsuo4u+uZvQXqwVVwg==";

    /// <summary>
    /// 검증에 쓸 공개키. 운영에서는 위 상수이고, 테스트만 시험용 키로 바꿔 넣는다.
    /// 시험용 키를 끼울 자리가 없으면 만료 판정을 테스트로 고정할 수 없다 —
    /// 운영 개인키가 저장소에 없어서 테스트가 코드를 만들 수 없기 때문이다.
    /// 그리고 이 규칙은 조용히 사라진 전력이 있다: 저장된 코드를 다시 보지 않아
    /// 기한이 지나도 앱이 계속 열렸다.
    /// </summary>
    private readonly string _publicKeyBase64;

    public LicenseService(ILicenseActivationStore activationStore)
        : this(activationStore, PublicKeyBase64)
    {
    }

    internal LicenseService(ILicenseActivationStore activationStore, string publicKeyBase64)
    {
        _activationStore = activationStore;
        _publicKeyBase64 = publicKeyBase64;
    }

    /// <summary>
    /// 저장된 코드를 읽어 상태를 만든다. 서명과 기한을 그때그때 다시 본다 —
    /// 파일이 열리는지만 보던 예전 방식에서는 기한이 지나도 앱이 계속 열렸다.
    /// </summary>
    public LicenseStatus GetStatus()
    {
        var storedCode = _activationStore.ReadActivatedCode();

        if (storedCode is null || !TryVerify(storedCode, out var payload, out _))
        {
            return LicenseStatus.NotActivated();
        }

        if (payload!.IsPerpetual)
        {
            return LicenseStatus.Active(payload.SerialNumber, expiresOn: null);
        }

        var expiresOn = ToLocalDate(payload.ExpiresAt);

        return DateTimeOffset.UtcNow.ToUnixTimeSeconds() > payload.ExpiresAt
            ? LicenseStatus.Expired(payload.SerialNumber, expiresOn)
            : LicenseStatus.Active(payload.SerialNumber, expiresOn);
    }

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
                var expiredOn = ToLocalDate(payload.ExpiresAt);
                return LicenseActivationResult.Failure(
                    $"This license expired on {expiredOn:yyyy-MM-dd}. Please contact your supplier.");
            }

            // 연장하려다 짧은 코드를 넣는 경우를 막는다. 덮어쓰고 나면 되돌릴 수 없고,
            // 약국은 기한이 늘어난 줄 알고 있다가 예정보다 일찍 잠긴다.
            var current = GetStatus();

            if (current.CanRun && !current.IsPerpetual
                && current.ExpiresOn is { } currentExpiry
                && ToLocalDate(payload.ExpiresAt) < currentExpiry)
            {
                return LicenseActivationResult.Failure(
                    $"This code expires earlier ({ToLocalDate(payload.ExpiresAt):yyyy-MM-dd}) "
                    + $"than the license already on this PC ({currentExpiry:yyyy-MM-dd}).");
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

    /// <summary>만료 시각을 현지 날짜로. 화면에 적는 날짜는 전부 이 값을 쓴다.</summary>
    private static DateTime ToLocalDate(uint expiresAt) =>
        DateTimeOffset.FromUnixTimeSeconds(expiresAt).ToLocalTime().Date;

    /// <summary>코드를 풀고 포맷과 서명까지 확인한다. 기한은 부르는 쪽이 본다.</summary>
    private bool TryVerify(string licenseCode, out LicensePayload? payload, out byte[]? signature)
    {
        payload = null;
        signature = null;

        if (!LicenseCodeCodec.TryDecode(licenseCode, out var decoded, out var decodedSignature)
            || decoded.Version != LicensePayload.CurrentVersion
            || !IsSignatureValid(decoded, decodedSignature))
        {
            return false;
        }

        payload = decoded;
        signature = decodedSignature;
        return true;
    }

    private bool IsSignatureValid(LicensePayload payload, byte[] signature)
    {
        try
        {
            using var ecdsa = ECDsa.Create();
            ecdsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(_publicKeyBase64), out _);

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
