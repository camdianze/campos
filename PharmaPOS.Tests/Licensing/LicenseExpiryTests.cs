using System.Security.Cryptography;
using System.Text.Json;
using PharmaPOS.Application.Licensing;

namespace PharmaPOS.Tests.Licensing;

/// <summary>
/// 기한 판정.
///
/// 이 테스트가 있는 이유: 한동안 만료일이 <b>아무 일도 하지 않는 값</b>이었다.
/// 코드를 입력하는 순간에만 기한을 봤고, 그 뒤로는 <c>license.dat</c>이 열리는지만
/// 확인했기 때문에 기한이 지난 코드로도 앱이 계속 열렸다. 발급 쪽은 날짜를 새겨
/// 내보내고 대장에도 남기는데 제품은 그것을 지키지 않는 상태였고, 아무도
/// 그 사실을 알 수 없었다 — 증상이 "아무 일도 안 일어남"이라서다.
///
/// 서명 검증은 시험용 키로 한다. 운영 개인키는 저장소에 없어 테스트가 진짜 코드를
/// 만들 수 없고, 그러면 이 규칙을 고정할 방법이 없다.
/// </summary>
public class LicenseExpiryTests
{
    private sealed class FakeStore : ILicenseActivationStore
    {
        public string? Code { get; set; }
        public string? Saved { get; private set; }

        public string? ReadActivatedCode() => Code;

        public void SaveActivation(string licenseCode)
        {
            Saved = licenseCode;
            Code = licenseCode;
        }
    }

