using System.Drawing.Drawing2D;

namespace Makarov.NeuralNet3D;

// A canvas for drawing digits with the mouse. Strokes go into a fixed-resolution bitmap
// that is scaled to the control, so the ink looks the same at any size or DPI.
internal sealed class DrawingPanel : Control
{
    private const int CanvasSize = 280;
    private const float PenWidth = 26f;

    private readonly Bitmap _canvas = new(CanvasSize, CanvasSize);
    private readonly Graphics _graphics;
    private readonly Pen _pen = new(Color.Black, PenWidth)
    {
        StartCap = LineCap.Round,
        EndCap = LineCap.Round,
        LineJoin = LineJoin.Round,
    };
    private bool _drawing;
    private Point _last;

    public DrawingPanel()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
        _graphics = Graphics.FromImage(_canvas);
        _graphics.SmoothingMode = SmoothingMode.AntiAlias;
        Clear();
    }

    // Raised when the mouse button is released (or the stroke is otherwise cut off).
    public event Action? StrokeEnded;

    public bool HasInk { get; private set; }

    public void Clear()
    {
        _graphics.Clear(Color.White);
        HasInk = false;
        Invalidate();
    }

    public Bitmap Snapshot() => (Bitmap)_canvas.Clone();

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left) return;

        _drawing = true;
        HasInk = true;
        _last = ToCanvas(e.Location);
        // A click leaves a dot as wide as the stroke.
        _graphics.FillEllipse(Brushes.Black, _last.X - PenWidth / 2, _last.Y - PenWidth / 2, PenWidth, PenWidth);
        Invalidate();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!_drawing) return;

        Point p = ToCanvas(e.Location);
        _graphics.DrawLine(_pen, _last, p);
        _last = p;
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        EndStroke();
    }

    // Losing the mouse mid-stroke (Alt+Tab, a message box) must end the stroke too.
    protected override void OnMouseCaptureChanged(EventArgs e)
    {
        base.OnMouseCaptureChanged(e);
        EndStroke();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        e.Graphics.DrawImage(_canvas, ClientRectangle);
        using var border = new Pen(Color.FromArgb(90, 90, 110));
        e.Graphics.DrawRectangle(border, 0, 0, ClientSize.Width - 1, ClientSize.Height - 1);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _pen.Dispose();
            _graphics.Dispose();
            _canvas.Dispose();
        }
        base.Dispose(disposing);
    }

    private void EndStroke()
    {
        if (!_drawing) return;
        _drawing = false;
        StrokeEnded?.Invoke();
    }

    private Point ToCanvas(Point p) => new(
        (int)(p.X * (float)CanvasSize / Math.Max(1, ClientSize.Width)),
        (int)(p.Y * (float)CanvasSize / Math.Max(1, ClientSize.Height)));
}
