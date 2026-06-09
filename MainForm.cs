using System.Diagnostics;

namespace Makarov.NeuralNet3D
{
    internal sealed class MainForm : Form
    {
        private static readonly string AppDir = AppContext.BaseDirectory;
        private static readonly string WeightsPath = Path.Combine(AppDir, "weights.bin");

        private readonly NeuralNetwork _net;
        private readonly NetworkRenderer _renderer;
        private readonly DrawingPanel _drawing;
        private readonly ResultBars _bars;
        private readonly GlControl _gl;
        private readonly Button _btnClear;
        private readonly Button _btnRecognize;
        private readonly Label _bigDigit;
        private readonly Label _status;
        private readonly System.Windows.Forms.Timer _timer;
        private readonly Stopwatch _clock = new();
        private bool _dragging;
        private Point _lastMouse;
        private volatile bool _ready;

        public MainForm()
        {
            _net = new NeuralNetwork(784, 64, 32, 10);
            _renderer = new NetworkRenderer(_net);

            Text = "Digit Recognition";
            BackColor = Color.FromArgb(24, 24, 28);
            ForeColor = Color.Gainsboro;
            Width = 1200;
            Height = 800;
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(900, 600);

            var left = new Panel
            {
                Dock = DockStyle.Left,
                Width = 340,
                BackColor = Color.FromArgb(30, 30, 36),
                Padding = new Padding(16)
            };

            var title = new Label
            {
                Text = "Draw a digit (0–9)",
                Dock = DockStyle.Top,
                Height = 28,
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft
            };

            _drawing = new DrawingPanel
            {
                Location = new Point(16, 52),
                Size = new Size(300, 300),
                BackColor = Color.White
            };
            _drawing.StrokeEnded += () => Recognize();

            _drawing.MouseDown += (_, __) =>
            {
                _drawing.Clear();
            };

            _btnClear = new Button
            {
                Text = "Clear",
                Location = new Point(16, 362),
                Size = new Size(142, 34),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(54, 54, 64),
                ForeColor = Color.Gainsboro
            };

            _btnClear.Click += (_, __) =>
            {
                _drawing.Clear();
            };

            _btnRecognize = new Button
            {
                Text = "Recognize",
                Location = new Point(174, 362),
                Size = new Size(142, 34),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(58, 84, 140),
                ForeColor = Color.White,
                Enabled = false
            };
            _btnRecognize.Click += (_, __) => Recognize();

            var lblResult = new Label
            {
                Text = "Result:",
                Location = new Point(16, 410),
                AutoSize = true,
                Font = new Font("Segoe UI", 10f)
            };

            _bigDigit = new Label
            {
                Text = "—",
                Location = new Point(150, 404),
                Size = new Size(166, 86),
                Font = new Font("Segoe UI", 56f, FontStyle.Bold),
                ForeColor = Color.Gold,
                TextAlign = ContentAlignment.MiddleCenter
            };

            _bars = new ResultBars
            {
                Location = new Point(16, 498),
                Size = new Size(300, 200),
                BackColor = Color.FromArgb(30, 30, 36)
            };

            _status = new Label
            {
                Dock = DockStyle.Bottom,
                Height = 24,
                Text = "Initializing…",
                ForeColor = Color.DarkGray,
                TextAlign = ContentAlignment.MiddleLeft
            };

            left.Controls.Add(_bars);
            left.Controls.Add(_bigDigit);
            left.Controls.Add(lblResult);
            left.Controls.Add(_btnRecognize);
            left.Controls.Add(_btnClear);
            left.Controls.Add(_drawing);
            left.Controls.Add(title);
            left.Controls.Add(_status);

            _gl = new GlControl { Dock = DockStyle.Fill };
            _gl.GlInit += _renderer.InitGl;
            _gl.GlRender += (w, h) => _renderer.Render(w, h);

            _gl.MouseEnter += (_, __) => { if (_gl.CanFocus) _gl.Focus(); };
            _gl.MouseDown += (_, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    _dragging = true;
                    _lastMouse = e.Location;
                    _renderer.AutoRotate = false;
                }
            };
            _gl.MouseMove += (_, e) =>
            {
                if (!_dragging) return;
                _renderer.Yaw += (e.X - _lastMouse.X) * 0.4f;
                _renderer.Pitch += (e.Y - _lastMouse.Y) * 0.4f;
                if (_renderer.Pitch > 89f) _renderer.Pitch = 89f;
                if (_renderer.Pitch < -89f) _renderer.Pitch = -89f;
                _lastMouse = e.Location;
            };
            _gl.MouseUp += (_, e) => { if (e.Button == MouseButtons.Left) _dragging = false; };
            _gl.MouseDoubleClick += (_, __) => _renderer.AutoRotate = !_renderer.AutoRotate;
            _gl.MouseWheel += (_, e) =>
            {
                _renderer.Distance -= e.Delta * 0.01f;
                if (_renderer.Distance < 12f) _renderer.Distance = 12f;
                if (_renderer.Distance > 70f) _renderer.Distance = 70f;
            };

