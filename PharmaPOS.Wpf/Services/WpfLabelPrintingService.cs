using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Shapes;
using PharmaPOS.Application.Products;

namespace Lightweight_Digital_Inventory_Management___POS_System.Services;

/// <summary>
/// 바코드 라벨을 기본 프린터로 출력한다. 영수증·복약안내와 같은 경로(ThermalTextPrinter)를 쓴다.
///
/// 막대를 글꼴이나 외부 패키지 없이 직접 사각형으로 그린다. 바코드 글꼴을 embed하면
/// 라이선스를 따져야 하고, 패키지를 넣으면 이것 하나 때문에 의존성이 는다.
/// 굵기 계산은 Code128Encoder가 하고 여기서는 그리기만 한다.
///
/// 라벨 한 장이 한 페이지다. 80mm 감열지에서는 장마다 잘리고,
/// A4 프린터에 걸어도 장당 한 면이라 섞이지 않는다.
/// </summary>
public class WpfLabelPrintingService : ILabelPrintingService
{
    private const double Margin = 8;

    /// <summary>막대 높이. 스캐너가 비스듬히 읽어도 잡히도록 넉넉히 둔다 (약 12mm).</summary>
    private const double BarcodeHeight = 46;

    /// <summary>
    /// 모듈(가장 가는 막대) 폭의 한계.
    /// 아래로는 감열 헤드가 뭉개 못 읽고, 위로는 A4 같은 넓은 용지에서 바코드만 커진다.
    /// </summary>
    private const double MinModuleWidth = 1.0;
    private const double MaxModuleWidth = 3.0;

    public Task<bool> PrintLabelsAsync(IReadOnlyList<BarcodeLabel> labels)
    {
        if (labels.Count == 0)
        {
            return Task.FromResult(false);
        }

        // 담을 수 없는 문자가 섞이면 그리는 도중이 아니라 여기서 걸러 낸다.
        if (labels.Any(l => !Code128Encoder.CanEncode(l.Code)))
        {
            return Task.FromResult(false);
        }

        var printed = ThermalTextPrinter.TryPrint(
            (width, height) => BuildDocument(labels, width, height),
            "Barcode labels");

        return Task.FromResult(printed);
    }

    private static FixedDocument BuildDocument(
        IReadOnlyList<BarcodeLabel> labels, double pageWidth, double pageHeight)
    {
        var document = new FixedDocument();
        document.DocumentPaginator.PageSize = new Size(pageWidth, pageHeight);

        foreach (var label in labels)
        {
            var page = new FixedPage
            {
                Width = pageWidth,
                Height = pageHeight,
                Background = Brushes.White
            };

            var content = BuildLabel(label, pageWidth - Margin * 2);

            FixedPage.SetLeft(content, Margin);
            FixedPage.SetTop(content, Margin);
            page.Children.Add(content);

            var pageContent = new PageContent();
            ((IAddChild)pageContent).AddChild(page);
            document.Pages.Add(pageContent);
        }

        return document;
    }

    private static UIElement BuildLabel(BarcodeLabel label, double contentWidth)
    {
        var stack = new StackPanel { Width = contentWidth };

        // 상품명이 맨 위다. 선반에서는 막대가 아니라 이름을 보고 찾는다.
        stack.Children.Add(new TextBlock
        {
            Text = label.ProductName,
            FontFamily = new FontFamily(ThermalTextPrinter.FontFamilyList),
            FontSize = 12,
            FontWeight = FontWeights.Bold,
            Foreground = Brushes.Black,
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 0, 0, 4)
        });

        if (label.Caption is not null)
        {
            // 박스용과 낱개용은 상품명이 같다. 이 줄이 없으면 둘을 구분할 수 없다.
            stack.Children.Add(new TextBlock
            {
                Text = label.Caption,
                FontFamily = new FontFamily(ThermalTextPrinter.FontFamilyList),
                FontSize = 10,
                Foreground = Brushes.Black,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 0, 0, 4)
            });
        }

        stack.Children.Add(BuildBarcode(label.Code, contentWidth));

        // 사람이 읽는 값. 스캐너가 안 될 때 손으로 칠 수 있어야 한다.
        stack.Children.Add(new TextBlock
        {
            Text = label.Code,
            FontFamily = new FontFamily(ThermalTextPrinter.FontFamilyList),
            FontSize = 11,
            Foreground = Brushes.Black,
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 3, 0, 0)
        });

        return stack;
    }

    private static UIElement BuildBarcode(string code, double contentWidth)
    {
        var widths = Code128Encoder.ToModuleWidths(code);
        var totalModules = widths.Sum();

        // 용지 폭에 맞춰 모듈 폭을 정하되 한계를 벗어나지 않게 한다.
        // 좁아서 잘리는 쪽보다 조금 작게 그리는 쪽이 낫다 — 한계 안이면 스캐너가 읽는다.
        var moduleWidth = Math.Clamp(contentWidth / totalModules, MinModuleWidth, MaxModuleWidth);

        var canvas = new Canvas
        {
            Width = totalModules * moduleWidth,
            Height = BarcodeHeight,
            Background = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        var x = 0.0;
        var isBar = true;

        foreach (var width in widths)
        {
            var length = width * moduleWidth;

            if (isBar)
            {
                var bar = new Rectangle
                {
                    Width = length,
                    Height = BarcodeHeight,
                    Fill = Brushes.Black
                };

                Canvas.SetLeft(bar, x);
                Canvas.SetTop(bar, 0);
                canvas.Children.Add(bar);
            }

            x += length;
            isBar = !isBar;
        }

        return canvas;
    }
}
