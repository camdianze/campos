using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;

public static class MakeIcon
{
    // 브랜드 색. 바탕은 메인 색(Pantone 553 C), 글자는 화면 바탕색(크림)이다.
    static readonly Color Back = ColorTranslator.FromHtml("#44705D");
    static readonly Color Fore = ColorTranslator.FromHtml("#ECEADC");

    static PrivateFontCollection _fonts;

    static FontFamily Family(string ttfPath)
    {
        if (_fonts == null)
        {
            _fonts = new PrivateFontCollection();
            _fonts.AddFontFile(ttfPath);
        }
        return _fonts.Families[0];
    }

    /// <summary>한 변 size 픽셀의 아이콘 한 장.</summary>
    public static Bitmap Draw(int size, string ttfPath)
    {
        var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);

        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.Clear(Color.Transparent);

            // 모서리 반지름은 크기에 비례해야 16px과 256px이 같은 모양으로 보인다.
            // 작은 크기에서 반지름이 1px 아래로 내려가면 그냥 사각형이 되므로 바닥을 둔다.
            float r = Math.Max(2f, size * 0.20f);
            using (var path = Rounded(new RectangleF(0, 0, size, size), r))
            using (var brush = new SolidBrush(Back))
            {
                g.FillPath(brush, path);
            }

            // 작은 크기일수록 글자를 키운다. Abril Fatface는 획 대비가 큰 Didone 계열이라,
            // 16px에서 큰 아이콘과 같은 비율로 두면 가는 획이 한 픽셀 아래로 내려가
            // 사라지고 C가 흐릿한 고리처럼 보인다. 여백을 내주고 글자를 키우는 편이
            // 작은 자리에서 읽힌다.
            float ratio = size <= 24 ? 0.80f
                        : size <= 48 ? 0.72f
                        : 0.62f;

            float target = size * ratio;
            var family = Family(ttfPath);
            float emSize = FitEmSize(g, family, "C", target);

