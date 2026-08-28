using System.IO;
using System.Windows.Media.Imaging;
using PharmaPOS.Application.Products;

namespace Lightweight_Digital_Inventory_Management___POS_System.Services;

/// <summary>
/// 상품 사진을 저장할 형태로 줄이고 JPEG로 다시 압축한다.
///
/// WPF 쪽에 있는 이유는 DPAPI·인쇄와 같다 — 이미지를 읽고 크기를 바꾸는 일은
/// PresentationCore가 들고 있는 기능이라 Application 계층에 둘 수 없다.
///
/// 원본을 그대로 넣지 않는 이유: 요즘 휴대폰 사진은 장당 3~5MB다.
/// 상품 300개면 DB가 1GB를 넘고, 그 DB를 통째로 복사하는 백업이 감당하지 못한다.
/// 상세 화면에서 보는 용도라 긴 변 800px이면 충분하다.
/// </summary>
public class WpfPhotoEncoder : IPhotoEncoder
{
    /// <summary>
    /// JPEG 품질. 85는 눈으로 열화를 알아보기 어려우면서 파일이 눈에 띄게 작아지는 지점이다.
    /// </summary>
    private const int JpegQuality = 85;

    public byte[]? Encode(byte[] source, int maxEdgePixels)
    {
        try
        {
            using var input = new MemoryStream(source);

            // OnLoad로 읽어야 스트림을 닫은 뒤에도 쓸 수 있다.
            var decoded = new BitmapImage();
            decoded.BeginInit();
            decoded.CacheOption = BitmapCacheOption.OnLoad;
            decoded.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
            decoded.StreamSource = input;
            decoded.EndInit();
            decoded.Freeze();

            if (decoded.PixelWidth <= 0 || decoded.PixelHeight <= 0)
            {
                return null;
            }

            var scale = GetScale(decoded.PixelWidth, decoded.PixelHeight, maxEdgePixels);

            BitmapSource image = decoded;

            // 이미 작은 사진은 늘리지 않는다. 키워 봐야 화질은 그대로이고 용량만 는다.
            if (scale < 1.0)
            {
                var scaled = new TransformedBitmap(
                    decoded, new System.Windows.Media.ScaleTransform(scale, scale));
                scaled.Freeze();
                image = scaled;
            }

            var encoder = new JpegBitmapEncoder { QualityLevel = JpegQuality };
            encoder.Frames.Add(BitmapFrame.Create(image));

            using var output = new MemoryStream();
            encoder.Save(output);

            return output.ToArray();
        }
        catch (Exception)
        {
            // 이미지가 아니거나 깨진 파일. 부르는 쪽이 "읽을 수 없다"고 알린다.
            return null;
        }
    }

    private static double GetScale(int width, int height, int maxEdgePixels)
    {
        var longestEdge = Math.Max(width, height);

        return longestEdge <= maxEdgePixels
            ? 1.0
            : (double)maxEdgePixels / longestEdge;
    }
}
