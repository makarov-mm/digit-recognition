namespace Makarov.NeuralNet3D;

// Gets the network ready: cached weights if there are any, otherwise trains it on MNIST
// (downloaded on first use) or, if that is unavailable, on synthetic font-rendered digits.
internal static class Training
{
    private const float InitialLearningRate = 0.05f;
    private const float LearningRateDecay = 0.93f;  // per epoch
    private const float LabelSmoothing = 0.05f;
    private const int SyntheticSamplesPerDigit = 350;

    // Returns a one-line summary for the status bar.
    public static async Task<string> PrepareAsync(
        NeuralNetwork network, string dataDirectory, IProgress<string> progress, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(dataDirectory);
        string weightsPath = Path.Combine(dataDirectory, "weights.bin");
        if (network.TryLoad(weightsPath))
            return "Loaded cached weights.";

        string mnistDirectory = Path.Combine(dataDirectory, "mnist");
        Dataset train;
        Dataset? test = null;
        try
        {
            await MnistLoader.DownloadAsync(mnistDirectory, progress, cancellationToken).ConfigureAwait(false);
            progress.Report("Loading MNIST …");
            train = MnistLoader.Load(mnistDirectory, test: false);
            test = MnistLoader.Load(mnistDirectory, test: true);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            if (ex is InvalidDataException) TryDeleteCache(mnistDirectory);  // damaged download: fetch it again next time
            progress.Report($"MNIST unavailable ({ex.Message}) — using synthetic data.");
            train = SyntheticDigits.Generate(SyntheticSamplesPerDigit, progress, cancellationToken);
        }

        Train(network, train, epochs: test is null ? 14 : 15, progress, cancellationToken);
        string summary = test is null
            ? "Trained on synthetic digits."
            : $"Trained on MNIST — test accuracy {100.0 * Accuracy(network, test):F1}%.";

        try
        {
            network.Save(weightsPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            summary += $" Weights not cached: {ex.Message}";
        }
        return summary;
    }

    private static void Train(NeuralNetwork network, Dataset data, int epochs, IProgress<string> progress, CancellationToken cancellationToken)
    {
        var rng = new Random(12345);
        int[] order = [.. Enumerable.Range(0, data.Count)];
        var input = new float[Dataset.PixelCount];
        float learningRate = InitialLearningRate;

        for (int epoch = 1; epoch <= epochs; epoch++)
        {
            rng.Shuffle(order);
            double loss = 0;
            for (int n = 0; n < order.Length; n++)
            {
                if (n % 4096 == 0) cancellationToken.ThrowIfCancellationRequested();
                int index = order[n];
                data.CopyInput(index, input);
                loss += network.TrainSample(input, data.Label(index), learningRate, LabelSmoothing);
            }
            progress.Report($"Training … epoch {epoch}/{epochs}, loss={loss / data.Count:F3}");
            learningRate *= LearningRateDecay;
        }
    }

    private static double Accuracy(NeuralNetwork network, Dataset data)
    {
        var input = new float[Dataset.PixelCount];
        int correct = 0;
        for (int i = 0; i < data.Count; i++)
        {
            data.CopyInput(i, input);
            if (network.Predict(input) == data.Label(i)) correct++;
        }
        return (double)correct / data.Count;
    }

    private static void TryDeleteCache(string directory)
    {
        try
        {
            MnistLoader.DeleteCache(directory);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Left as is; the next start tries again.
        }
    }
}
