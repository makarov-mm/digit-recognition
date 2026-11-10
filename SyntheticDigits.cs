using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Globalization;

namespace Makarov.NeuralNet3D;

// Fallback training data when MNIST cannot be downloaded: digits typed in system fonts,
// randomly sized, rotated and shifted, with a little speckle noise.
internal static class SyntheticDigits
{
    private static readonly string[] FontFamilies =
    [
        "Arial", "Times New Roman", "Calibri", "Segoe UI", "Tahoma",
        "Verdana", "Consolas", "Georgia", "Trebuchet MS", "Comic Sans MS",
    ];

    public static Dataset Generate(int perDigit, IProgress<string>? progress, CancellationToken cancellationToken)
    {
        const int canvasSize = 180;
        int total = perDigit * 10;
        var pixels = new byte[total * Dataset.PixelCount];
        var labels = new byte[total];
        var rng = new Random(777);

        using var bitmap = new Bitmap(canvasSize, canvasSize, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

        int sample = 0;
        for (int digit = 0; digit < 10; digit++)
            for (int k = 0; k < perDigit; k++, sample++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                DrawDigit(graphics, canvasSize / 2f, digit, rng);
                float[] input = DigitPreprocessor.FromBitmap(bitmap, innerSize: 16 + rng.Next(7));

                Span<byte> image = pixels.AsSpan(sample * Dataset.PixelCount, Dataset.PixelCount);
                for (int i = 0; i < input.Length; i++)
                {
                    double v = input[i];
                    if (rng.NextDouble() < 0.03)
                        v = Math.Min(1.0, v + rng.NextDouble() * 0.3);
                    image[i] = (byte)Math.Round(v * 255.0);
                }
                labels[sample] = (byte)digit;

                if ((sample + 1) % 256 == 0)
                    progress?.Report($"Generating dataset … {sample + 1}/{total}");
            }
        return new Dataset(pixels, labels);
    }

    private static void DrawDigit(Graphics graphics, float centre, int digit, Random rng)
    {
        graphics.ResetTransform();
        graphics.Clear(Color.White);

        string family = FontFamilies[rng.Next(FontFamilies.Length)];
        FontStyle style = rng.NextDouble() < 0.3 ? FontStyle.Bold : FontStyle.Regular;
        float size = 85f + (float)(rng.NextDouble() * 45.0);
        using Font font = CreateFont(family, size, style);

        string text = digit.ToString(CultureInfo.InvariantCulture);
        SizeF extent = graphics.MeasureString(text, font);
        graphics.TranslateTransform(centre, centre);
        graphics.RotateTransform((float)(rng.NextDouble() * 30.0 - 15.0));
        graphics.DrawString(text, font, Brushes.Black,
            -extent.Width / 2 + (float)(rng.NextDouble() * 16 - 8),
            -extent.Height / 2 + (float)(rng.NextDouble() * 16 - 8));
    }

    private static Font CreateFont(string family, float size, FontStyle style)
    {
        try
        {
            return new Font(family, size, style, GraphicsUnit.Pixel);
        }
        catch (ArgumentException)  // the family lacks this style
        {
            return new Font(FontFamily.GenericSansSerif, size, style, GraphicsUnit.Pixel);
        }
    }
}
