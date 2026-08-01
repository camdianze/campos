using System.Globalization;
using System.Printing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;
using PharmaPOS.Application.Counselling;

namespace Lightweight_Digital_Inventory_Management___POS_System.Services;

/// <summary>
/// 복약안내 용지를 Windows 인쇄 파이프라인(FixedDocument)으로 출력한다.
///
/// ESC/POS 바이트를 직접 쏘지 않고 프린터 드라이버를 거치는 이유:
///   - 이 저장소에는 프린터 출력 코드가 원래 없었고, 검증할 하드웨어도 없다.
///     기종별 명령 차이를 추측으로 구현하면 현장에서 그대로 재작업이 된다.
///   - 크메르어·라오어 같은 복합 문자의 자소 결합이 WPF 텍스트 스택에서
///     처리된다. ESC/POS 텍스트 모드에서 자소가 분해되는 문제를 피하려고
///     스펙이 raster 모드를 요구했는데, 이 경로에서는 그 우회가 필요 없다.
///     (로케일 파일의 render_mode는 형식 계약으로 남겨 두었다.)
///
/// 인쇄 대화상자는 띄우지 않는다. 인쇄 여부는 판매 화면에서 이미 물어봤고,
/// 거기서 또 창이 뜨면 약사가 매 판매마다 두 번 확인해야 한다.
/// </summary>
public class WpfCounsellingSheetPrintingService : ICounsellingSheetPrintingService
{
    /// <summary>
    /// 고정폭 글꼴 + 대체 글꼴 목록. 앞 글꼴에 없는 글자는 뒤 글꼴에서 찾는다.
    /// 크메르 문자와 도형 문자(■□▨)가 두부(tofu)로 찍히지 않게 하기 위한 것이다.
    /// </summary>
    private const string FontFamilyList =
        "Consolas, Courier New, Khmer UI, Leelawadee UI, Malgun Gothic, Segoe UI Symbol";

    /// <summary>58mm 감열지의 인쇄 가능 폭 근사치 (1/96인치 단위). 드라이버 정보를 못 얻을 때만 쓴다.</summary>
    private const double FallbackPaperWidth = 200;

    private const double FallbackPaperHeight = 1000;

    private const double Margin = 6;

    public Task<CounsellingPrintResult> PrintAsync(CounsellingSheetDocument document)
    {
        try
        {
            var printDialog = new PrintDialog();

            if (printDialog.PrintQueue is null)
            {
                return Task.FromResult(CounsellingPrintResult.Failure(
                    "No printer is available. The counselling sheet was not printed."));
            }

            var (pageWidth, pageHeight) = GetPageSize(printDialog);

            var fixedDocument = BuildFixedDocument(document, pageWidth, pageHeight);

            printDialog.PrintDocument(
                fixedDocument.DocumentPaginator,
                $"Antibiotic counselling - {document.ProductName}");

            return Task.FromResult(CounsellingPrintResult.Success());
        }
        catch (Exception)
        {
            // 프린터 연결 해제, 드라이버 오류, 용지 없음 — 무엇이든 여기로 온다.
            // 판매는 이미 확정됐으므로 되돌리지 않고 실패만 알린다.
            return Task.FromResult(CounsellingPrintResult.Failure(
                "The counselling sheet could not be printed."));
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
        CounsellingSheetDocument document, double pageWidth, double pageHeight)
    {
        var typeface = new Typeface(
            new FontFamily(FontFamilyList),
            FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);

        var contentWidth = Math.Max(20, pageWidth - Margin * 2);
        var fontSize = CalculateFontSize(document, typeface, contentWidth);
        var lineHeight = fontSize * 1.3;

        var linesPerPage = Math.Max(1, (int)((pageHeight - Margin * 2) / lineHeight));

        var fixedDocument = new FixedDocument();
        fixedDocument.DocumentPaginator.PageSize = new Size(pageWidth, pageHeight);

        var lines = BuildPrintableLines(document);

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
    /// QR 이미지 대신 주소를 글자로 덧붙인다.
    /// QR 인코더를 넣으려면 외부 패키지가 필요한데, 아직 QR이 가리킬 주소가
    /// 정해지지 않아 의존성부터 늘리지 않았다. 주소가 설정돼 있으면
    /// 적어도 사람이 옮겨 칠 수는 있게 해 둔다.
    /// </summary>
    private static List<string> BuildPrintableLines(CounsellingSheetDocument document)
    {
        var lines = document.Lines.ToList();

        if (string.IsNullOrWhiteSpace(document.QrUrl))
        {
            return lines;
        }

        var markerIndex = lines.FindIndex(l => l.StartsWith("[QR]", StringComparison.Ordinal));

        if (markerIndex >= 0)
        {
            lines.Insert(markerIndex + 1, "     " + document.QrUrl);
        }

        return lines;
    }

    /// <summary>
    /// 가장 긴 줄이 용지 폭에 들어가도록 글자 크기를 맞춘다.
    /// 고정폭 글꼴이라도 실제 글자 너비는 글꼴마다 달라서, 계산 대신 측정한다.
    /// </summary>
    private static double CalculateFontSize(
        CounsellingSheetDocument document, Typeface typeface, double contentWidth)
    {
        const double baseFontSize = 12;

        var longestLine = document.Lines
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
