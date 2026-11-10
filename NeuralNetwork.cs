using System.Numerics;
using System.Runtime.InteropServices;

namespace Makarov.NeuralNet3D;

// Fully connected network: ReLU hidden layers and a softmax output, trained one sample
// at a time with SGD on the cross-entropy loss (optionally with label smoothing).
//
// The weights of a layer are stored input-major: row i holds the weights from input i
// to every output. Most inputs (blank pixels) and many ReLU outputs are exactly zero and
// contribute nothing, so the forward pass and the weight update skip their rows and
// process the remaining rows as contiguous SIMD vectors.
internal sealed class NeuralNetwork
{
    private const int FileMagic = 0x4433_4E4E;  // "NN3D"
    private const int FileVersion = 2;

    private readonly int[] _sizes;
    private readonly float[][] _weights;      // per layer of connections: inputs × outputs
    private readonly float[][] _biases;       // per layer of connections: outputs
    private readonly float[][] _activations;  // per layer of neurons, 0 = input
    private readonly float[][] _errors;       // per layer of neurons: dLoss/dz (training scratch)

    public NeuralNetwork(params ReadOnlySpan<int> sizes)
    {
        if (sizes.Length < 2) throw new ArgumentException("A network needs an input and an output layer.", nameof(sizes));

        _sizes = sizes.ToArray();
        _weights = new float[_sizes.Length - 1][];
        _biases = new float[_sizes.Length - 1][];
        _activations = new float[_sizes.Length][];
        _errors = new float[_sizes.Length][];
        for (int n = 0; n < _sizes.Length; n++)
        {
            _activations[n] = new float[_sizes[n]];
            _errors[n] = n == 0 ? [] : new float[_sizes[n]];
        }

        // He initialisation from a fixed seed, so every fresh start trains the same way.
        var rng = new Random(1234);
        for (int l = 0; l < _weights.Length; l++)
        {
            int inputs = _sizes[l], outputs = _sizes[l + 1];
            float[] weights = _weights[l] = new float[inputs * outputs];
            _biases[l] = new float[outputs];
            double scale = Math.Sqrt(2.0 / inputs);
            for (int j = 0; j < outputs; j++)
                for (int i = 0; i < inputs; i++)
                    weights[i * outputs + j] = (float)(NextGaussian(rng) * scale);
        }
    }

    public int LayerCount => _sizes.Length;

    public int LayerSize(int layer) => _sizes[layer];

    public ReadOnlySpan<float> Activations(int layer) => _activations[layer];

    // Weight of the connection from neuron `from` of `layer` to neuron `to` of the next layer.
    public float Weight(int layer, int from, int to) => _weights[layer][from * _sizes[layer + 1] + to];

    // Runs the network; the result (class probabilities) is valid until the next call.
    public ReadOnlySpan<float> Forward(ReadOnlySpan<float> input)
    {
        if (input.Length != _sizes[0]) throw new ArgumentException($"Expected {_sizes[0]} inputs.", nameof(input));
        input.CopyTo(_activations[0]);

        int last = _weights.Length - 1;
        for (int l = 0; l <= last; l++)
        {
            ReadOnlySpan<float> a = _activations[l];
            Span<float> z = _activations[l + 1];
            float[] weights = _weights[l];
            int outputs = z.Length;

            _biases[l].CopyTo(z);
            for (int i = 0; i < a.Length; i++)
                if (a[i] != 0f)
                    Vectors.AddScaled(z, weights.AsSpan(i * outputs, outputs), a[i]);

            if (l < last)
                Relu(z);
            else
                Softmax(z);
        }
        return _activations[^1];
    }

    public int Predict(ReadOnlySpan<float> input) => ArgMax(Forward(input));

    // One SGD step on a single sample; returns its cross-entropy loss.
    public float TrainSample(ReadOnlySpan<float> input, int label, float learningRate, float labelSmoothing = 0f)
    {
        ReadOnlySpan<float> output = Forward(input);
        int classes = output.Length;
        float on = 1f - labelSmoothing + labelSmoothing / classes;
        float off = labelSmoothing / classes;

        Span<float> outputError = _errors[^1];
        for (int j = 0; j < classes; j++)
            outputError[j] = output[j] - (j == label ? on : off);
        float loss = -MathF.Log(MathF.Max(output[label], 1e-12f));

        for (int l = _weights.Length - 1; l >= 0; l--)
        {
            ReadOnlySpan<float> a = _activations[l];
            ReadOnlySpan<float> error = _errors[l + 1];
            float[] weights = _weights[l];
            int outputs = error.Length;

            // Error of the layer below, through the weights as they were before this step;
            // ReLU lets it through only where the neuron fired.
            if (l > 0)
            {
                Span<float> below = _errors[l];
                for (int i = 0; i < a.Length; i++)
                    below[i] = a[i] > 0f ? Vectors.Dot(weights.AsSpan(i * outputs, outputs), error) : 0f;
            }

            for (int i = 0; i < a.Length; i++)
                if (a[i] != 0f)
                    Vectors.AddScaled(weights.AsSpan(i * outputs, outputs), error, -learningRate * a[i]);
            Vectors.AddScaled(_biases[l], error, -learningRate);
        }
        return loss;
    }