            Controls.Add(_gl);
            Controls.Add(left);

            _timer = new System.Windows.Forms.Timer { Interval = 16 };
            _timer.Tick += (_, __) =>
            {
                double dt = _clock.Elapsed.TotalSeconds;
                _clock.Restart();
                _renderer.Update(dt);
                _gl.Invalidate();
            };

            Load += (_, __) =>
            {
                _clock.Start();
                _timer.Start();
                StartTraining();
            };

            KeyUp += (object? sender, KeyEventArgs args) =>
            {
                if (args.KeyCode == Keys.Escape)
                {
                    _drawing.Clear();
                }
            };
        }

        private void StartTraining()
        {
            Task.Run(() =>
            {
                try
                {
                    if (File.Exists(WeightsPath) && _net.TryLoad(WeightsPath))
                    {
                        SetStatus("Loaded cached weights.");
                    }
                    else
                    {
                        List<(double[] x, int y)> train, test = null;
                        bool real = false;

                        try
                        {
                            SetStatus("Downloading MNIST …");
                            MnistLoader.EnsureDownloaded("mnist", SetStatus);
                            SetStatus("Loading MNIST …");
                            train = MnistLoader.Load("mnist", test: false);
                            test = MnistLoader.Load("mnist", test: true);
                            real = true;
                        }
                        catch (Exception ex)
                        {
                            SetStatus("MNIST unavailable (" + ex.Message + ") — using synthetic data.");
                            const int perDigit = 350;
                            int total = perDigit * 10;
                            train = ImageOps.GenerateDataset(perDigit,
                                done => SetStatus($"Generating dataset … {done}/{total}"));
                        }

                        var rng = new Random(12345);
                        int epochs = real ? 15 : 14;
                        double lr = 0.05;

                        for (int ep = 0; ep < epochs; ep++)
                        {
                            for (int i = train.Count - 1; i > 0; i--)
                            {
                                int j = rng.Next(i + 1);
                                (train[i], train[j]) = (train[j], train[i]);
                            }

                            double loss = 0;
                            foreach (var (x, y) in train)
                                loss += _net.TrainSample(x, y, lr, labelSmoothing: 0.05);
                            loss /= train.Count;

                            SetStatus($"Training … epoch {ep + 1}/{epochs}, loss={loss:F3}");
                            lr *= 0.93;
                        }

                        if (real && test != null)
                        {
                            int correct = 0;
                            foreach (var (x, y) in test)
                                if (_net.Predict(x) == y) correct++;
                            double acc = 100.0 * correct / test.Count;
                            SetStatus($"Trained on MNIST — test accuracy {acc:F1}%.");
                        }

                        try { _net.Save(WeightsPath); } catch { }
                    }

                    BeginInvoke(() =>
                    {
                        _ready = true;
                        _renderer.Ready = true;
                        _btnRecognize.Enabled = true;
                        if (!_status.Text.StartsWith("Trained") && !_status.Text.StartsWith("Loaded"))
                            _status.Text = "Ready. Draw a digit.";
                        else
                            _status.Text += "  Draw a digit.";
                    });
                }
                catch (Exception ex)
                {
                    SetStatus("Training error: " + ex.Message);
                }
            });
        }

        private void SetStatus(string text)
        {
            if (IsHandleCreated)
                BeginInvoke(() => _status.Text = text);
        }

        private void Recognize()
        {
            if (!_ready) return;

            using var snap = _drawing.Snapshot();
            double[] x = ImageOps.Preprocess(snap);
            double[] probs = _net.Forward(x);

            int winner = 0;
            for (int i = 1; i < probs.Length; i++)
                if (probs[i] > probs[winner]) winner = i;

            _bigDigit.Text = winner.ToString();
            _bars.SetValues(probs, winner);

            _renderer.SetResult(probs, winner);
            _renderer.StartWave();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _timer.Stop();
            base.OnFormClosed(e);
        }
    }
}
