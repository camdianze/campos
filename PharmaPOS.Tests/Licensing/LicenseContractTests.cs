using System.Security.Cryptography;
using System.Text.Json;
using PharmaPOS.Application.Licensing;

namespace PharmaPOS.Tests.Licensing;

/// <summary>
/// 라이선스 코드 형식의 계약 검사.
///
/// 이 저장소(발급)와 CamPOS 저장소(검증)에는 같은 세 파일 — Base32, LicensePayload,
/// LicenseCodeCodec — 이 사본으로 존재한다. 한쪽만 고치면 새로 발급한 코드가 고객 PC에서
/// 거부되는데, 발급도 대장 기록도 코드 생김새도 전부 정상으로 보여서
/// <b>계산대에서 거부당하고 나서야</b> 안다. 그때는 이미 고객 앞이다.
///
/// 그래서 양쪽 저장소가 같은 license-vectors.json을 들고 같은 것을 검사한다.
/// 한쪽이 형식을 건드리면 고객이 아니라 그쪽 빌드가 먼저 깨진다.
///
/// 벡터의 키는 <b>시험용</b>이다. 운영 개인키는 %APPDATA%\PharmaPOS.Issuer\private.key 에
/// 있고 저장소에 들어오지 않는다.
/// </summary>
public class LicenseContractTests
{
    private static JsonDocument LoadVectors()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "license-vectors.json");

        Assert.True(File.Exists(path), "license-vectors.json 이 빌드 출력에 없다. csproj의 None 항목을 확인할 것.");

        return JsonDocument.Parse(File.ReadAllText(path));
    }

    public static TheoryData<int> VectorIndexes()
    {
        using var document = LoadVectors();

        var data = new TheoryData<int>();

        for (var i = 0; i < document.RootElement.GetProperty("vectors").GetArrayLength(); i++)
        {
            data.Add(i);
        }

        return data;
    }

    private static JsonElement Vector(JsonDocument document, int index) =>
        document.RootElement.GetProperty("vectors")[index];

    private static LicensePayload PayloadOf(JsonElement vector) => new()
    {
        Version = (byte)vector.GetProperty("version").GetUInt32(),
        SerialNumber = vector.GetProperty("serialNumber").GetUInt32(),
        IssuedAt = vector.GetProperty("issuedAt").GetUInt32(),
        ExpiresAt = vector.GetProperty("expiresAt").GetUInt32()
    };

    /// <summary>
    /// 코드에 담기는 13바이트의 배치. 이 검사가 깨지면 바이트 순서나 길이가 바뀐 것이고,
    /// 그 순간 기존에 발급한 모든 코드가 다른 뜻으로 읽힌다.
    /// </summary>
    [Theory]
    [MemberData(nameof(VectorIndexes))]
    public void PayloadLayout_IsUnchanged(int index)
    {
        using var document = LoadVectors();
        var vector = Vector(document, index);

        Assert.Equal(
            vector.GetProperty("payloadBytesHex").GetString(),
            Convert.ToHexString(PayloadOf(vector).ToBytes()));
    }

    /// <summary>
    /// 코드 문자열에서 내용을 되읽는 규칙(Base32 알파벳, 접두사, 자르는 위치).
    /// 여기가 깨지면 코드가 아예 해독되지 않거나 엉뚱한 번호로 읽힌다.
    /// </summary>
    [Theory]
    [MemberData(nameof(VectorIndexes))]
    public void Code_DecodesBackToTheSamePayload(int index)
    {
        using var document = LoadVectors();
        var vector = Vector(document, index);
        var expected = PayloadOf(vector);

        Assert.True(
            LicenseCodeCodec.TryDecode(vector.GetProperty("code").GetString()!, out var decoded, out var signature),
            "고정 벡터의 코드를 해독하지 못했다.");

        Assert.Equal(expected.Version, decoded.Version);
        Assert.Equal(expected.SerialNumber, decoded.SerialNumber);
        Assert.Equal(expected.IssuedAt, decoded.IssuedAt);
        Assert.Equal(expected.ExpiresAt, decoded.ExpiresAt);

        Assert.Equal(vector.GetProperty("signatureBase64").GetString(), Convert.ToBase64String(signature));
    }

    /// <summary>
    /// 서명 검증. 서명 대상 바이트나 서명 형식(IEEE P1363)이 바뀌면 여기서 걸린다.
    /// 운영 검증부(LicenseService.IsSignatureValid)와 같은 호출을 쓴다.
    /// </summary>
    [Theory]
    [MemberData(nameof(VectorIndexes))]
    public void Code_VerifiesAgainstTheTestPublicKey(int index)
    {
        using var document = LoadVectors();
        var vector = Vector(document, index);

        Assert.True(LicenseCodeCodec.TryDecode(
            vector.GetProperty("code").GetString()!, out var payload, out var signature));

        Assert.True(Verify(document, payload, signature), "고정 벡터의 서명이 검증되지 않았다.");
    }

    /// <summary>
    /// 한 글자만 바꿔도 검증이 실패해야 한다. 이게 없으면 위의 검사들은
    /// "무엇이든 통과시키는 검증기"에서도 그대로 통과한다.
    /// </summary>
    [Fact]
    public void TamperedCode_DoesNotVerify()
    {
        using var document = LoadVectors();
        var code = Vector(document, 0).GetProperty("code").GetString()!;

        // 서명 부분의 글자 하나를 다른 글자로 바꾼다(Base32 알파벳 안에서).
        var index = code.Length - 3;
        var tampered = code[..index] + (code[index] == 'A' ? 'B' : 'A') + code[(index + 1)..];

        Assert.NotEqual(code, tampered);

        if (LicenseCodeCodec.TryDecode(tampered, out var payload, out var signature))
        {
            Assert.False(Verify(document, payload, signature), "위조된 코드가 검증을 통과했다.");
        }
    }

    /// <summary>운영 검증부와 같은 방식. 형식을 명시하지 않으면 DER로 해석돼 전부 실패한다.</summary>
    private static bool Verify(JsonDocument document, LicensePayload payload, byte[] signature)
    {
        using var ecdsa = ECDsa.Create();
        ecdsa.ImportSubjectPublicKeyInfo(
            Convert.FromBase64String(document.RootElement.GetProperty("testPublicKeySpki").GetString()!), out _);

        return ecdsa.VerifyData(
            LicenseCodeCodec.GetSignableBytes(payload),
            signature,
            HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
    }

    /// <summary>
    /// 발급 쪽에서 새로 서명한 코드도 같은 규칙으로 검증돼야 한다.
    /// 위 벡터들이 "예전에 만든 것"이라면 이건 "지금 만드는 것"을 본다.
    /// </summary>
    [Fact]
    public void FreshlySignedCode_RoundTrips()
    {
        using var document = LoadVectors();

        using var signer = ECDsa.Create();
        signer.ImportPkcs8PrivateKey(
            Convert.FromBase64String(document.RootElement.GetProperty("testPrivateKeyPkcs8").GetString()!), out _);

        var payload = new LicensePayload
        {
            Version = LicensePayload.CurrentVersion,
            SerialNumber = 7,
            IssuedAt = 1_760_000_000,
            ExpiresAt = 0
        };

        var signature = signer.SignData(
            LicenseCodeCodec.GetSignableBytes(payload),
            HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation);

        Assert.Equal(LicenseCodeCodec.SignatureByteLength, signature.Length);

        var code = LicenseCodeCodec.Encode(payload, signature);

        Assert.True(LicenseCodeCodec.TryDecode(code, out var decoded, out var decodedSignature));
        Assert.Equal(payload.SerialNumber, decoded.SerialNumber);
        Assert.True(Verify(document, decoded, decodedSignature));
    }
}
