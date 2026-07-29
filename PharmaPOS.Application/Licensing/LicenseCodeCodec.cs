using System.Text;

namespace PharmaPOS.Application.Licensing;

/// <summary>
/// 라이선스 코드 문자열과 (내용 + 서명) 사이를 변환한다.
/// 발급 도구와 앱이 같은 규칙을 써야 하므로 Application에 두고 양쪽이 참조한다.
///
/// 코드 생김새: CAMPOS-XXXXXXXX-XXXXXXXX-... (Base32, 8글자씩 끊음)
/// 길이는 내용 13바이트 + 서명 64바이트 = 77바이트 → Base32 124글자로 고정이다.
/// 서명 64바이트는 줄일 수 없어서 코드를 더 짧게 만들 방법은 없다.
/// </summary>
public static class LicenseCodeCodec
{
    public const string Prefix = "CAMPOS";

    /// <summary>P-256 ECDSA 서명의 고정 길이(r 32바이트 + s 32바이트).</summary>
    public const int SignatureByteLength = 64;

    private const int GroupSize = 8;

    public static string Encode(LicensePayload payload, byte[] signature)
    {
        if (signature.Length != SignatureByteLength)
            throw new ArgumentException($"Signature must be {SignatureByteLength} bytes.", nameof(signature));

        var payloadBytes = payload.ToBytes();

        var combined = new byte[payloadBytes.Length + signature.Length];
        payloadBytes.CopyTo(combined, 0);
        signature.CopyTo(combined, payloadBytes.Length);

        return Prefix + "-" + GroupWithHyphens(Base32.Encode(combined));
    }

    /// <summary>
    /// 실패하면 false. 오타난 코드는 정상 흐름이므로 예외를 던지지 않는다.
    /// </summary>
    public static bool TryDecode(string code, out LicensePayload payload, out byte[] signature)
    {
        payload = new LicensePayload();
        signature = [];

        if (string.IsNullOrWhiteSpace(code))
            return false;

        var body = code.Trim();

        // 접두사는 있어도 되고 없어도 된다. 대소문자도 가리지 않는다.
        if (body.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
            body = body[Prefix.Length..];

        if (!Base32.TryDecode(body, out var combined))
            return false;

        if (combined.Length < LicensePayload.ByteLength + SignatureByteLength)
            return false;

        if (!LicensePayload.TryFromBytes(combined.AsSpan(0, LicensePayload.ByteLength), out payload))
            return false;

        signature = combined.AsSpan(LicensePayload.ByteLength, SignatureByteLength).ToArray();
        return true;
    }

    /// <summary>서명 대상 바이트. 발급 도구와 앱이 반드시 같은 값을 써야 한다.</summary>
    public static byte[] GetSignableBytes(LicensePayload payload) => payload.ToBytes();

    private static string GroupWithHyphens(string text)
    {
        var builder = new StringBuilder(text.Length + text.Length / GroupSize);

        for (var i = 0; i < text.Length; i++)
        {
            if (i > 0 && i % GroupSize == 0)
                builder.Append('-');

            builder.Append(text[i]);
        }

        return builder.ToString();
    }
}
