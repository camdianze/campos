using PharmaPOS.Application.Receipts;
using PharmaPOS.Application.Settings;

namespace PharmaPOS.Tests.Receipts;

/// <summary>
/// 저장 전 검증. 여기서 못 걸린 값은 전부 종이에서 드러난다.
/// </summary>
public class ReceiptSettingsValidatorTests
{
    private static ReceiptSettings Valid() => new()
    {
        ShopNameKm = "ឱសថស្ថាន",
        ShopNameEn = "Sample Pharmacy",
        ReceiptPrefix = "INV",
        ExchangeRate = 4100m,
        VatRate = 10m
    };

    [Fact]
    public void Validate_AcceptsACompleteSetting()
    {
        Assert.Empty(ReceiptSettingsValidator.Validate(Valid()));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_RejectsMissingShopNames(string blank)
    {
        var settings = Valid();
        settings.ShopNameKm = blank;
        settings.ShopNameEn = blank;

        var errors = ReceiptSettingsValidator.Validate(settings);

        Assert.Contains(AppSettingKeys.ShopNameKm, errors.Keys);
        Assert.Contains(AppSettingKeys.ShopNameEn, errors.Keys);
    }

    [Theory]
    [InlineData("INV")]
    [InlineData("SP")]
    [InlineData("SP01")]
    [InlineData("ABCDE")]
    public void Validate_AcceptsPrefixesOfTwoToFiveUppercaseCharacters(string prefix)
    {
        var settings = Valid();
        settings.ReceiptPrefix = prefix;

        Assert.DoesNotContain(
            AppSettingKeys.ReceiptPrefix, ReceiptSettingsValidator.Validate(settings).Keys);
    }

    [Theory]
    [InlineData("A")]          // 너무 짧다
    [InlineData("ABCDEF")]     // 너무 길다
    [InlineData("inv")]        // 소문자
    [InlineData("SP-1")]       // 기호
    [InlineData("ឱស")]         // 라틴 문자가 아니다
    public void Validate_RejectsMalformedPrefixes(string prefix)
    {
        var settings = Valid();
        settings.ReceiptPrefix = prefix;

        var errors = ReceiptSettingsValidator.Validate(settings);

        Assert.Contains(AppSettingKeys.ReceiptPrefix, errors.Keys);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(0.5)]
    public void Validate_RejectsExchangeRatesBelowOne(double rate)
    {
        var settings = Valid();
        settings.ExchangeRate = (decimal)rate;

        Assert.Contains(AppSettingKeys.CurrencyRate, ReceiptSettingsValidator.Validate(settings).Keys);
    }

    /// <summary>
    /// 리엘 표기를 꺼 두었어도 환율은 검사한다. 꺼 둔 채 0을 저장해 두면
    /// 나중에 켜는 순간 리엘 금액이 0으로 인쇄된다.
    /// </summary>
    [Fact]
    public void Validate_ChecksTheRateEvenWhenRielIsHidden()
    {
        var settings = Valid();
        settings.ShowRiel = false;
        settings.ExchangeRate = 0m;

        Assert.Contains(AppSettingKeys.CurrencyRate, ReceiptSettingsValidator.Validate(settings).Keys);
    }

    [Fact]
    public void Validate_RequiresTheTaxNumberWhenVatIsOn()
    {
        var settings = Valid();
        settings.VatEnabled = true;
        settings.VatTin = "   ";

        Assert.Contains(AppSettingKeys.VatTin, ReceiptSettingsValidator.Validate(settings).Keys);
    }

    [Fact]
    public void Validate_DoesNotRequireTheTaxNumberWhenVatIsOff()
    {
        var settings = Valid();
        settings.VatEnabled = false;
        settings.VatTin = string.Empty;

        Assert.DoesNotContain(AppSettingKeys.VatTin, ReceiptSettingsValidator.Validate(settings).Keys);
    }

    /// <summary>
    /// 문구는 무엇이 잘못됐는지와 어떻게 고치는지를 담아야 한다.
    /// "오류가 발생했습니다" 같은 문구는 계산대에서 아무 도움이 되지 않는다.
    /// </summary>
    [Fact]
    public void Validate_ExplainsHowToFixThePrefix()
    {
        var settings = Valid();
        settings.ReceiptPrefix = "x";

        var message = ReceiptSettingsValidator.Validate(settings)[AppSettingKeys.ReceiptPrefix];

        Assert.Contains("2 to 5", message);
        Assert.Contains("uppercase", message);
    }
}
