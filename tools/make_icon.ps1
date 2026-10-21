param([string]$OutIco, [string]$PreviewPng)

Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @"
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

public static class FishIcon
{
    // Rysuje ikonę w układzie 256x256 przeskalowanym do 'size'.
    public static Bitmap Render(int size)
    {
        var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.Clear(Color.Transparent);
            float k = size / 256f;
            g.ScaleTransform(k, k);
            bool small = size <= 24;

            // Tło: zaokrąglony kwadrat w kolorze wody.
            var bg = RoundRect(small ? 4 : 10, small ? 4 : 10, small ? 248 : 236, small ? 248 : 236, small ? 40 : 52);
            using (var b = new LinearGradientBrush(new PointF(0, 0), new PointF(0, 256),
                       Color.FromArgb(70, 170, 235), Color.FromArgb(18, 80, 160)))
                g.FillPath(b, bg);

            if (!small)
            {
                // Fale na dole.
                using (var wave = new GraphicsPath())
                {
                    wave.AddBezier(10, 205, 55, 185, 85, 225, 128, 205);
                    wave.AddBezier(128, 205, 170, 185, 200, 225, 246, 205);
                    wave.AddLine(246, 205, 246, 246);
                    wave.AddLine(246, 246, 10, 246);
                    wave.CloseFigure();
                    var clip = g.Clip;
                    g.SetClip(bg);
                    using (var b = new SolidBrush(Color.FromArgb(60, 255, 255, 255))) g.FillPath(b, wave);
                    g.Clip = clip;
                }

                // Bąbelki przy pysku.
                using (var p = new Pen(Color.FromArgb(220, 255, 255, 255), 6))
                {
                    g.DrawEllipse(p, 30, 62, 18, 18);
                    g.DrawEllipse(p, 46, 30, 12, 12);
                }
            }

            // Ogon (ryba płynie w lewo).
            using (var tail = new GraphicsPath())
            {
                tail.AddPolygon(new[] { new PointF(176, 124), new PointF(236, 76), new PointF(222, 124), new PointF(236, 172) });
                using (var b = new SolidBrush(Color.FromArgb(235, 105, 20))) g.FillPath(b, tail);
            }

            // Płetwa grzbietowa.
            using (var fin = new GraphicsPath())
            {
                fin.AddPolygon(new[] { new PointF(96, 84), new PointF(140, 46), new PointF(160, 88) });
                using (var b = new SolidBrush(Color.FromArgb(235, 105, 20))) g.FillPath(b, fin);
            }

            // Tułów.
            var body = new RectangleF(40, 72, 156, 104);
            using (var b = new LinearGradientBrush(body, Color.FromArgb(255, 200, 60), Color.FromArgb(245, 120, 20), LinearGradientMode.Vertical))
                g.FillEllipse(b, body);

            if (!small)
            {
                // Pokrywa skrzelowa.
                using (var p = new Pen(Color.FromArgb(200, 190, 80, 10), 6))
                    g.DrawArc(p, 70, 92, 50, 64, -60, 120);
            }

            // Oko.
            g.FillEllipse(Brushes.White, 64, 98, small ? 30 : 26, small ? 30 : 26);
            g.FillEllipse(new SolidBrush(Color.FromArgb(25, 35, 60)), small ? 70 : 69, small ? 104 : 103, small ? 18 : 15, small ? 18 : 15);
        }
        return bmp;
    }

    private static GraphicsPath RoundRect(float x, float y, float w, float h, float r)
    {
        var p = new GraphicsPath();
        float d = r * 2;
        p.AddArc(x, y, d, d, 180, 90);
        p.AddArc(x + w - d, y, d, d, 270, 90);
        p.AddArc(x + w - d, y + h - d, d, d, 0, 90);
        p.AddArc(x, y + h - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    // Klasyczny wpis DIB (32 bpp + maska AND) – zgodny ze wszystkimi narzędziami.
    private static byte[] ToDib(Bitmap bmp)
    {
        int s = bmp.Width;
        int maskStride = ((s + 31) / 32) * 4;
        using (var ms = new MemoryStream())
        using (var w = new BinaryWriter(ms))
        {
            w.Write(40); w.Write(s); w.Write(s * 2); w.Write((short)1); w.Write((short)32);
            w.Write(0); w.Write(s * s * 4 + maskStride * s); w.Write(0); w.Write(0); w.Write(0); w.Write(0);
            for (int y = s - 1; y >= 0; y--)
                for (int x = 0; x < s; x++)
                {
                    Color c = bmp.GetPixel(x, y);
                    w.Write(c.B); w.Write(c.G); w.Write(c.R); w.Write(c.A);
                }
            w.Write(new byte[maskStride * s]);
            return ms.ToArray();
        }
    }

    private static byte[] ToPng(Bitmap bmp)
    {
        using (var ms = new MemoryStream())
        {
            bmp.Save(ms, ImageFormat.Png);
            return ms.ToArray();
        }
    }

    public static void Save(string icoPath, string previewPath)
    {
        int[] sizes = { 16, 20, 24, 32, 40, 48, 64, 128, 256 };
        var images = new byte[sizes.Length][];
        for (int i = 0; i < sizes.Length; i++)
            using (var bmp = Render(sizes[i]))
                images[i] = sizes[i] >= 256 ? ToPng(bmp) : ToDib(bmp);

        using (var fs = File.Create(icoPath))
        using (var w = new BinaryWriter(fs))
        {
            w.Write((short)0); w.Write((short)1); w.Write((short)sizes.Length);
            int offset = 6 + 16 * sizes.Length;
            for (int i = 0; i < sizes.Length; i++)
            {
                w.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i]));
                w.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i]));
                w.Write((byte)0); w.Write((byte)0);
                w.Write((short)1); w.Write((short)32);
                w.Write(images[i].Length); w.Write(offset);
                offset += images[i].Length;
            }
            foreach (var img in images) w.Write(img);
        }

        // Podgląd: wszystkie rozmiary obok siebie.
        using (var sheet = new Bitmap(16 + 24 + 32 + 48 + 64 + 128 + 256 + 80, 270))
        using (var g = Graphics.FromImage(sheet))
        {
            g.Clear(Color.White);
            int x = 5;
            foreach (int s in new[] { 16, 24, 32, 48, 64, 128, 256 })
            {
                using (var bmp = Render(s)) g.DrawImage(bmp, x, 260 - s);
                x += s + 10;
            }
            sheet.Save(previewPath, ImageFormat.Png);
        }
    }
}
"@

[FishIcon]::Save($OutIco, $PreviewPng)