            // DrawString 대신 글리프 외곽선을 직접 채운다. 힌팅이 끼어들지 않아
            // 모든 크기에서 같은 모양이 나오고, 위치를 소수점까지 맞출 수 있다.
            using (var glyph = new GraphicsPath())
            {
                glyph.AddString("C", family, (int)FontStyle.Regular, emSize,
                                new PointF(0, 0), StringFormat.GenericTypographic);

                RectangleF ink = glyph.GetBounds();

                // 글리프의 잉크 상자 중심을 아이콘 중심에 놓는다. 글꼴이 가진
                // 위아래 여백까지 포함해 가운데를 맞추면 글자가 살짝 위로 뜬다.
                using (var move = new Matrix())
                {
                    move.Translate((size - ink.Width) / 2f - ink.X,
                                   (size - ink.Height) / 2f - ink.Y);
                    glyph.Transform(move);
                }

                using (var brush = new SolidBrush(Fore))
                {
                    g.FillPath(brush, glyph);
                }
            }
        }

        return bmp;
    }

    /// <summary>글리프 높이가 target 픽셀이 되는 em 크기를 이분법으로 찾는다.</summary>
    static float FitEmSize(Graphics g, FontFamily family, string text, float target)
    {
        float lo = 1f, hi = target * 3f;

        for (int i = 0; i < 24; i++)
        {
            float mid = (lo + hi) / 2f;
            using (var f = new Font(family, mid, FontStyle.Regular, GraphicsUnit.Pixel))
            {
                RectangleF ink = InkBounds(g, f, text, (int)Math.Ceiling(hi * 2));
                if (ink.Height < target) lo = mid; else hi = mid;
            }
        }

        return (lo + hi) / 2f;
    }

    /// <summary>글자가 실제로 칠해진 영역. 글꼴의 위아래 여백은 빼고 잰다.</summary>
    static RectangleF InkBounds(Graphics g, Font font, string text, int canvas)
    {
        using (var path = new GraphicsPath())
        {
            path.AddString(text, font.FontFamily, (int)font.Style, font.Size,
                           new PointF(0, 0), StringFormat.GenericTypographic);
            return path.PointCount == 0 ? RectangleF.Empty : path.GetBounds();
        }
    }

    static GraphicsPath Rounded(RectangleF rect, float radius)
    {
        float d = radius * 2f;
        var p = new GraphicsPath();
        p.AddArc(rect.X, rect.Y, d, d, 180, 90);
        p.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        p.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        p.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    /// <summary>
    /// 여러 크기를 한 .ico로 묶는다.
    ///
    /// 256px만 PNG로 넣고 나머지는 BMP(DIB)로 넣는다. PNG 항목은 Vista 이후만
    /// 읽고, 작은 크기는 BMP 쪽이 어디서나 확실하다. 256을 BMP로 두면 파일이
    /// 262KB 늘어나는데 그만한 이유가 없다.
    /// </summary>
    public static void Write(string path, int[] sizes, string ttfPath)
    {
        var images = new List<byte[]>();
        var isPng = new List<bool>();
        var dims = new List<int>();

        foreach (int s in sizes)
        {
            using (Bitmap bmp = Draw(s, ttfPath))
            {
                if (s >= 256)
                {
                    using (var ms = new MemoryStream())
                    {
                        bmp.Save(ms, ImageFormat.Png);
                        images.Add(ms.ToArray());
                        isPng.Add(true);
                    }
                }
                else
                {
                    images.Add(Dib(bmp));
                    isPng.Add(false);
                }
                dims.Add(s);
            }
        }

        using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write))
        using (var w = new BinaryWriter(fs))
        {
            w.Write((ushort)0);                 // reserved
            w.Write((ushort)1);                 // type: icon
            w.Write((ushort)images.Count);

            int offset = 6 + 16 * images.Count;

            for (int i = 0; i < images.Count; i++)
            {
                w.Write((byte)(dims[i] >= 256 ? 0 : dims[i]));   // 256은 0으로 적는다
                w.Write((byte)(dims[i] >= 256 ? 0 : dims[i]));
                w.Write((byte)0);               // palette
                w.Write((byte)0);               // reserved
                w.Write((ushort)1);             // colour planes
                w.Write((ushort)32);            // bits per pixel
                w.Write(images[i].Length);
                w.Write(offset);
                offset += images[i].Length;
            }

            foreach (var img in images) w.Write(img);
        }
    }

    /// <summary>
    /// BITMAPINFOHEADER + BGRA 픽셀(아래에서 위로) + AND 마스크.
    ///
    /// 32비트 아이콘의 투명도는 알파 채널이 정하므로 AND 마스크는 0으로 채운다.
    /// 다만 <b>마스크를 빼면 안 된다</b> — 헤더의 biHeight가 2배로 적혀 있어서,
    /// 없으면 읽는 쪽이 파일이 잘린 것으로 본다.
    /// </summary>
    static byte[] Dib(Bitmap bmp)
    {
        int w = bmp.Width, h = bmp.Height;
        int maskStride = ((w + 31) / 32) * 4;

        using (var ms = new MemoryStream())
        using (var bw = new BinaryWriter(ms))
        {
            bw.Write(40);                 // biSize
            bw.Write(w);
            bw.Write(h * 2);              // XOR + AND
            bw.Write((ushort)1);
            bw.Write((ushort)32);
            bw.Write(0);                  // BI_RGB
            bw.Write(w * h * 4 + maskStride * h);
            bw.Write(0); bw.Write(0); bw.Write(0); bw.Write(0);

            for (int y = h - 1; y >= 0; y--)
                for (int x = 0; x < w; x++)
                {
                    Color c = bmp.GetPixel(x, y);
                    bw.Write(c.B); bw.Write(c.G); bw.Write(c.R); bw.Write(c.A);
                }

            bw.Write(new byte[maskStride * h]);
            return ms.ToArray();
        }
    }
}