    public static int ArgMax(ReadOnlySpan<float> values)
    {
        int best = 0;
        for (int i = 1; i < values.Length; i++)
            if (values[i] > values[best])
                best = i;
        return best;
    }

    public void Save(string path)
    {
        string temp = path + ".tmp";
        using (var writer = new BinaryWriter(File.Create(temp)))
        {
            writer.Write(FileMagic);
            writer.Write(FileVersion);
            writer.Write(_sizes.Length);
            foreach (int size in _sizes)
                writer.Write(size);
            for (int l = 0; l < _weights.Length; l++)
            {
                writer.Write(MemoryMarshal.AsBytes(_biases[l].AsSpan()));
                writer.Write(MemoryMarshal.AsBytes(_weights[l].AsSpan()));
            }
        }
        File.Move(temp, path, overwrite: true);  // never leaves a half-written file under the real name
    }

    // Loads weights saved by Save for the same layer sizes; leaves the network unchanged otherwise.
    public bool TryLoad(string path)
    {
        if (!File.Exists(path)) return false;
        try
        {
            using var stream = File.OpenRead(path);
            using var reader = new BinaryReader(stream);
            if (reader.ReadInt32() != FileMagic || reader.ReadInt32() != FileVersion || reader.ReadInt32() != _sizes.Length)
                return false;
            foreach (int size in _sizes)
                if (reader.ReadInt32() != size)
                    return false;

            float[][] biases = [.. _biases.Select(b => new float[b.Length])];
            float[][] weights = [.. _weights.Select(w => new float[w.Length])];
            for (int l = 0; l < weights.Length; l++)
            {
                stream.ReadExactly(MemoryMarshal.AsBytes(biases[l].AsSpan()));
                stream.ReadExactly(MemoryMarshal.AsBytes(weights[l].AsSpan()));
            }
            if (stream.Position != stream.Length) return false;

            for (int l = 0; l < weights.Length; l++)
            {
                biases[l].CopyTo(_biases[l]);
                weights[l].CopyTo(_weights[l]);
            }
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static void Relu(Span<float> values)
    {
        for (int i = 0; i < values.Length; i++)
            values[i] = values[i] > 0f ? values[i] : 0f;
    }

    private static void Softmax(Span<float> values)
    {
        float max = float.NegativeInfinity;
        foreach (float v in values)
            max = MathF.Max(max, v);

        float sum = 0f;
        for (int i = 0; i < values.Length; i++)
        {
            values[i] = MathF.Exp(values[i] - max);
            sum += values[i];
        }
        for (int i = 0; i < values.Length; i++)
            values[i] /= sum;
    }

    private static double NextGaussian(Random rng)
    {
        double u1 = 1.0 - rng.NextDouble();
        double u2 = 1.0 - rng.NextDouble();
        return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
    }
}

// Small SIMD kernels over float spans.
internal static class Vectors
{
    // y += scale * x
    public static void AddScaled(Span<float> y, ReadOnlySpan<float> x, float scale)
    {
        Span<Vector<float>> yv = MemoryMarshal.Cast<float, Vector<float>>(y);
        ReadOnlySpan<Vector<float>> xv = MemoryMarshal.Cast<float, Vector<float>>(x);
        var s = new Vector<float>(scale);
        for (int k = 0; k < xv.Length; k++)
            yv[k] += xv[k] * s;
        for (int i = xv.Length * Vector<float>.Count; i < x.Length; i++)
            y[i] += x[i] * scale;
    }

    public static float Dot(ReadOnlySpan<float> x, ReadOnlySpan<float> y)
    {
        ReadOnlySpan<Vector<float>> xv = MemoryMarshal.Cast<float, Vector<float>>(x);
        ReadOnlySpan<Vector<float>> yv = MemoryMarshal.Cast<float, Vector<float>>(y);
        Vector<float> sum = Vector<float>.Zero;
        for (int k = 0; k < xv.Length; k++)
            sum += xv[k] * yv[k];
        float result = Vector.Sum(sum);
        for (int i = xv.Length * Vector<float>.Count; i < x.Length; i++)
            result += x[i] * y[i];
        return result;
    }
}
