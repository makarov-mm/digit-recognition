using System.Drawing.Drawing2D;

namespace Makarov.NeuralNet3D;

internal sealed class DrawingPanel : Control
{
    private const int Res = 280;
    private readonly Bitmap _bmp = new(Res, Res);
    private readonly Graphics _g;
    private readonly Pen _pen;
    private bool _drawing;
    private Point _last;

    public event Action StrokeEnded;

    public DrawingPanel()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
        _g = Graphics.FromImage(_bmp);
        _g.SmoothingMode = SmoothingMode.AntiAlias;
        _pen = new Pen(Color.Black, 26f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        Clear();
    }

    public void Clear()
    {
        _g.Clear(Color.White);
        Invalidate();
    }

    public Bitmap Snapshot() => (Bitmap)_bmp.Clone();

    private Point ToBitmap(Point p)
    {
        float sx = Res / (float)Math.Max(1, ClientSize.Width);
        float sy = Res / (float)Math.Max(1, ClientSize.Height);
        return new Point((int)(p.X * sx), (int)(p.Y * sy));
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left) return;
        _drawing = true;
        _last = ToBitmap(e.Location);
        _g.FillEllipse(Brushes.Black, _last.X - 10, _last.Y - 10, 20, 20);
        Invalidate();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!_drawing) return;
        Point p = ToBitmap(e.Location);
        _g.DrawLine(_pen, _last, p);
        _last = p;
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (!_drawing) return;
        _drawing = false;
        StrokeEnded?.Invoke();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        e.Graphics.DrawImage(_bmp, ClientRectangle);
        using var border = new Pen(Color.FromArgb(90, 90, 110), 1);
        e.Graphics.DrawRectangle(border, 0, 0, ClientSize.Width - 1, ClientSize.Height - 1);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { _pen.Dispose(); _g.Dispose(); _bmp.Dispose(); }
        base.Dispose(disposing);
    }
}
