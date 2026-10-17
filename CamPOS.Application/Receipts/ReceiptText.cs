using PharmaPOS.Application.Counselling;

namespace PharmaPOS.Application.Receipts;

/// <summary>
/// 영수증 한 문구를 어느 언어로 낼지 결정한다.
///
/// 현지어는 이 앱의 유일한 로케일 장치인 locales/{bcp47}.json 에서 온다.
/// 그 파일을 읽는 인터페이스 이름이 ICounsellingLocaleProvider인 것은 복약안내가
/// 먼저 쓰기 시작했기 때문이고, 영수증도 같은 파일·같은 검수 규칙을 그대로 따른다
/// (review_status가 approved가 아니면 현지어를 한 글자도 내보내지 않는다).
///
/// print.lang이 km인데 현지어를 못 쓰는 상황(파일 없음·미검수)에서는 영어로 떨어진다.
/// 번역이 아직 안 됐다고 백지가 나오면 영수증으로서 쓸모가 없다.
/// </summary>
public class ReceiptText
{
    private readonly CounsellingLocale _locale;

    public ReceiptPrintLanguage Language { get; }

    /// <summary>현지어를 실제로 찍을 수 있는지. 미검수 로케일이면 false다.</summary>
    public bool HasLocalLanguage { get; }

    public ReceiptText(ReceiptPrintLanguage language, CounsellingLocale locale)
    {
        Language = language;
        _locale = locale;

        HasLocalLanguage =
            language != ReceiptPrintLanguage.English &&
            locale.GetString(ReceiptStringKeys.LabelTotal) is not null;
    }

    /// <summary>
    /// 줄의 본문. 현지어를 쓸 수 있으면 현지어, 아니면 영어다.
    /// </summary>
    public string Primary(string key, params (string Name, string Value)[] values)
    {
        var local = HasLocalLanguage ? _locale.GetString(key) : null;

        return ReceiptStrings.Format(local ?? ReceiptStrings.English(key), values);
    }

    /// <summary>
    /// 본문 아래에 덧붙일 보조 라벨. km_en일 때만 영어가 돌아오고,
    /// 그 외에는 null이다(= 그 줄은 본문 하나로 끝난다).
    /// 현지어를 못 써서 본문이 이미 영어인 경우에도 null이다 — 같은 문구를 두 번 찍지 않는다.
    /// </summary>
    public string? Secondary(string key, params (string Name, string Value)[] values)
    {
        if (Language != ReceiptPrintLanguage.KhmerAndEnglish || !HasLocalLanguage)
        {
            return null;
        }

        if (_locale.GetString(key) is null)
        {
            // 본문이 이미 영어로 나갔다.
            return null;
        }

        return ReceiptStrings.Format(ReceiptStrings.English(key), values);
    }

    /// <summary>
    /// 설정에 저장된 크메르어/영어 두 벌 중 어느 것을 쓸지 고른다.
    /// 약국 이름·주소·맺음 문구처럼 번역이 로케일 파일이 아니라 설정에 들어 있는 값들이다.
    /// </summary>
    public string PrimaryOf(string khmer, string english)
    {
        if (Language == ReceiptPrintLanguage.English)
        {
            return english;
        }

        return string.IsNullOrWhiteSpace(khmer) ? english : khmer;
    }

    /// <summary>PrimaryOf가 크메르어를 골랐고 언어가 km_en일 때만 영어를 덧붙인다.</summary>
    public string? SecondaryOf(string khmer, string english)
    {
        if (Language != ReceiptPrintLanguage.KhmerAndEnglish)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(khmer) || string.IsNullOrWhiteSpace(english))
        {
            return null;
        }

        return english;
    }
}
