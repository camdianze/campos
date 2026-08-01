using System.Globalization;
using System.Text;
using PharmaPOS.Domain.Enums;

namespace PharmaPOS.Application.Counselling;

/// <summary>
/// 복약안내 용지를 고정폭 텍스트로 그린다.
///
/// 설계상 지켜야 하는 것들:
///   - 용량/횟수/기간/투여시점은 반드시 공란이다. 시스템이 채우지 않는다.
///   - AWaRe 분류명은 번역하지 않는다. 로케일이 approved여도 예외 없다.
///   - 이모지를 쓰지 않는다. 감열 프린터는 흑백 단색이라 이모지 글리프가 없다.
///   - 적응증·진단·상호작용 정보는 넣지 않는다.
///
/// 폭에 대하여: 스펙 예시 그림은 40자 폭으로 그려져 있지만 명시된 제약은
/// "58mm 기준 32자/행"이다. 40자로 찍으면 58mm 감열지에서 잘리므로,
/// 제약 쪽을 따르고 구분선과 줄바꿈을 지정된 폭에 맞춘다.
/// </summary>
public static class CounsellingSheetRenderer
{
    public static CounsellingSheetDocument Render(CounsellingSheetRequest request)
    {
        var width = Math.Max(24, request.Width);
        var locale = request.Locale;
        var lines = new List<string>();

        AppendHeader(lines, request, locale, width);

        if (request.Format == CounsellingSheetFormat.Full)
        {
            AppendIdentification(lines, request, width);
        }

        AppendClassification(lines, request, width);

        lines.Add(new string('-', width));
        AppendPharmacistInstructions(lines, locale, width);

        lines.Add(new string('-', width));
        AppendImportantNotes(lines, locale, width);

        lines.Add(new string('-', width));

        if (request.Format == CounsellingSheetFormat.Full)
        {
            AppendSignatureAndQr(lines, request, locale, width);
        }

        AppendDisclaimer(lines, request, width);
        lines.Add(new string('=', width));

        return new CounsellingSheetDocument
        {
            Lines = lines,
            // 축약본에서는 QR 영역을 그리지 않으므로 코드도 만들지 않는다.
            QrUrl = request.Format == CounsellingSheetFormat.Full && !string.IsNullOrWhiteSpace(request.QrUrl)
                ? request.QrUrl
                : null,
            ProductName = request.ProductName
        };
    }

    private static void AppendHeader(
        List<string> lines, CounsellingSheetRequest request, CounsellingLocale locale, int width)
    {
        lines.Add(new string('=', width));
        AppendCentered(lines, "ANTIBIOTIC USE INFORMATION", width);

        var subtitle = locale.GetString(CounsellingStringKeys.SheetSubtitle);

        if (subtitle is not null)
        {
            AppendCentered(lines, subtitle, width);
        }

        lines.Add(new string('=', width));
    }

    private static void AppendIdentification(
        List<string> lines, CounsellingSheetRequest request, int width)
    {
        AppendField(lines, "Medicine", request.ProductName, width);

        if (!string.IsNullOrWhiteSpace(request.GenericName))
        {
            AppendField(lines, "Ingredient", request.GenericName!, width);
        }

        if (!string.IsNullOrWhiteSpace(request.AtcCode))
        {
            AppendField(lines, "ATC", request.AtcCode!, width);
        }
    }

    private static void AppendClassification(
        List<string> lines, CounsellingSheetRequest request, int width)
    {
        // 축약본은 상품 식별 줄을 생략하지만, 어느 약의 안내인지조차 없으면
        // 환자에게 건네는 종이로서 쓸모가 없어 상품명 한 줄은 남긴다.
        if (request.Format == CounsellingSheetFormat.Compact)
        {
            AppendField(lines, "Medicine", request.ProductName, width);
        }

        var group = request.AwareGroup;
        var value = $"[{DisplayLabel(group)}]  {Pattern(group)}";

        AppendField(lines, "WHO AWaRe", value, width);

        if (!string.IsNullOrWhiteSpace(request.SourceVersion))
        {
            var indent = new string(' ', FieldIndent);
            AppendWrapped(lines, indent, indent, $"({request.SourceVersion})", width);
        }

        // WATCH / RESERVE / NOT_RECOMMENDED는 처방 없이 나가서는 안 되는 계열이다.
        if (group != AwareGroup.Access)
        {
            AppendWrapped(lines, ">> ", "   ", "Prescription strongly recommended.", width);
        }
    }

