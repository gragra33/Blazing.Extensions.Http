namespace WinFormsExample;

/// <summary>
/// A custom statistics panel with card-style display.
/// </summary>
internal sealed class StatisticsPanelCustom : UserControl
{
    private readonly List<(string Title, string Value)> _stats = new();
    private readonly int _cardWidth = 185;
    private readonly int _cardHeight = 55;
    private readonly int _cardMargin = 10;
    private readonly Font _titleFont = new Font("Segoe UI", 8F, FontStyle.Regular);
    private readonly Font _valueFont = new Font("Segoe UI", 11F, FontStyle.Bold);
    private readonly Brush _titleBrush = new SolidBrush(Color.Gray);
    private readonly Brush _valueBrush = new SolidBrush(Color.Black);
    private readonly Brush _cardBrush = new SolidBrush(Color.FromArgb(240, 240, 240));
    private readonly Pen _borderPen = new Pen(Color.FromArgb(200, 200, 200)); // Gray border like WPF

    private static string FormatByteRate(double bytesPerSecond)
    {
        string[] units = { "B", "KiB", "MiB", "GiB", "TiB" };
        int unitIndex = 0;
        while (bytesPerSecond >= 1024 && unitIndex < units.Length - 1)
        {
            bytesPerSecond /= 1024;
            unitIndex++;
        }
        return $"{bytesPerSecond:N2} {units[unitIndex]}/s";
    }

    public StatisticsPanelCustom()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
        UpdateStats([
            ("Total Downloads", "0"),
            ("Active", "0"),
            ("Completed", "0"),
            ("Failed", "0"),
            ("Total Bytes", "0 B"),
            ("Overall Speed", "0.00 MiB/s"),
            ("Avg Latency", "0.00 ms"),
            ("Total Elapsed", "0.0s")
        ]);
    }

    /// <summary>
    /// Updates the displayed statistics.
    /// </summary>
    /// <param name="stats">The statistics to display.</param>
    public void UpdateStats(IEnumerable<(string Title, string Value)> stats)
    {
        ArgumentNullException.ThrowIfNull(stats);
        
        _stats.Clear();
        foreach (var stat in stats)
        {
            if (stat.Title == "Overall Speed")
            {
                string value = stat.Value;
                // If value is a raw number or ends with B/s, format as MiB/s
                if (double.TryParse(value.Replace("B/s", "", StringComparison.InvariantCulture).Trim(), out double bytesPerSec))
                {
                    value = FormatByteRate(bytesPerSec);
                }
                _stats.Add((stat.Title, value));
            }
            else
            {
                _stats.Add(stat);
            }
        }
        Invalidate();
        PerformLayout();
    }

    /// <summary>
    /// Raises the <see cref="Control.Paint"/> event.
    /// </summary>
    /// <param name="e">A <see cref="PaintEventArgs"/> that contains the event data.</param>
    protected override void OnPaint(PaintEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnPaint(e);
        var g = e.Graphics;
        g.Clear(BackColor);

        // Draw outer border (gray, like WPF)
        using (var borderPen = new Pen(Color.FromArgb(200, 200, 200), 1))
        {
            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            g.DrawRectangle(borderPen, rect);
        }

        int availableWidth = ClientSize.Width - _cardMargin;
        int cardsPerRow = Math.Max(1, availableWidth / (_cardWidth + _cardMargin));
        int totalCards = _stats.Count;
        int rows = (totalCards + cardsPerRow - 1) / cardsPerRow;

        int y = _cardMargin;
        int statIndex = 0;
        for (int row = 0; row < rows; row++)
        {
            int cardsThisRow = Math.Min(cardsPerRow, totalCards - statIndex);
            int rowWidth = cardsThisRow * _cardWidth + (cardsThisRow - 1) * _cardMargin;
            int startX = (ClientSize.Width - rowWidth) / 2;
            int x = startX;
            for (int col = 0; col < cardsThisRow && statIndex < totalCards; col++, statIndex++)
            {
                var (title, value) = _stats[statIndex];
                var rect = new Rectangle(x, y, _cardWidth, _cardHeight);
                g.FillRectangle(_cardBrush, rect);
                g.DrawRectangle(_borderPen, rect);
                SizeF titleSize = g.MeasureString(title, _titleFont);
                SizeF valueSize = g.MeasureString(value, _valueFont);
                float titleX = x + (_cardWidth - titleSize.Width) / 2f;
                float titleY = y + (_cardHeight / 2f - titleSize.Height);
                float valueX = x + (_cardWidth - valueSize.Width) / 2f;
                float valueY = y + _cardHeight / 2f;
                g.DrawString(title, _titleFont, _titleBrush, titleX, titleY);
                g.DrawString(value, _valueFont, _valueBrush, valueX, valueY);
                x += _cardWidth + _cardMargin;
            }
            y += _cardHeight + _cardMargin;
        }
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        int availableWidth = ClientSize.Width - _cardMargin;
        int cardsPerRow = Math.Max(1, availableWidth / (_cardWidth + _cardMargin));
        int rows = (_stats.Count + cardsPerRow - 1) / cardsPerRow;
        int newHeight = rows * (_cardHeight + _cardMargin) + _cardMargin;
        if (Height != newHeight)
            Height = newHeight;
        Invalidate();
        PerformLayout();
    }

    /// <summary>
    /// Releases the unmanaged resources used by the control and optionally releases the managed resources.
    /// </summary>
    /// <param name="disposing">true to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _titleFont?.Dispose();
            _valueFont?.Dispose();
            _titleBrush?.Dispose();
            _valueBrush?.Dispose();
            _cardBrush?.Dispose();
            _borderPen?.Dispose();
        }
        base.Dispose(disposing);
    }
}