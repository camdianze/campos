using PharmaPOS.Application.Repositories;
using PharmaPOS.Application.Security;
using PharmaPOS.Application.Settings;
using PharmaPOS.Application.Sync;

namespace PharmaPOS.Tests.Sync;

/// <summary>
/// 서버 연결 설정. 여기서 틀리면 화면에는 아무 이상이 없고 전송만 거부된다.
/// </summary>
public class SyncSettingsTests
{
    // ── 붙여넣은 주소 ────────────────────────────────────────────────────

    /// <summary>
    /// Supabase 설정 화면이 보여주는 주소에는 <c>/rest/v1/</c>이 붙어 있고, 사람은
    /// 거기 있는 것을 복사한다. 떼어 내지 않으면 POST가 <c>/rest/v1/functions/v1/sync</c>로
    /// 가서 404가 나는데, 화면의 주소는 콘솔에 적힌 것과 글자 하나 다르지 않다.
    /// </summary>
    [Theory]
    [InlineData("https://abc.supabase.co/rest/v1/")]
    [InlineData("https://abc.supabase.co/rest/v1")]
    [InlineData("https://abc.supabase.co/")]
    [InlineData("  https://abc.supabase.co  ")]
    [InlineData("https://abc.supabase.co/functions/v1")]
    [InlineData("https://abc.supabase.co/functions/v1/sync")]
    public void AnyWayThePersonPastedItReachesTheSameEndpoint(string pasted)
    {
        var settings = new SyncSettings
        {
            ServerUrl = SyncSettings.NormalizeUrl(pasted),
            PublishableKey = "sb_publishable_x",
            Token = "abc",
        };

        Assert.Equal("https://abc.supabase.co/functions/v1/sync", settings.Endpoint);
    }

    [Fact]
    public void ASettingWithAnyFieldMissingIsNotConfigured()
    {
        Assert.False(new SyncSettings { PublishableKey = "k", Token = "t" }.IsConfigured);
        Assert.False(new SyncSettings { ServerUrl = "u", Token = "t" }.IsConfigured);
        Assert.False(new SyncSettings { ServerUrl = "u", PublishableKey = "k" }.IsConfigured);
        Assert.True(new SyncSettings { ServerUrl = "u", PublishableKey = "k", Token = "t" }.IsConfigured);
    }

    // ── 저장 ─────────────────────────────────────────────────────────────

    /// <summary>
    /// 비밀 키는 RLS를 통째로 우회하므로 약국 PC에 내려와서는 안 된다. 토큰 칸에
    /// 잘못 붙여넣어도 저장은 되고, 전송이 거부될 뿐이라 원인이 드러나지 않는다.
    /// 두 칸 모두에서 막는다.
    /// </summary>
    [Theory]
    [InlineData("sb_secret_abc", "sb_publishable_x")]
    [InlineData("good-token", "sb_secret_abc")]
    public async Task TheServersSecretKeyIsRefusedInEitherField(string token, string key)
    {
        var result = await NewService().SaveAsync("https://abc.supabase.co", key, token);

        Assert.False(result.IsSuccess);
        Assert.Contains("secret key", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>http로 보내면 토큰이 평문으로 흐른다.</summary>
    [Fact]
    public async Task PlainHttpIsRefused()
    {
        var result = await NewService().SaveAsync("http://abc.supabase.co", "k", "t");

        Assert.False(result.IsSuccess);
        Assert.Contains("https", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("", "k", "t")]
    [InlineData("https://abc.supabase.co", "", "t")]
    [InlineData("https://abc.supabase.co", "k", "")]
    public async Task EveryFieldIsRequired(string url, string key, string token)
    {
        Assert.False((await NewService().SaveAsync(url, key, token)).IsSuccess);
    }

    /// <summary>
    /// 토큰만 암호화되어 들어간다. 주소와 공개 키는 모든 약국이 같고 앱에 실려
    /// 나가는 값이라 평문이어도 잃을 것이 없지만, 토큰이 평문으로 남으면 복사된
    /// 데이터베이스가 다른 PC에서 그 약국인 척 쓸 수 있다.
    /// </summary>
    [Fact]
    public async Task OnlyTheTokenIsStoredEncrypted()
    {
        var store = new FakeSettings();
        var service = new SyncSettingsService(store, new ReversingProtector());

        var result = await service.SaveAsync(
            "https://abc.supabase.co/rest/v1/", "sb_publishable_x", "SECRETTOKEN");

        Assert.True(result.IsSuccess);
        Assert.Equal("https://abc.supabase.co", store.Values[AppSettingKeys.SyncServerUrl]);
        Assert.Equal("sb_publishable_x", store.Values[AppSettingKeys.SyncPublishableKey]);
        Assert.DoesNotContain("SECRETTOKEN", store.Values[AppSettingKeys.SyncToken], StringComparison.Ordinal);

        // 그리고 다시 읽으면 원래 값으로 돌아와야 한다.
        Assert.Equal("SECRETTOKEN", (await service.LoadAsync()).Token);
    }

    /// <summary>
    /// 다른 PC로 데이터베이스만 옮기면 DPAPI가 풀지 못한다. 그것은 고장이 아니라
    /// 의도한 결과이므로, 앱이 죽는 대신 "연결되지 않음"이 되어야 한다.
    /// </summary>
    [Fact]
    public async Task ATokenThisMachineCannotDecryptReadsAsNotConnected()
    {
        var store = new FakeSettings();
        store.Values[AppSettingKeys.SyncServerUrl] = "https://abc.supabase.co";
        store.Values[AppSettingKeys.SyncPublishableKey] = "k";
        store.Values[AppSettingKeys.SyncToken] = "written-on-another-pc";

        var settings = await new SyncSettingsService(store, new ThrowingProtector()).LoadAsync();

        Assert.Equal(string.Empty, settings.Token);
        Assert.False(settings.IsConfigured);
    }

    private static SyncSettingsService NewService() =>
        new(new FakeSettings(), new ReversingProtector());

    // ── 받침대 ───────────────────────────────────────────────────────────

    private sealed class FakeSettings : IAppSettingRepository
    {
        public Dictionary<string, string> Values { get; } = new();

        public Task<string?> GetAsync(string key) =>
            Task.FromResult(Values.TryGetValue(key, out var v) ? v : null);

        public Task<IReadOnlyDictionary<string, string>> GetManyAsync(IReadOnlyList<string> keys) =>
            Task.FromResult<IReadOnlyDictionary<string, string>>(
                keys.Where(Values.ContainsKey).ToDictionary(k => k, k => Values[k]));

        public Task SetAsync(string key, string value, string? valueType = null, string? updatedBy = null)
        {
            Values[key] = value;
            return Task.CompletedTask;
        }
    }

    /// <summary>DPAPI 대신 뒤집기. 암호화 자체가 아니라 "암호화를 거치는가"를 본다.</summary>
    private sealed class ReversingProtector : IRecoveryDataProtector
    {
        public string Protect(string plainText) => new(plainText.Reverse().ToArray());

        public string Unprotect(string encryptedText) => new(encryptedText.Reverse().ToArray());
    }

    private sealed class ThrowingProtector : IRecoveryDataProtector
    {
        public string Protect(string plainText) => plainText;

        public string Unprotect(string encryptedText) =>
            throw new System.Security.Cryptography.CryptographicException("wrong machine");
    }
}
