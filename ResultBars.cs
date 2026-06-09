namespace Makarov.NeuralNet3D;

internal sealed class ResultBars : Control
{
    private double[] _values = new double[10];
    private int _winner = -1;

    public ResultBars()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
        BackColor = Color.FromArgb(28, 30, 40);
        ForeColor = Color.Gainsboro;
    }

    public void SetValues(double[] probs, int winner)
    {
        _values = probs;
        _winner = winner;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(BackColor);

        int n = 10;
        float pad = 6f;
        float rowH = (Height - pad) / n;
        float labelW = 22f;
        float barX = labelW + 4f;
        float barW = Width - barX - 46f;

        using var font = new Font("Segoe UI", 9f, FontStyle.Bold);
        using var dim = new SolidBrush(Color.FromArgb(55, 58, 72));

        for (int d = 0; d < n; d++)
        {
            float y = pad + d * rowH;
            float h = rowH - 5f;
            double v = (_values != null && d < _values.Length) ? _values[d] : 0;

            bool win = d == _winner;
            using var label = new SolidBrush(win ? Color.Gold : Color.Gainsboro);
            g.DrawString(d.ToString(), font, label, 4f, y + h / 2 - 8f);

            g.FillRectangle(dim, barX, y, barW, h);
            using var fill = new SolidBrush(win ? Color.FromArgb(255, 210, 60) : Color.FromArgb(70, 200, 170));
            g.FillRectangle(fill, barX, y, (float)(barW * Math.Clamp(v, 0, 1)), h);

            g.DrawString($"{v * 100:0.0}%", font, label, barX + barW + 4f, y + h / 2 - 8f);
        }
    }
}