    private static void AppendPharmacistInstructions(
        List<string> lines, CounsellingLocale locale, int width)
    {
        lines.Add("PHARMACIST INSTRUCTIONS");

        // 아래 공란들은 의도적으로 비워 둔다.
        // 용량·투여기간·투여시점을 시스템이 채우면 그 순간 처방 행위가 된다.
        AppendWrapped(lines, "", "  ", Bilingual("Dose", locale, CounsellingStringKeys.LabelDose), width);
        lines.Add("  ______ tablet(s)");

        AppendWrapped(lines, "", "  ", Bilingual("Frequency", locale, CounsellingStringKeys.LabelFrequency), width);
        lines.Add("  ______ time(s)/day");

        AppendWrapped(lines, "", "  ", Bilingual("Duration", locale, CounsellingStringKeys.LabelDuration), width);
        lines.Add("  ______ day(s)");

        AppendWrapped(lines, "", "  ", Bilingual("Take", locale, CounsellingStringKeys.LabelTake), width);
        AppendWrapped(lines, "  [ ] ", "      ", Bilingual("Before", locale, CounsellingStringKeys.TakeBefore), width);
        AppendWrapped(lines, "  [ ] ", "      ", Bilingual("After", locale, CounsellingStringKeys.TakeAfter), width);
        AppendWrapped(lines, "  [ ] ", "      ", Bilingual("Either", locale, CounsellingStringKeys.TakeEither), width);
    }

    private static void AppendImportantNotes(
        List<string> lines, CounsellingLocale locale, int width)
    {
        AppendWrapped(lines, "", "", Bilingual("IMPORTANT", locale, CounsellingStringKeys.SectionImportant), width);

        AppendNote(lines, locale, "Complete the full course", CounsellingStringKeys.Important1, width);
        AppendNote(lines, locale, "Do not stop early", CounsellingStringKeys.Important2, width);
        AppendNote(lines, locale, "Do not share antibiotics", CounsellingStringKeys.Important3, width);
        AppendNote(lines, locale, "Do not keep leftovers", CounsellingStringKeys.Important4, width);
        AppendNote(lines, locale, "See a doctor if side effects occur", CounsellingStringKeys.Important5, width);
    }

    /// <summary>
    /// 영어 줄을 먼저 찍고, 검수된 현지어가 있으면 그 아래에 들여써서 덧붙인다.
    /// 현지어가 없으면 영어 줄만 남는다 — 빈 줄이나 키 문자열이 찍히면 안 된다.
    /// </summary>
    private static void AppendNote(
        List<string> lines, CounsellingLocale locale, string english, string localeKey, int width)
    {
        AppendWrapped(lines, "[v] ", "    ", english, width);

        var localText = locale.GetString(localeKey);

        if (localText is not null)
        {
            AppendWrapped(lines, "    ", "    ", localText, width);
        }
    }

    private static void AppendSignatureAndQr(
        List<string> lines, CounsellingSheetRequest request, CounsellingLocale locale, int width)
    {
        lines.Add("Pharmacist : " + new string('_', Math.Max(4, width - 13)));
        lines.Add("Date       : " + new string('_', Math.Max(4, width - 13)));

        if (!string.IsNullOrWhiteSpace(request.QrUrl))
        {
            var caption = locale.GetString(CounsellingStringKeys.QrCaption);

            AppendWrapped(
                lines,
                "[QR] ", "     ",
                caption is null ? "More information" : $"{caption} / More information",
                width);
        }
    }

    private static void AppendDisclaimer(
        List<string> lines, CounsellingSheetRequest request, int width)
    {
        var source = string.IsNullOrWhiteSpace(request.SourceVersion)
            ? string.Empty
            : $"Source: {request.SourceVersion}. ";

        AppendWrapped(
            lines, "", "",
            source + "This sheet supports pharmacist counselling and does not replace medical diagnosis or prescription.",
            width);
    }

    /// <summary>
    /// 분류 표시. 이 문자열은 어떤 로케일에서도 번역하지 않는다 —
    /// 번역하는 순간 국가 간 지표 집계가 불가능해진다.
    /// </summary>
    private static string DisplayLabel(AwareGroup group) => group switch
    {
        AwareGroup.Access => "ACCESS",
        AwareGroup.Watch => "WATCH",
        AwareGroup.Reserve => "RESERVE",
        AwareGroup.NotRecommended => "NOT RECOMMENDED",
        _ => "UNKNOWN"
    };

