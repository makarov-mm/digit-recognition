namespace Makarov.NeuralNet3D;

// Labelled 28×28 grayscale digits, one byte per pixel (0 = background, 255 = full ink).
// Bytes take an eighth of the memory of double arrays; samples are converted to network
// input one at a time.
internal sealed class Dataset
{
    public const int Side = 28;
    public const int PixelCount = Side * Side;

    private readonly byte[] _pixels;
    private readonly byte[] _labels;

    public Dataset(byte[] pixels, byte[] labels)
    {
        if (pixels.Length != labels.Length * PixelCount)
            throw new ArgumentException("Pixel data does not match the number of labels.", nameof(pixels));
        _pixels = pixels;
        _labels = labels;
    }

    public int Count => _labels.Length;

    public int Label(int index) => _labels[index];

    // Writes sample `index` as network input in [0, 1].
    public void CopyInput(int index, Span<float> input)
    {
        ReadOnlySpan<byte> image = _pixels.AsSpan(index * PixelCount, PixelCount);
        for (int i = 0; i < image.Length; i++)
            input[i] = image[i] / 255f;
    }
}
