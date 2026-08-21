using System.Globalization;
using System.Printing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;

namespace Lightweight_Digital_Inventory_Management___POS_System.Services;

/// <summary>
/// 줄글을 감열지에 찍는 공통 경로. 복약안내 용지와 판매 영수증이 함께 쓴다.
///
/// ESC/POS 바이트를 직접 쏘지 않고 Windows 인쇄 파이프라인(FixedDocument)을 거치는 이유:
///   - 검증할 프린터 하드웨어가 없다. 기종별 명령 차이를 추측으로 구현하면 현장에서 그대로 재작업이 된다.
///   - 크메르어 같은 복합 문자의 자소 결합이 WPF 텍스트 스택에서 처리된다.
///     ESC/POS 텍스트 모드에서 자소가 분해되는 문제를 피할 수 있다.
///
/// 인쇄 대화상자는 띄우지 않는다. 판매마다 창이 뜨면 계산대가 멈춘다.
/// </summary>
public static class ThermalTextPrinter
{
    /// <summary>
    /// 고정폭 글꼴 + 대체 글꼴 목록. 앞 글꼴에 없는 글자는 뒤 글꼴에서 찾는다.
    /// 크메르 문자와 도형 문자(■□▨)가 두부(tofu)로 찍히지 않게 하기 위한 것이다.
    /// </summary>
    public const string FontFamilyList =
        "Consolas, Courier New, Noto Sans Khmer, Khmer UI, Leelawadee UI, Malgun Gothic, Segoe UI Symbol";

    /// <summary>58mm 감열지의 인쇄 가능 폭 근사치 (1/96인치 단위). 드라이버 정보를 못 얻을 때만 쓴다.</summary>
    private const double FallbackPaperWidth = 200;

    private const double FallbackPaperHeight = 1000;

    private const double Margin = 6;

    /// <summary>라틴 문자만 있는 문서의 줄 간격 배수.</summary>
    public const double DefaultLineHeightFactor = 1.3;

    /// <summary>
    /// 크메르어처럼 자소가 위아래로 쌓이는 문자를 담은 문서의 줄 간격 배수.
    /// 1.3으로 찍으면 위 첨자와 아래 첨자가 앞뒤 줄에 닿아 잘려 보인다.
    /// </summary>
    public const double ComplexScriptLineHeightFactor = 1.75;

    /// <summary>
    /// 기본 프린터로 보낸다.
    ///
    /// 예외를 밖으로 내보내지 않는다 — 부르는 쪽은 전부 "판매가 이미 끝난 뒤"라,
    /// 프린터 문제로 예외가 올라가면 이미 커밋된 거래 흐름이 깨진다.
    /// 프린터가 없든 드라이버가 죽었든 용지가 없든 false로 돌아온다.
    /// </summary>
    public static bool TryPrint(
        IReadOnlyList<string> lines,
        string jobName,
        double lineHeightFactor = DefaultLineHeightFactor) =>
        TryPrint((width, height) => BuildFixedDocument(lines, width, height, lineHeightFactor), jobName);

    /// <summary>
    /// 문서를 직접 그려 보낼 때 쓴다 (바코드 라벨처럼 글자만으로는 안 되는 경우).
    /// 프린터를 잡고 용지 크기를 알아내고 실패를 삼키는 일은 여기가 맡고,
    /// 무엇을 그릴지는 buildDocument가 정한다.
    /// </summary>
    /// <param name="buildDocument">인쇄 가능한 폭·높이를 받아 문서를 만든다.</param>
    public static bool TryPrint(Func<double, double, FixedDocument> buildDocument, string jobName)
    {
        try
        {
            var printDialog = new PrintDialog();

            if (printDialog.PrintQueue is null)
            {
                return false;
            }

            var (pageWidth, pageHeight) = GetPageSize(printDialog);

            printDialog.PrintDocument(
                buildDocument(pageWidth, pageHeight).DocumentPaginator, jobName);

            return true;
        }
        catch (Exception)
        {
            // 프린터 연결 해제, 드라이버 오류, 용지 없음 — 무엇이든 여기로 온다.
            return false;
        }
    }

