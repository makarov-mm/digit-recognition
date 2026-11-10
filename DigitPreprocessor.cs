namespace Makarov.NeuralNet3D;

// Turns a drawing into network input the way the MNIST digits were prepared: the ink's
// bounding box is scaled so its longer side spans 20 pixels, the result is centred by its
// centre of mass in a 28×28 field, and a light blur thickens it towards MNIST's strokes.
internal static partial class DigitPreprocessor
{
    public const int InnerSize = 20;
    private const float InkThreshold = 0.25f;

    // ink: width × height values in [0, 1], row by row. Returns 28×28 values in [0, 1].
    public static float[] Normalize(ReadOnlySpan<float> ink, int width, int height, int innerSize = InnerSize)
    {
        const int side = Dataset.Side;
        var result = new float[side * side];

        int minX = width, minY = height, maxX = -1, maxY = -1;
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                if (ink[y * width + x] > InkThreshold)
                {
                    minX = Math.Min(minX, x);
                    maxX = Math.Max(maxX, x);
                    minY = Math.Min(minY, y);
                    maxY = Math.Max(maxY, y);
                }
        if (maxX < 0) return result;  // nothing drawn

        // Shrink the box by area averaging and centre it in the field.
        int boxWidth = maxX - minX + 1, boxHeight = maxY - minY + 1;
        double scale = (double)innerSize / Math.Max(boxWidth, boxHeight);
        int scaledWidth = Math.Max(1, (int)Math.Round(boxWidth * scale));
        int scaledHeight = Math.Max(1, (int)Math.Round(boxHeight * scale));
        int offsetX = (side - scaledWidth) / 2, offsetY = (side - scaledHeight) / 2;
        var scaled = new float[side * side];
        for (int gy = 0; gy < scaledHeight; gy++)
        {
            double sy0 = minY + gy / (double)scaledHeight * boxHeight;
            double sy1 = minY + (gy + 1) / (double)scaledHeight * boxHeight;
            int y0 = (int)Math.Floor(sy0);
            int y1 = Math.Max(y0, Math.Min(height - 1, (int)Math.Floor(sy1 - 1e-6)));
            for (int gx = 0; gx < scaledWidth; gx++)
            {
                double sx0 = minX + gx / (double)scaledWidth * boxWidth;
                double sx1 = minX + (gx + 1) / (double)scaledWidth * boxWidth;
                int x0 = (int)Math.Floor(sx0);
                int x1 = Math.Max(x0, Math.Min(width - 1, (int)Math.Floor(sx1 - 1e-6)));
                float sum = 0f;
                int count = 0;
                for (int yy = y0; yy <= y1; yy++)
                    for (int xx = x0; xx <= x1; xx++)
                    {
                        sum += ink[yy * width + xx];
                        count++;
                    }
                scaled[(offsetY + gy) * side + offsetX + gx] = sum / count;
            }
        }

        // Move the centre of mass to the middle of the field.
        double mass = 0, centreX = 0, centreY = 0;
        for (int y = 0; y < side; y++)
            for (int x = 0; x < side; x++)
            {
                float v = scaled[y * side + x];
                mass += v;
                centreX += v * x;
                centreY += v * y;
            }
        if (mass > 1e-6)
        {
            centreX /= mass;
            centreY /= mass;
        }
        int shiftX = (int)Math.Round((side - 1) / 2.0 - centreX);
        int shiftY = (int)Math.Round((side - 1) / 2.0 - centreY);
        var centred = new float[side * side];
        for (int y = 0; y < side; y++)
            for (int x = 0; x < side; x++)
            {
                int tx = x + shiftX, ty = y + shiftY;
                if (tx >= 0 && tx < side && ty >= 0 && ty < side)
                    centred[ty * side + tx] = scaled[y * side + x];
            }

        // 3×3 box blur with a little gain.
        for (int y = 0; y < side; y++)
            for (int x = 0; x < side; x++)
            {
                float sum = 0f;
                int count = 0;
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int yy = y + dy, xx = x + dx;
                        if (xx >= 0 && xx < side && yy >= 0 && yy < side)
                        {
                            sum += centred[yy * side + xx];
                            count++;
                        }
                    }
                result[y * side + x] = (float)Math.Min(1.0, sum / count * 1.15);
            }
        return result;
    }
}
