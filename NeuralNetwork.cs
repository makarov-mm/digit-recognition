namespace NeuralNet3D;

public sealed class NeuralNetwork
{
    public readonly int[] Sizes;
    public readonly double[][][] W;
    public readonly double[][] B;
    public readonly double[][] A;
    private readonly double[][] _z;
    private readonly Random _rng = new(1234);

    public int LayerCount => Sizes.Length;

    public NeuralNetwork(params int[] sizes)
    {
        Sizes = sizes;
        int L = sizes.Length;
        W = new double[L - 1][][];
        B = new double[L - 1][];
        A = new double[L][];
        _z = new double[L][];

        A[0] = new double[sizes[0]];
        for (int l = 1; l < L; l++)
        {
            A[l] = new double[sizes[l]];
            _z[l] = new double[sizes[l]];
            B[l - 1] = new double[sizes[l]];
            W[l - 1] = new double[sizes[l]][];

            double scale = Math.Sqrt(2.0 / sizes[l - 1]);

            for (int j = 0; j < sizes[l]; j++)
            {
                W[l - 1][j] = new double[sizes[l - 1]];
                for (int i = 0; i < sizes[l - 1]; i++)
                    W[l - 1][j][i] = NextGaussian() * scale;
            }
        }
    }

    private double NextGaussian()
    {
        double u1 = 1.0 - _rng.NextDouble();
        double u2 = 1.0 - _rng.NextDouble();
        return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
    }

    public double[] Forward(double[] input)
    {
        Array.Copy(input, A[0], Sizes[0]);

        for (int l = 1; l < LayerCount; l++)
        {
            bool last = l == LayerCount - 1;
            double[] prev = A[l - 1], z = _z[l], a = A[l], b = B[l - 1];
            double[][] w = W[l - 1];

            for (int j = 0; j < Sizes[l]; j++)
            {
                double s = b[j];
                double[] wj = w[j];
                for (int i = 0; i < prev.Length; i++) s += wj[i] * prev[i];
                z[j] = s;
                if (!last) a[j] = s > 0 ? s : 0;
            }

            if (last)
            {
                double max = double.NegativeInfinity;
                for (int j = 0; j < Sizes[l]; j++) if (z[j] > max) max = z[j];
                double sum = 0;
                for (int j = 0; j < Sizes[l]; j++) { a[j] = Math.Exp(z[j] - max); sum += a[j]; }
                for (int j = 0; j < Sizes[l]; j++) a[j] /= sum;
            }
        }
        return A[LayerCount - 1];
    }

    public double TrainSample(double[] input, int label, double lr, double labelSmoothing = 0.0)
    {
        double[] output = Forward(input);
        int L = LayerCount;
        int K = Sizes[L - 1];
        double on = 1.0 - labelSmoothing + labelSmoothing / K;
        double off = labelSmoothing / K;
        double[] delta = new double[K];

        for (int j = 0; j < K; j++) 
            delta[j] = output[j] - (j == label ? on : off);

        double loss = -Math.Log(Math.Max(output[label], 1e-12));

        for (int l = L - 1; l >= 1; l--)
        {
            double[] prev = A[l - 1];
            double[][] w = W[l - 1];
            double[] b = B[l - 1];
            double[] deltaPrev = l > 1 ? new double[Sizes[l - 1]] : null;

            for (int j = 0; j < Sizes[l]; j++)
            {
                double d = delta[j];
                double[] wj = w[j];
                if (deltaPrev != null)
                    for (int i = 0; i < prev.Length; i++) deltaPrev[i] += wj[i] * d;
                for (int i = 0; i < prev.Length; i++) wj[i] -= lr * d * prev[i];
                b[j] -= lr * d;
            }

            if (deltaPrev != null)
            {
                double[] zPrev = _z[l - 1];

                for (int i = 0; i < deltaPrev.Length; i++)
                    if (zPrev[i] <= 0) deltaPrev[i] = 0;

                delta = deltaPrev;
            }
        }
        return loss;
    }

    public int Predict(double[] input)
    {
        double[] o = Forward(input);
        int best = 0;
        for (int j = 1; j < o.Length; j++) if (o[j] > o[best]) best = j;
        return best;
    }

    public void Save(string path)
    {
        using var bw = new BinaryWriter(File.Create(path));
        bw.Write(Sizes.Length);
        foreach (int s in Sizes) bw.Write(s);
        for (int l = 0; l < W.Length; l++)
            for (int j = 0; j < W[l].Length; j++)
            {
                bw.Write(B[l][j]);
                foreach (double v in W[l][j]) bw.Write(v);
            }
    }

    public bool TryLoad(string path)
    {
        if (!File.Exists(path)) return false;
        try
        {
            using var br = new BinaryReader(File.OpenRead(path));
            if (br.ReadInt32() != Sizes.Length) return false;
            for (int i = 0; i < Sizes.Length; i++) if (br.ReadInt32() != Sizes[i]) return false;
            for (int l = 0; l < W.Length; l++)
                for (int j = 0; j < W[l].Length; j++)
                {
                    B[l][j] = br.ReadDouble();
                    for (int i = 0; i < W[l][j].Length; i++) W[l][j][i] = br.ReadDouble();
                }
            return true;
        }
        catch { return false; }
    }
}