    /// <summary>
    /// 드라이버가 알려주는 인쇄 가능 영역을 쓴다.
    /// 58mm/80mm를 코드에 박아두지 않는 이유는, 실제 용지 폭은 설치된
    /// 프린터 설정이 결정하기 때문이다.
    /// </summary>
    private static (double Width, double Height) GetPageSize(PrintDialog printDialog)
    {
        try
        {
            var area = printDialog.PrintQueue?.GetPrintCapabilities()?.PageImageableArea;

            if (area is not null && area.ExtentWidth > 0 && area.ExtentHeight > 0)
            {
                return (area.ExtentWidth, area.ExtentHeight);
            }
        }
        catch (Exception)
        {
            // 드라이버가 기능 정보를 주지 않는 경우가 있다. 아래 값으로 넘어간다.
        }

        var width = printDialog.PrintableAreaWidth > 0 ? printDialog.PrintableAreaWidth : FallbackPaperWidth;
        var height = printDialog.PrintableAreaHeight > 0 ? printDialog.PrintableAreaHeight : FallbackPaperHeight;

        return (width, height);
    }

    private static FixedDocument BuildFixedDocument(
        IReadOnlyList<string> lines, double pageWidth, double pageHeight, double lineHeightFactor)
    {
        var typeface = new Typeface(
            new FontFamily(FontFamilyList),
            FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);

        var contentWidth = Math.Max(20, pageWidth - Margin * 2);
        var fontSize = CalculateFontSize(lines, typeface, contentWidth);
        var lineHeight = fontSize * lineHeightFactor;

        var linesPerPage = Math.Max(1, (int)((pageHeight - Margin * 2) / lineHeight));

        var fixedDocument = new FixedDocument();
        fixedDocument.DocumentPaginator.PageSize = new Size(pageWidth, pageHeight);

        for (var start = 0; start < lines.Count; start += linesPerPage)
        {
            var pageLines = lines.Skip(start).Take(linesPerPage).ToList();

            var textBlock = new TextBlock
            {
                Text = string.Join(Environment.NewLine, pageLines),
                FontFamily = new FontFamily(FontFamilyList),
                FontSize = fontSize,
                LineHeight = lineHeight,
                LineStackingStrategy = LineStackingStrategy.BlockLineHeight,
                TextWrapping = TextWrapping.NoWrap,
                Foreground = Brushes.Black,
                Width = contentWidth
            };

            FixedPage.SetLeft(textBlock, Margin);
            FixedPage.SetTop(textBlock, Margin);

            var fixedPage = new FixedPage
            {
                Width = pageWidth,
                Height = pageHeight,
                Background = Brushes.White
            };

            fixedPage.Children.Add(textBlock);

            var pageContent = new PageContent();
            ((IAddChild)pageContent).AddChild(fixedPage);
            fixedDocument.Pages.Add(pageContent);
        }

        return fixedDocument;
    }

    /// <summary>
    /// 가장 긴 줄이 용지 폭에 들어가도록 글자 크기를 맞춘다.
    /// 고정폭 글꼴이라도 실제 글자 너비는 글꼴마다 달라서, 계산 대신 측정한다.
    /// </summary>
    private static double CalculateFontSize(
        IReadOnlyList<string> lines, Typeface typeface, double contentWidth)
    {
        const double baseFontSize = 12;

        var longestLine = lines
            .OrderByDescending(l => l.Length)
            .FirstOrDefault();

        if (string.IsNullOrEmpty(longestLine))
        {
            return baseFontSize;
        }

        var measured = new FormattedText(
            longestLine,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            typeface,
            baseFontSize,
            Brushes.Black,
            pixelsPerDip: 1.0);

        if (measured.Width <= 0)
        {
            return baseFontSize;
        }

        var scaled = baseFontSize * (contentWidth / measured.Width);

        // 너무 작으면 읽을 수 없고, 너무 크면 감열지 폭을 넘는다.
        return Math.Clamp(scaled, 5.0, 14.0);
    }
}
