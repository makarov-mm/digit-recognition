using System.Diagnostics;
using System.Globalization;

namespace Makarov.NeuralNet3D;

// Left: a canvas to draw a digit on, the recognised digit and the probability of each.
// Right: the network in 3D, lighting up as the digit passes through it.
internal sealed class MainForm : Form
{
    private const string NoDigit = "—";

    private static readonly string DataDirectory =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NeuralNet3D");

    private readonly NeuralNetwork _network = new(Dataset.PixelCount, 64, 32, 10);
    private readonly NetworkRenderer _renderer;
    private readonly DrawingPanel _canvas;
    private readonly Button _recognizeButton;
    private readonly Label _digitLabel;
    private readonly ResultBars _bars;
    private readonly Label _statusLabel;
    private readonly GlControl _view;
    private readonly Font _titleFont = new("Segoe UI", 11f, FontStyle.Bold);
    private readonly Font _resultFont = new("Segoe UI", 10f);
    private readonly Font _digitFont = new("Segoe UI", 56f, FontStyle.Bold);
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 16 };
    private readonly Stopwatch _clock = new();
    private readonly CancellationTokenSource _closing = new();
    private bool _ready;
    private bool _dragging;
    private Point _lastMouse;

    public MainForm()
    {
        _renderer = new NetworkRenderer(_network);

        SuspendLayout();
        // Coordinates below are 96-DPI pixels; WinForms scales them to the monitor.
        AutoScaleDimensions = new SizeF(96f, 96f);
        AutoScaleMode = AutoScaleMode.Dpi;
        Text = "Digit Recognition";
        BackColor = Color.FromArgb(24, 24, 28);
        ForeColor = Color.Gainsboro;
        ClientSize = new Size(1184, 761);
        MinimumSize = new Size(900, 600);
        StartPosition = FormStartPosition.CenterScreen;

        var title = new Label
        {
            Text = "Draw a digit (0–9)",
            Dock = DockStyle.Top,
            Height = 28,
            Font = _titleFont,
            TextAlign = ContentAlignment.MiddleLeft,
        };
        _canvas = new DrawingPanel { Location = new Point(16, 52), Size = new Size(300, 300), BackColor = Color.White };
        Button clearButton = CreateButton("Clear", new Point(16, 362), Color.FromArgb(54, 54, 64), Color.Gainsboro);
        _recognizeButton = CreateButton("Recognize", new Point(174, 362), Color.FromArgb(58, 84, 140), Color.White);
        _recognizeButton.Enabled = false;
        var resultLabel = new Label { Text = "Result:", Location = new Point(16, 410), AutoSize = true, Font = _resultFont };
        _digitLabel = new Label
        {
            Text = NoDigit,
            Location = new Point(150, 404),
            Size = new Size(166, 86),
            Font = _digitFont,
            ForeColor = Color.Gold,
            TextAlign = ContentAlignment.MiddleCenter,
        };
        _bars = new ResultBars { Location = new Point(16, 498), Size = new Size(300, 200), BackColor = Color.FromArgb(30, 30, 36) };
        _statusLabel = new Label
        {
            Text = "Initializing…",
            Dock = DockStyle.Bottom,
            Height = 24,
            ForeColor = Color.DarkGray,
            TextAlign = ContentAlignment.MiddleLeft,
        };

        var leftPanel = new Panel
        {
            Dock = DockStyle.Left,
            Width = 340,
            BackColor = Color.FromArgb(30, 30, 36),
            Padding = new Padding(16),
        };
        leftPanel.Controls.AddRange([_bars, _digitLabel, resultLabel, _recognizeButton, clearButton, _canvas, title, _statusLabel]);

        _view = new GlControl { Dock = DockStyle.Fill };
        Controls.Add(_view);  // added first: it fills what the left panel leaves
        Controls.Add(leftPanel);
        CancelButton = clearButton;  // Esc clears the canvas
        ResumeLayout(false);
        PerformLayout();

        _canvas.StrokeEnded += Recognize;
        clearButton.Click += (_, _) => ClearDrawing();
        _recognizeButton.Click += (_, _) => Recognize();

        _view.ContextCreated += _renderer.InitGl;
        _view.RenderFrame += (width, height) => _renderer.Render(width, height, _view.DeviceDpi / 96f);
        _view.MouseEnter += (_, _) => _view.Focus();  // so the wheel reaches it
        _view.MouseDown += OnViewMouseDown;
        _view.MouseMove += OnViewMouseMove;
        _view.MouseUp += (_, e) =>
        {
            if (e.Button == MouseButtons.Left) _dragging = false;
        };
        _view.MouseCaptureChanged += (_, _) => _dragging = false;
        _view.MouseDoubleClick += (_, _) => _renderer.AutoRotate = true;
        _view.MouseWheel += (_, e) => _renderer.Zoom(e.Delta * 0.01f);

        _timer.Tick += OnTick;
    }

    protected override async void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        // Scaled for a high-DPI monitor, the design size can exceed a small screen.
        Rectangle area = Screen.FromControl(this).WorkingArea;
        if (Width > area.Width || Height > area.Height)
        {
            Size = new Size(Math.Min(Width, area.Width), Math.Min(Height, area.Height));
            CenterToScreen();
        }

        _clock.Start();
        _timer.Start();
        await PrepareNetworkAsync();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _closing.Cancel();  // stops a download or training still in progress
        _timer.Stop();
        base.OnFormClosed(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _timer.Dispose();
            _closing.Dispose();
            _titleFont.Dispose();
            _resultFont.Dispose();
            _digitFont.Dispose();
        }
        base.Dispose(disposing);
    }

    private static Button CreateButton(string text, Point location, Color background, Color foreground) => new()
    {
        Text = text,
        Location = location,
        Size = new Size(142, 34),
        FlatStyle = FlatStyle.Flat,
        BackColor = background,
        ForeColor = foreground,
    };

    // Loads or trains the network in the background, reporting progress in the status bar.
    private async Task PrepareNetworkAsync()
    {
        var progress = new Progress<string>(text =>
        {
            if (!IsDisposed) _statusLabel.Text = text;
        });
        string summary;
        try
        {
            summary = await Task.Run(() => Training.PrepareAsync(_network, DataDirectory, progress, _closing.Token));
        }
        catch (OperationCanceledException) when (_closing.IsCancellationRequested)
        {
            return;  // the window was closed
        }
        catch (Exception ex)
        {
            _statusLabel.Text = "Training failed: " + ex.Message;
            return;
        }

        _ready = true;
        _recognizeButton.Enabled = true;
        _statusLabel.Text = summary + "  Draw a digit.";
        if (_canvas.HasInk) Recognize();  // drawn while the network was still training
    }

    private void Recognize()
    {
        if (!_ready) return;
        if (!_canvas.HasInk)
        {
            ShowNoResult();
            return;
        }

        float[] input;
        using (Bitmap snapshot = _canvas.Snapshot())
            input = DigitPreprocessor.FromBitmap(snapshot);
        ReadOnlySpan<float> probabilities = _network.Forward(input);
        int digit = NeuralNetwork.ArgMax(probabilities);

        _digitLabel.Text = digit.ToString(CultureInfo.InvariantCulture);
        _bars.SetValues(probabilities, digit);
        _renderer.ShowResult(digit);
    }

    private void ClearDrawing()
    {
        _canvas.Clear();
        ShowNoResult();
    }

    private void ShowNoResult()
    {
        _digitLabel.Text = NoDigit;
        _bars.Clear();
        _renderer.ClearResult();
    }

    private void OnViewMouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;
        _dragging = true;
        _lastMouse = e.Location;
        _renderer.AutoRotate = false;
    }

    private void OnViewMouseMove(object? sender, MouseEventArgs e)
    {
        if (!_dragging) return;
        float degreesPerPixel = 0.4f * 96f / _view.DeviceDpi;
        _renderer.Rotate((e.X - _lastMouse.X) * degreesPerPixel, (e.Y - _lastMouse.Y) * degreesPerPixel);
        _lastMouse = e.Location;
    }

    private void OnTick(object? sender, EventArgs e)
    {
        double seconds = _clock.Elapsed.TotalSeconds;
        _clock.Restart();
        _renderer.Update(seconds);
        if (WindowState != FormWindowState.Minimized)
            _view.Invalidate();
    }
}
