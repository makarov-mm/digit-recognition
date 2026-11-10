using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace Makarov.NeuralNet3D;

internal static partial class DigitPreprocessor
{
    // Dark strokes on a light background, any size.
    public static float[] FromBitmap(Bitmap bitmap, int innerSize = InnerSize)
    {
        int width = bitmap.Width, height = bitmap.Height;
        var ink = new float[width * height];
        var row = new byte[width * 4];
        BitmapData data = bitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            for (int y = 0; y < height; y++)
            {
                Marshal.Copy(data.Scan0 + y * data.Stride, row, 0, row.Length);
                for (int x = 0; x < width; x++)
                {
                    byte b = row[x * 4], g = row[x * 4 + 1], r = row[x * 4 + 2];
                    ink[y * width + x] = 1f - (0.299f * r + 0.587f * g + 0.114f * b) / 255f;
                }
            }
        }
        finally
        {
            bitmap.UnlockBits(data);
        }
        return Normalize(ink, width, height, innerSize);
    }
}
