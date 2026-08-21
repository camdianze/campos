using System.Text.RegularExpressions;
using PharmaPOS.Application.Settings;

namespace PharmaPOS.Application.Receipts;

/// <summary>
/// 저장 전에 설정값을 검사한다.
///
/// 여기서 걸러야 하는 것들은 전부 "저장은 되는데 나중에 종이에서 드러나는" 문제들이다:
/// 약국 이름이 없으면 머리말이 빈 채로 인쇄되고, 환율이 0이면 리엘 금액이 0으로 찍히고,
/// 접두어가 없으면 영수증 번호를 발번할 수 없다.
///
/// 문구는 무엇이 잘못됐고 어떻게 고치는지를 함께 적는다. "오류가 발생했습니다" 류는
/// 계산대에서 아무 도움이 되지 않는다.
/// </summary>
public static class ReceiptSettingsValidator
{
    /// <summary>영문 대문자와 숫자 2~5자.</summary>
    private static readonly Regex PrefixPattern = new("^[A-Z0-9]{2,5}$", RegexOptions.Compiled);

    /// <summary>문제가 없으면 빈 사전을 돌려준다.</summary>
    public static IReadOnlyDictionary<string, string> Validate(ReceiptSettings settings)
    {
        var errors = new Dictionary<string, string>();

        if (string.IsNullOrWhiteSpace(settings.ShopNameKm))
        {
            errors[AppSettingKeys.ShopNameKm] =
                ReceiptStrings.English(ReceiptStringKeys.ErrorShopNameKmRequired);
        }

        if (string.IsNullOrWhiteSpace(settings.ShopNameEn))
        {
            errors[AppSettingKeys.ShopNameEn] =
                ReceiptStrings.English(ReceiptStringKeys.ErrorShopNameEnRequired);
        }

        var prefix = settings.ReceiptPrefix?.Trim() ?? string.Empty;

        if (string.IsNullOrEmpty(prefix))
        {
            errors[AppSettingKeys.ReceiptPrefix] =
                ReceiptStrings.English(ReceiptStringKeys.ErrorPrefixRequired);
        }
        else if (!PrefixPattern.IsMatch(prefix))
        {
            errors[AppSettingKeys.ReceiptPrefix] =
                ReceiptStrings.English(ReceiptStringKeys.ErrorPrefixFormat);
        }

        // 환율은 리엘 표기를 끈 상태에서도 검사한다. 꺼 둔 채 잘못된 값을 저장해 두면
        // 나중에 켜는 순간 틀린 금액이 인쇄된다.
        if (settings.ExchangeRate < 1m)
        {
            errors[AppSettingKeys.CurrencyRate] =
                ReceiptStrings.English(ReceiptStringKeys.ErrorRateRange);
        }

        if (settings.VatEnabled && string.IsNullOrWhiteSpace(settings.VatTin))
        {
            errors[AppSettingKeys.VatTin] =
                ReceiptStrings.English(ReceiptStringKeys.ErrorVatTinRequired);
        }

        if (settings.VatRate < 0m || settings.VatRate > 100m)
        {
            errors[AppSettingKeys.VatRate] =
                ReceiptStrings.English(ReceiptStringKeys.ErrorVatRateRange);
        }

        return errors;
    }
}
