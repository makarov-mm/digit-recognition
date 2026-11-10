using System.Globalization;

namespace Makarov.NeuralNet3D;

// The probability of every digit as a horizontal bar, the winner in gold.
internal sealed class ResultBars : Control
{
    private const TextFormatFlags TextFlags =
        TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding;

    private static readonly Color TrackColor = Color.FromArgb(55, 58, 72);
    private static readonly Color BarColor = Color.FromArgb(70, 200, 170);
    private static readonly Color WinnerBarColor = Color.FromArgb(255, 210, 60);

    private readonly Font _font = new("Segoe UI", 9f, FontStyle.Bold);
    private readonly float[] _values = new float[10];
    private int _winner = -1;

    public ResultBars()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        BackColor = Color.FromArgb(28, 30, 40);
        ForeColor = Color.Gainsboro;
        Font = _font;
    }

    public void SetValues(ReadOnlySpan<float> probabilities, int winner)
    {
        probabilities[.._values.Length].CopyTo(_values);
        _winner = winner;
        Invalidate();
    }

    public void Clear()
    {
        Array.Clear(_values);
        _winner = -1;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.Clear(BackColor);

        // Columns are measured from the font, so nothing clips at higher DPI.
        int gap = LogicalToDeviceUnits(4);
        int top = LogicalToDeviceUnits(6);
        int labelWidth = TextRenderer.MeasureText(g, "0", Font, Size.Empty, TextFlags).Width;
        int percentWidth = TextRenderer.MeasureText(g, "100.0%", Font, Size.Empty, TextFlags).Width;
        int barLeft = gap + labelWidth + gap;
        int barWidth = Math.Max(1, ClientSize.Width - barLeft - gap - percentWidth);
        float rowHeight = (ClientSize.Height - top) / (float)_values.Length;
        float barHeight = Math.Max(1f, rowHeight - LogicalToDeviceUnits(5));

        using var track = new SolidBrush(TrackColor);
        using var bar = new SolidBrush(BarColor);
        using var winnerBar = new SolidBrush(WinnerBarColor);
        for (int digit = 0; digit < _values.Length; digit++)
        {
            bool winner = digit == _winner;
            float y = top + digit * rowHeight;
            float value = Math.Clamp(_values[digit], 0f, 1f);
            Color textColor = winner ? Color.Gold : ForeColor;
            int textTop = (int)y, textHeight = (int)Math.Ceiling(barHeight);

            TextRenderer.DrawText(g, digit.ToString(CultureInfo.InvariantCulture), Font,
                new Rectangle(gap, textTop, labelWidth, textHeight), textColor, TextFlags);
            g.FillRectangle(track, barLeft, y, barWidth, barHeight);
            g.FillRectangle(winner ? winnerBar : bar, barLeft, y, barWidth * value, barHeight);
            TextRenderer.DrawText(g, $"{value * 100:0.0}%", Font,
                new Rectangle(barLeft + barWidth + gap, textTop, percentWidth, textHeight), textColor, TextFlags);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _font.Dispose();
        base.Dispose(disposing);
    }
}
