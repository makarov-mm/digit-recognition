using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;

namespace Makarov.NeuralNet3D;

internal static class ImageOps
{
    public const int N = 28;

    public static unsafe double[] Preprocess(Bitmap src, int innerSize = 20)
    {
        int w = src.Width, h = src.Height;
        float[,] ink = new float[h, w];

        BitmapData data = src.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);

        try
        {
            byte* basePtr = (byte*)data.Scan0;
            int stride = data.Stride;

            for (int y = 0; y < h; y++)
            {
                byte* row = basePtr + y * stride;
                for (int x = 0; x < w; x++)
                {
                    byte b = row[x * 4 + 0], g = row[x * 4 + 1], r = row[x * 4 + 2];
                    float lum = (0.299f * r + 0.587f * g + 0.114f * b) / 255f;
                    ink[y, x] = 1f - lum;
                }
            }
        }
        finally
        {
            src.UnlockBits(data);
        }

        const float thr = 0.25f;
        int minx = w, miny = h, maxx = -1, maxy = -1;

        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                if (ink[y, x] > thr)
                {
                    if (x < minx) minx = x; if (x > maxx) maxx = x;
                    if (y < miny) miny = y; if (y > maxy) maxy = y;
                }

        double[] outv = new double[N * N];
        if (maxx < 0) return outv;
        int bw = maxx - minx + 1, bh = maxy - miny + 1;
        double scale = (double)innerSize / Math.Max(bw, bh);
        int dw = Math.Max(1, (int)Math.Round(bw * scale));
        int dh = Math.Max(1, (int)Math.Round(bh * scale));
        int offx = (N - dw) / 2, offy = (N - dh) / 2;
        float[,] grid = new float[N, N];

        for (int gy = 0; gy < dh; gy++)
            for (int gx = 0; gx < dw; gx++)
            {
                double sx0 = minx + gx / (double)dw * bw, sx1 = minx + (gx + 1) / (double)dw * bw;
                double sy0 = miny + gy / (double)dh * bh, sy1 = miny + (gy + 1) / (double)dh * bh;
                int ix0 = (int)Math.Floor(sx0), ix1 = Math.Min(w - 1, (int)Math.Floor(sx1 - 1e-6));
                int iy0 = (int)Math.Floor(sy0), iy1 = Math.Min(h - 1, (int)Math.Floor(sy1 - 1e-6));
                if (ix1 < ix0) ix1 = ix0;
                if (iy1 < iy0) iy1 = iy0;
                float sum = 0; int cnt = 0;
                for (int yy = iy0; yy <= iy1; yy++)
                    for (int xx = ix0; xx <= ix1; xx++) { sum += ink[yy, xx]; cnt++; }
                grid[offy + gy, offx + gx] = cnt > 0 ? sum / cnt : 0;
            }

        double m = 0, cx = 0, cy = 0;
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++) { float v = grid[y, x]; m += v; cx += v * x; cy += v * y; }

        if (m > 1e-6) { cx /= m; cy /= m; }
        int shx = (int)Math.Round((N - 1) / 2.0 - cx);
        int shy = (int)Math.Round((N - 1) / 2.0 - cy);

        float[,] g2 = new float[N, N];
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                int nx = x + shx, ny = y + shy;
                if (nx >= 0 && nx < N && ny >= 0 && ny < N) g2[ny, nx] = grid[y, x];
            }

        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float s = 0; int c = 0;
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int yy = y + dy, xx = x + dx;
                        if (xx >= 0 && xx < N && yy >= 0 && yy < N) { s += g2[yy, xx]; c++; }
                    }
                outv[y * N + x] = Math.Min(1.0, s / c * 1.15);
            }
        return outv;
    }

    private static readonly string[] Families =
    {
        "Arial", "Times New Roman", "Calibri", "Segoe UI", "Tahoma",
        "Verdana", "Consolas", "Georgia", "Trebuchet MS", "Comic Sans MS"
    };

    public static List<(double[] x, int y)> GenerateDataset(int perDigit, Action<int> progress = null)
    {
        var rng = new Random(777);
        var list = new List<(double[], int)>(perDigit * 10);
        int done = 0;

        for (int d = 0; d < 10; d++)
            for (int k = 0; k < perDigit; k++)
            {
                using var bmp = new Bitmap(180, 180, PixelFormat.Format32bppArgb);
                using (var g = Graphics.FromImage(bmp))
                {
                    g.Clear(Color.White);
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

                    string fam = Families[rng.Next(Families.Length)];
                    FontStyle style = rng.NextDouble() < 0.3 ? FontStyle.Bold : FontStyle.Regular;
                    float size = 85f + (float)(rng.NextDouble() * 45.0);

                    Font font;
                    try { font = new Font(fam, size, style, GraphicsUnit.Pixel); }
                    catch { font = new Font(FontFamily.GenericSansSerif, size, style, GraphicsUnit.Pixel); }

                    string s = d.ToString();
                    SizeF sz = g.MeasureString(s, font);

                    GraphicsState state = g.Save();
                    g.TranslateTransform(90, 90);
                    g.RotateTransform((float)(rng.NextDouble() * 30.0 - 15.0));
                    g.DrawString(s, font, Brushes.Black,
                        -sz.Width / 2 + (float)(rng.NextDouble() * 16 - 8),
                        -sz.Height / 2 + (float)(rng.NextDouble() * 16 - 8));
                    g.Restore(state);
                    font.Dispose();
                }

                int inner = 16 + rng.Next(7);
                double[] x = Preprocess(bmp, inner);

                for (int i = 0; i < x.Length; i++)
                    if (rng.NextDouble() < 0.03) 
                        x[i] = Math.Min(1.0, x[i] + rng.NextDouble() * 0.3);

                list.Add((x, d));
                if ((++done & 255) == 0) progress?.Invoke(done);
            }

        return list;
    }
}