    /// <summary>
    /// 위험도를 한눈에 보이게 하는 도형 표시.
    /// 이모지가 아니라 도형 문자다 — 감열 프린터에 이모지를 보내면 '?'로 찍힌다.
    /// </summary>
    private static string Pattern(AwareGroup group) => group switch
    {
        AwareGroup.Access => "■□□",
        AwareGroup.Watch => "■■□",
        AwareGroup.Reserve => "■■■",
        AwareGroup.NotRecommended => "▨▨▨",
        _ => string.Empty
    };

    /// <summary>"English / 현지어" 형태. 현지어가 없으면 영어만 돌려준다.</summary>
    private static string Bilingual(string english, CounsellingLocale locale, string localeKey)
    {
        var localText = locale.GetString(localeKey);
        return localText is null ? english : $"{english} / {localText}";
    }

    /// <summary>"Medicine   : 값" 형태의 라벨 줄에서 값이 시작하는 칸.</summary>
    private const int FieldIndent = 13;

    private static void AppendField(List<string> lines, string label, string value, int width)
    {
        var paddedLabel = label.Length >= FieldIndent - 2
            ? label
            : label.PadRight(FieldIndent - 2);

        var firstPrefix = $"{paddedLabel}: ";

        // 한 줄에 들어가면 그대로 넣는다. 줄바꿈 처리를 거치면 값 안의
        // 정렬용 연속 공백("[WATCH]  ■■□")이 한 칸으로 뭉개지기 때문이다.
        if (TextLength(firstPrefix + value) <= width)
        {
            lines.Add(firstPrefix + value);
            return;
        }

        AppendWrapped(lines, firstPrefix, new string(' ', FieldIndent), value, width);
    }

    private static void AppendCentered(List<string> lines, string text, int width)
    {
        if (TextLength(text) >= width)
        {
            AppendWrapped(lines, "", "", text, width);
            return;
        }

        var padding = (width - TextLength(text)) / 2;
        lines.Add(new string(' ', padding) + text);
    }

    /// <summary>
    /// 지정한 폭에 맞춰 줄을 나눈다.
    ///
    /// 접두사(라벨, 체크박스 등)를 본문과 분리해서 받는 이유는,
    /// 줄바꿈이 단어 단위로 이뤄지면서 "Medicine   : " 같은 정렬용 공백이
    /// 하나로 뭉개지는 것을 막기 위해서다. 접두사는 손대지 않고 그대로 붙인다.
    ///
    /// 공백이 없는 문자열(크메르어 등)은 한 덩어리로 들어오므로 글자 수로 끊되,
    /// 자소 결합이 깨지지 않도록 텍스트 요소(grapheme cluster) 단위로 자른다.
    /// </summary>
    private static void AppendWrapped(
        List<string> lines, string firstPrefix, string hangingPrefix, string text, int width)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            if (!string.IsNullOrWhiteSpace(firstPrefix))
            {
                lines.Add(firstPrefix.TrimEnd());
            }

            return;
        }

        var prefix = firstPrefix;
        var current = new StringBuilder();

        int Available() => Math.Max(1, width - TextLength(prefix));

        void Flush()
        {
            if (current.Length == 0)
            {
                return;
            }

            lines.Add(prefix + current);
            current.Clear();
            prefix = hangingPrefix;
        }

        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var remaining = word;

            // 한 단어가 폭보다 길면 통째로 넘길 수 없으므로 잘라 낸다.
            while (TextLength(remaining) > Available())
            {
                Flush();

                var take = Available();
                lines.Add(prefix + SubstringByTextElements(remaining, 0, take));
                remaining = SubstringByTextElements(remaining, take);
                prefix = hangingPrefix;
            }

            if (current.Length == 0)
            {
                current.Append(remaining);
            }
            else if (TextLength(current.ToString()) + 1 + TextLength(remaining) <= Available())
            {
                current.Append(' ').Append(remaining);
            }
            else
            {
                Flush();
                current.Append(remaining);
            }
        }

        Flush();
    }

    private static int TextLength(string text) => new StringInfo(text).LengthInTextElements;

    private static string SubstringByTextElements(string text, int start, int length)
        => new StringInfo(text).SubstringByTextElements(start, length);

    private static string SubstringByTextElements(string text, int start)
    {
        var info = new StringInfo(text);
        return start >= info.LengthInTextElements
            ? string.Empty
            : info.SubstringByTextElements(start);
    }
}
