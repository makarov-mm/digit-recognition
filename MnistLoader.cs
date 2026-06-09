using System.IO.Compression;

namespace Makarov.NeuralNet3D;

internal static class MnistLoader
{
    private const string BaseUrl = "https://raw.githubusercontent.com/fgnt/mnist/master/";

    private static readonly string[] FileNames =
    {
        "train-images-idx3-ubyte.gz", "train-labels-idx1-ubyte.gz",
        "t10k-images-idx3-ubyte.gz",  "t10k-labels-idx1-ubyte.gz"
    };

    public static void EnsureDownloaded(string dir, Action<string> status = null)
    {
        Directory.CreateDirectory(dir);
        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };

        foreach (string f in FileNames)
        {
            string path = Path.Combine(dir, f);
            if (File.Exists(path) && new FileInfo(path).Length > 1000) continue;

            status?.Invoke($"Downloading {f} …");
            byte[] bytes = http.GetByteArrayAsync(BaseUrl + f).GetAwaiter().GetResult();
            File.WriteAllBytes(path, bytes);
        }
    }

    public static List<(double[] x, int y)> Load(string dir, bool test)
    {
        string imgPath = Path.Combine(dir, test ? "t10k-images-idx3-ubyte.gz" : "train-images-idx3-ubyte.gz");
        string lblPath = Path.Combine(dir, test ? "t10k-labels-idx1-ubyte.gz" : "train-labels-idx1-ubyte.gz");

        byte[] images = Decompress(imgPath);
        byte[] labels = Decompress(lblPath);

        if (ReadBE(images, 0) != 0x803) throw new InvalidDataException("Bad MNIST image magic");
        if (ReadBE(labels, 0) != 0x801) throw new InvalidDataException("Bad MNIST label magic");

        int n = ReadBE(images, 4);
        int rows = ReadBE(images, 8), cols = ReadBE(images, 12);
        int nl = ReadBE(labels, 4);
        if (n != nl) throw new InvalidDataException("MNIST count mismatch");

        int stride = rows * cols;
        const int imgHeader = 16, lblHeader = 8;

        var list = new List<(double[], int)>(n);
        for (int s = 0; s < n; s++)
        {
            var x = new double[stride];
            int off = imgHeader + s * stride;
            for (int i = 0; i < stride; i++) x[i] = images[off + i] / 255.0;
            list.Add((x, labels[lblHeader + s]));
        }
        return list;
    }

    private static byte[] Decompress(string path)
    {
        using var fs = File.OpenRead(path);
        using var gz = new GZipStream(fs, CompressionMode.Decompress);
        using var ms = new MemoryStream();
        gz.CopyTo(ms);
        return ms.ToArray();
    }

    private static int ReadBE(byte[] b, int o) => (b[o] << 24) | (b[o + 1] << 16) | (b[o + 2] << 8) | b[o + 3];
}
