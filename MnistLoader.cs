using System.Buffers.Binary;
using System.IO.Compression;

namespace Makarov.NeuralNet3D;

// MNIST in the original IDX format, fetched once from a GitHub mirror and cached.
internal static class MnistLoader
{
    private const string BaseUrl = "https://raw.githubusercontent.com/fgnt/mnist/master/";
    private const string TrainImages = "train-images-idx3-ubyte.gz";
    private const string TrainLabels = "train-labels-idx1-ubyte.gz";
    private const string TestImages = "t10k-images-idx3-ubyte.gz";
    private const string TestLabels = "t10k-labels-idx1-ubyte.gz";
    private const int ImagesMagic = 0x803;  // unsigned bytes, 3 dimensions
    private const int LabelsMagic = 0x801;  // unsigned bytes, 1 dimension
    private static readonly string[] Files = [TrainImages, TrainLabels, TestImages, TestLabels];

    public static async Task DownloadAsync(string directory, IProgress<string>? progress, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(directory);
        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        foreach (string file in Files)
        {
            string path = Path.Combine(directory, file);
            if (File.Exists(path)) continue;

            progress?.Report($"Downloading {file} …");
            string partial = path + ".part";
            using (HttpResponseMessage response = await http
                       .GetAsync(BaseUrl + file, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                       .ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();
                await using FileStream target = File.Create(partial);
                await response.Content.CopyToAsync(target, cancellationToken).ConfigureAwait(false);
            }
            // Only a complete download gets the final name, so an interrupted one is fetched again.
            File.Move(partial, path, overwrite: true);
        }
    }

    // Throws InvalidDataException if the cached files are damaged.
    public static Dataset Load(string directory, bool test)
    {
        byte[] pixels = ReadIdx(Path.Combine(directory, test ? TestImages : TrainImages), ImagesMagic, Dataset.PixelCount);
        byte[] labels = ReadIdx(Path.Combine(directory, test ? TestLabels : TrainLabels), LabelsMagic, 1);
        if (pixels.Length != labels.Length * Dataset.PixelCount)
            throw new InvalidDataException("MNIST image and label files do not match.");
        if (labels.AsSpan().IndexOfAnyInRange((byte)10, byte.MaxValue) >= 0)
            throw new InvalidDataException("Bad MNIST label.");
        return new Dataset(pixels, labels);
    }

    // Removes the cached files, so the next start downloads them again.
    public static void DeleteCache(string directory)
    {
        if (!Directory.Exists(directory)) return;
        foreach (string file in Files)
            File.Delete(Path.Combine(directory, file));
    }

    // Gzipped IDX file: big-endian magic (its low byte is the number of dimensions), the
    // size of each dimension, then the data. Decompressed straight into the result.
    private static byte[] ReadIdx(string path, int magic, int itemSize)
    {
        using var gzip = new GZipStream(File.OpenRead(path), CompressionMode.Decompress);
        try
        {
            Span<byte> header = stackalloc byte[4 * (1 + (magic & 0xFF))];
            gzip.ReadExactly(header);
            if (ReadInt32(header, 0) != magic)
                throw new InvalidDataException($"{Path.GetFileName(path)} is not the expected MNIST file.");
            if (itemSize > 1 && (ReadInt32(header, 8) != Dataset.Side || ReadInt32(header, 12) != Dataset.Side))
                throw new InvalidDataException("MNIST images are not 28×28.");
            int count = ReadInt32(header, 4);
            if (count is <= 0 or > 100_000)
                throw new InvalidDataException($"{Path.GetFileName(path)} has a bad item count.");

            var data = new byte[count * itemSize];
            gzip.ReadExactly(data);
            return data;
        }
        catch (EndOfStreamException)
        {
            throw new InvalidDataException($"{Path.GetFileName(path)} is truncated.");
        }
    }

    private static int ReadInt32(ReadOnlySpan<byte> data, int offset) => BinaryPrimitives.ReadInt32BigEndian(data[offset..]);
}