    private static JsonDocument LoadVectors() =>
        JsonDocument.Parse(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "license-vectors.json")));

    private static string TestPublicKey()
    {
        using var document = LoadVectors();
        return document.RootElement.GetProperty("testPublicKeySpki").GetString()!;
    }

    /// <summary>시험용 키로 서명한 코드 한 장. expiresAt이 0이면 영구다.</summary>
    private static string SignCode(uint expiresAt, uint serialNumber = 42)
    {
        using var document = LoadVectors();

        using var signer = ECDsa.Create();
        signer.ImportPkcs8PrivateKey(
            Convert.FromBase64String(document.RootElement.GetProperty("testPrivateKeyPkcs8").GetString()!),
            out _);

        var payload = new LicensePayload
        {
            Version = LicensePayload.CurrentVersion,
            SerialNumber = serialNumber,
            IssuedAt = 1_700_000_000,
            ExpiresAt = expiresAt
        };

        var signature = signer.SignData(
            LicenseCodeCodec.GetSignableBytes(payload),
            HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation);

        return LicenseCodeCodec.Encode(payload, signature);
    }

    private static uint InDays(int days) =>
        (uint)DateTimeOffset.UtcNow.AddDays(days).ToUnixTimeSeconds();

    private static LicenseService ServiceWith(FakeStore store) =>
        new(store, TestPublicKey());

    // ── ① 시작할 때 다시 검증한다 ────────────────────────────────────────

    /// <summary>
    /// 이 파일의 핵심. 기한이 지난 코드가 저장돼 있으면 앱이 열려서는 안 된다.
    /// 예전에는 여기서 그대로 통과했다.
    /// </summary>
    [Fact]
    public void StoredCode_PastItsExpiry_CannotRun()
    {
        var store = new FakeStore { Code = SignCode(InDays(-1)) };

        var status = ServiceWith(store).GetStatus();

        Assert.True(status.IsExpired);
        Assert.False(status.CanRun);
    }

    [Fact]
    public void StoredCode_WithinItsTerm_CanRun()
    {
        var store = new FakeStore { Code = SignCode(InDays(10)) };

        var status = ServiceWith(store).GetStatus();

        Assert.True(status.CanRun);
        Assert.False(status.IsExpired);
        Assert.Equal(10, status.DaysRemaining);
    }

    /// <summary>expiresAt 0은 영구다. 기한을 세지 않으며 경고도 뜨지 않는다.</summary>
    [Fact]
    public void PerpetualCode_NeverExpiresAndNeverWarns()
    {
        var store = new FakeStore { Code = SignCode(0) };

        var status = ServiceWith(store).GetStatus();

        Assert.True(status.CanRun);
        Assert.True(status.IsPerpetual);
        Assert.Null(status.DaysRemaining);
        Assert.False(status.ShouldWarn);
    }

    [Fact]
    public void NoStoredCode_IsNotActivated()
    {
        var status = ServiceWith(new FakeStore { Code = null }).GetStatus();

        Assert.Equal(LicenseState.NotActivated, status.State);
        Assert.False(status.CanRun);
    }

    /// <summary>
    /// 파일이 남아 있어도 내용을 믿을 수 없으면 활성화되지 않은 것으로 본다.
    /// 다른 PC에서 복사해 왔거나 손으로 고친 경우다.
    /// </summary>
    [Fact]
    public void StoredCode_ThatDoesNotVerify_IsNotActivated()
    {
        var store = new FakeStore { Code = "NOT-A-REAL-LICENSE-CODE" };

        Assert.Equal(LicenseState.NotActivated, ServiceWith(store).GetStatus().State);
    }

    /// <summary>
    /// 서명이 맞지 않는 코드도 마찬가지다. 운영 키로 서명된 코드를 시험용 키로 검증하면
    /// 통과해서는 안 된다 — 통과한다면 키 확인이 실제로는 이뤄지지 않고 있다는 뜻이다.
    /// </summary>
    [Fact]
    public void StoredCode_SignedWithAnotherKey_IsNotActivated()
    {
        using var document = LoadVectors();
        var productionLookingCode = document.RootElement
            .GetProperty("vectors")[0].GetProperty("code").GetString()!;

        using var other = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var otherPublicKey = Convert.ToBase64String(other.ExportSubjectPublicKeyInfo());

        var store = new FakeStore { Code = productionLookingCode };

        Assert.Equal(
            LicenseState.NotActivated,
            new LicenseService(store, otherPublicKey).GetStatus().State);
    }

    // ── ② 남은 기간 경고 ─────────────────────────────────────────────────

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, true)]
    [InlineData(30, true)]
    [InlineData(31, false)]
    [InlineData(200, false)]
    public void Warning_StartsThirtyDaysBeforeExpiry(int daysAhead, bool shouldWarn)
    {
        var store = new FakeStore { Code = SignCode(InDays(daysAhead)) };

        Assert.Equal(shouldWarn, ServiceWith(store).GetStatus().ShouldWarn);
    }

    /// <summary>배지와 관리자 화면이 같이 쓰는 한 줄. 날짜와 남은 날이 함께 들어간다.</summary>
    [Fact]
    public void Summary_CarriesBothTheDaysLeftAndTheDate()
    {
        var store = new FakeStore { Code = SignCode(InDays(12)) };
        var status = ServiceWith(store).GetStatus();

        Assert.Contains("12 days left", status.Summary);
        Assert.Contains(status.ExpiryDisplay, status.Summary);
    }

    [Fact]
    public void Summary_OfAnExpiredLicense_SaysWhenItExpired()
    {
        var store = new FakeStore { Code = SignCode(InDays(-3)) };
        var status = ServiceWith(store).GetStatus();

        Assert.StartsWith("Expired on", status.Summary);
        Assert.Equal(-3, status.DaysRemaining);
    }

    // ── ③ 연장 ───────────────────────────────────────────────────────────

    [Fact]
    public void Renewal_ReplacesTheStoredCodeAndExtendsTheTerm()
    {
        var store = new FakeStore { Code = SignCode(InDays(5)) };
        var service = ServiceWith(store);

        var renewal = SignCode(InDays(400));
        var result = service.Activate(renewal);

        Assert.True(result.IsSuccess, result.Message);
        Assert.Equal(renewal, store.Saved);
        Assert.Equal(400, service.GetStatus().DaysRemaining);
    }

    /// <summary>기한이 지난 뒤에도 연장 코드로 다시 열린다. 데이터는 건드리지 않는다.</summary>
    [Fact]
    public void Renewal_AfterExpiry_OpensTheAppAgain()
    {
        var store = new FakeStore { Code = SignCode(InDays(-10)) };
        var service = ServiceWith(store);

        Assert.False(service.GetStatus().CanRun);

        Assert.True(service.Activate(SignCode(InDays(365))).IsSuccess);
        Assert.True(service.GetStatus().CanRun);
    }

    /// <summary>이미 만료된 코드는 넣을 수 없다. 넣어 봐야 그 자리에서 잠긴다.</summary>
    [Fact]
    public void AlreadyExpiredCode_IsRefusedAndTheStoredOneIsKept()
    {
        var current = SignCode(InDays(20));
        var store = new FakeStore { Code = current };
        var service = ServiceWith(store);

        var result = service.Activate(SignCode(InDays(-1)));

        Assert.False(result.IsSuccess);
        Assert.Contains("expired on", result.Message);
        Assert.Null(store.Saved);
        Assert.Equal(current, store.Code);
    }

    /// <summary>
    /// 연장하려다 더 짧은 코드를 넣는 실수를 막는다. 덮어쓰고 나면 되돌릴 수 없고,
    /// 약국은 기한이 늘어난 줄 알고 있다가 예정보다 일찍 잠긴다.
    /// </summary>
    [Fact]
    public void CodeThatExpiresEarlierThanTheCurrentOne_IsRefused()
    {
        var current = SignCode(InDays(300));
        var store = new FakeStore { Code = current };
        var service = ServiceWith(store);

        var result = service.Activate(SignCode(InDays(30)));

        Assert.False(result.IsSuccess);
        Assert.Contains("expires earlier", result.Message);
        Assert.Equal(current, store.Code);
    }

    /// <summary>영구 코드는 기한이 짧아지는 경우가 아니므로 언제든 들어간다.</summary>
    [Fact]
    public void PerpetualCode_ReplacesATermLicense()
    {
        var store = new FakeStore { Code = SignCode(InDays(30)) };
        var service = ServiceWith(store);

        Assert.True(service.Activate(SignCode(0)).IsSuccess);
        Assert.True(service.GetStatus().IsPerpetual);
    }

    [Fact]
    public void EmptyCode_IsRefusedWithoutTouchingTheStoredOne()
    {
        var current = SignCode(InDays(20));
        var store = new FakeStore { Code = current };

        var result = ServiceWith(store).Activate("   ");

        Assert.False(result.IsSuccess);
        Assert.Null(store.Saved);
    }
}
