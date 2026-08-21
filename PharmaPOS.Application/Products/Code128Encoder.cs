namespace PharmaPOS.Application.Products;

/// <summary>
/// Code 128-B 인코더. 바코드 라벨의 막대 굵기 배열을 만든다.
///
/// 외부 패키지를 쓰지 않는 이유는 복약안내지가 ESC/POS를 피해 간 것과 같다 —
/// 이 하나를 위해 의존성을 늘리기보다, 표가 고정돼 있고 검산이 가능한 알고리즘이라 직접 둔다.
///
/// B형을 고른 이유: 내부 바코드가 "INT-00000146" 형식이고 낱개용은 뒤에 "-EA"가 붙는다.
/// 영문자와 하이픈이 섞여 있어 숫자 전용인 EAN-13/UPC로는 담을 수 없고,
/// 자릿수 고정도 맞지 않는다. B형은 ASCII 32~126을 모두 담는다.
///
/// 이 클래스가 Application에 있는 이유: 막대 굵기를 계산하는 일은 순수한 규칙이라
/// 화면 없이 검증할 수 있다. 그것을 실제로 그리는 일만 WPF 쪽에 둔다.
/// </summary>
public static class Code128Encoder
{
    /// <summary>
    /// 107개 심볼의 막대·공백 굵기표. 한 줄이 심볼 하나이고, 숫자는 모듈 수다.
    /// 막대부터 시작해 공백과 번갈아 읽는다. 정지 심볼(106)만 7자리다.
    /// 표준 표라 손대면 안 된다 — 한 자리만 틀려도 스캐너가 읽지 못한다.
    /// </summary>
    private static readonly string[] Patterns =
    [
        "212222", "222122", "222221", "121223", "121322", "131222", "122213", "122312",
        "132212", "221213", "221312", "231212", "112232", "122132", "122231", "113222",
        "123122", "123221", "223211", "221132", "221231", "213212", "223112", "312131",
        "311222", "321122", "321221", "312212", "322112", "322211", "212123", "212321",
        "232121", "111323", "131123", "131321", "112313", "132113", "132311", "211313",
        "231113", "231311", "112133", "112331", "132131", "113123", "113321", "133121",
        "313121", "211331", "231131", "213113", "213311", "213131", "311123", "311321",
        "331121", "312113", "312311", "332111", "314111", "221411", "431111", "111224",
        "111422", "121124", "121421", "141122", "141221", "112214", "112412", "122114",
        "122411", "142112", "142211", "241211", "221114", "413111", "241112", "134111",
        "111242", "121142", "121241", "114212", "124112", "124211", "411212", "421112",
        "421211", "212141", "214121", "412121", "111143", "111341", "131141", "114113",
        "114311", "411113", "411311", "113141", "114131", "311141", "411131", "211412",
        "211214", "211232", "2331112"
    ];

    /// <summary>B형 시작 심볼.</summary>
    public const int StartCodeB = 104;

    /// <summary>정지 심볼.</summary>
    public const int StopCode = 106;

    /// <summary>B형이 담을 수 있는 문자인지. ASCII 32(공백)~126(~).</summary>
    public static bool CanEncode(string? value) =>
        !string.IsNullOrEmpty(value) && value.All(c => c is >= ' ' and <= '~');

    /// <summary>
    /// 심볼 값 목록. 시작 · 자료 · 검사문자 · 정지 순서다.
    /// 막대를 그리기 전 단계라 따로 내준다 — 검사문자 계산을 화면 없이 검증할 수 있다.
    /// </summary>
    public static IReadOnlyList<int> ToSymbols(string value)
    {
        if (!CanEncode(value))
        {
            throw new ArgumentException(
                "Code 128-B can only encode ASCII 32 to 126.", nameof(value));
        }

        var symbols = new List<int> { StartCodeB };

        foreach (var c in value)
        {
            // B형의 심볼 값은 ASCII에서 32를 뺀 값이다.
            symbols.Add(c - 32);
        }

        symbols.Add(CalculateCheckSymbol(symbols));
        symbols.Add(StopCode);

        return symbols;
    }

    /// <summary>
    /// 검사문자. 시작 심볼에 각 자료 심볼을 자리번호(1부터)만큼 곱해 더하고 103으로 나눈 나머지다.
    /// 시작 심볼의 가중치는 1이 아니라 그 자신이다.
    /// </summary>
    private static int CalculateCheckSymbol(IReadOnlyList<int> startAndData)
    {
        long sum = startAndData[0];

        for (var i = 1; i < startAndData.Count; i++)
        {
            sum += (long)startAndData[i] * i;
        }

        return (int)(sum % 103);
    }

    /// <summary>
    /// 막대와 공백의 굵기를 차례로 늘어놓은 배열. 첫 값이 막대이고 그 뒤로 번갈아 간다.
    /// 값은 모듈 수(1~4)이며, 실제 길이는 그리는 쪽이 모듈 폭을 곱해 정한다.
    /// </summary>
    public static IReadOnlyList<int> ToModuleWidths(string value)
    {
        var widths = new List<int>();

        foreach (var symbol in ToSymbols(value))
        {
            foreach (var digit in Patterns[symbol])
            {
                widths.Add(digit - '0');
            }
        }

        return widths;
    }

    /// <summary>
    /// 바코드 전체가 차지하는 모듈 수. 용지 폭에 맞춰 모듈 폭을 정할 때 쓴다.
    /// 정지 심볼이 13모듈이라 자료 길이 n에 대해 11n + 35가 된다.
    /// </summary>
    public static int TotalModules(string value) => ToModuleWidths(value).Sum();
}
