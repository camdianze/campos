namespace PharmaPOS.Application.Receipts;

/// <summary>
/// 영수증에 어느 언어를 찍을지.
///
/// KhmerAndEnglish는 크메르어 본문 아래에 영어 보조 라벨을 덧붙인다.
/// Khmer/English는 해당 언어만 찍는다.
/// </summary>
public enum ReceiptPrintLanguage
{
    KhmerAndEnglish,
    Khmer,
    English
}

/// <summary>감열지 폭. 프린터 사양과 다르면 인쇄가 잘린다.</summary>
public enum ReceiptPaperWidth
{
    Mm80,
    Mm58
}

/// <summary>영수증 일련번호를 언제 0001로 되돌릴지.</summary>
public enum ReceiptNumberResetCycle
{
    Daily,
    Monthly,
    Never
}

/// <summary>
/// 설정값의 저장 표기. 열거형 이름을 그대로 쓰지 않는 이유는 명세가 키별 값을
/// km_en / 80 / daily 처럼 고정해 두었기 때문이다. 한번 저장된 값은 그대로 남으므로
/// 이 표기는 바꾸지 않는다.
/// </summary>
public static class ReceiptSettingCodes
{
    public const string LanguageKhmerAndEnglish = "km_en";
    public const string LanguageKhmer = "km";
    public const string LanguageEnglish = "en";

    public const string Width80 = "80";
    public const string Width58 = "58";

    public const string CycleDaily = "daily";
    public const string CycleMonthly = "monthly";
    public const string CycleNever = "never";

    public static string ToCode(ReceiptPrintLanguage value) => value switch
    {
        ReceiptPrintLanguage.Khmer => LanguageKhmer,
        ReceiptPrintLanguage.English => LanguageEnglish,
        _ => LanguageKhmerAndEnglish
    };

    public static string ToCode(ReceiptPaperWidth value) =>
        value == ReceiptPaperWidth.Mm58 ? Width58 : Width80;

    public static string ToCode(ReceiptNumberResetCycle value) => value switch
    {
        ReceiptNumberResetCycle.Monthly => CycleMonthly,
        ReceiptNumberResetCycle.Never => CycleNever,
        _ => CycleDaily
    };

    /// <summary>
    /// 알 수 없는 값이면 fallback을 돌려준다. 예외를 던지지 않는다 —
    /// 설정 하나가 깨졌다고 영수증이 안 나오면 계산대가 멈춘다.
    /// </summary>
    public static ReceiptPrintLanguage ParseLanguage(string? code, ReceiptPrintLanguage fallback) =>
        code?.Trim().ToLowerInvariant() switch
        {
            LanguageKhmerAndEnglish => ReceiptPrintLanguage.KhmerAndEnglish,
            LanguageKhmer => ReceiptPrintLanguage.Khmer,
            LanguageEnglish => ReceiptPrintLanguage.English,
            _ => fallback
        };

    public static ReceiptPaperWidth ParseWidth(string? code, ReceiptPaperWidth fallback) =>
        code?.Trim() switch
        {
            Width80 => ReceiptPaperWidth.Mm80,
            Width58 => ReceiptPaperWidth.Mm58,
            _ => fallback
        };

    public static ReceiptNumberResetCycle ParseResetCycle(string? code, ReceiptNumberResetCycle fallback) =>
        code?.Trim().ToLowerInvariant() switch
        {
            CycleDaily => ReceiptNumberResetCycle.Daily,
            CycleMonthly => ReceiptNumberResetCycle.Monthly,
            CycleNever => ReceiptNumberResetCycle.Never,
            _ => fallback
        };
}
