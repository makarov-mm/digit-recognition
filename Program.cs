namespace Makarov.NeuralNet3D;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        // Visual styles, text rendering and DPI awareness (per-monitor v2) from the project file.
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}
