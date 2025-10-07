using Blazing.Extensions.DependencyInjection;
using Blazing.Extensions.Http.Models;
using Microsoft.Extensions.DependencyInjection;

namespace WinFormsExample;

/// <summary>
/// Panel that displays overall performance statistics across all downloads.
/// </summary>
[AutoRegister(ServiceLifetime.Transient)]
internal sealed class StatisticsPanel : UserControl
{
    private FlowLayoutPanel _statsPanel = null!;
    private Label _totalDownloadsValueLabel = null!;
    private Label _totalBytesValueLabel = null!;
    private Label _overallSpeedValueLabel = null!;
    private Label _averageLatencyValueLabel = null!;
    private Label _totalElapsedValueLabel = null!;
    private Label _activeDownloadsValueLabel = null!;
    private Label _completedDownloadsValueLabel = null!;
    private Label _failedDownloadsValueLabel = null!;

    private int _totalDownloads;
    private long _totalBytes;
    private double _totalSpeed;
    private double _totalLatency;
    private int _latencyCount;
    private DateTime _startTime;
    private int _activeDownloads;
    private int _completedDownloads;
    private int _failedDownloads;

    public StatisticsPanel()
    {
        // Enable autosizing for the panel itself
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        InitializeComponent();
        InitializeStatCards();
    }

    private void InitializeComponent()
    {
        _statsPanel = new FlowLayoutPanel();
        SuspendLayout();
        // 
        // _statsPanel
        // 
        _statsPanel.Dock = DockStyle.Fill; // Fill parent
        _statsPanel.AutoSize = false; // Disable AutoSize for docking
        _statsPanel.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        _statsPanel.BackColor = Color.White;
        _statsPanel.Location = new Point(0, 0);
        _statsPanel.Name = "_statsPanel";
        _statsPanel.Padding = new Padding(5);
        _statsPanel.TabIndex = 0;
        _statsPanel.WrapContents = true;
        _statsPanel.SizeChanged += StatsPanel_SizeChanged;
        // 
        // StatisticsPanel
        // 
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        Controls.Add(_statsPanel);
        Name = "StatisticsPanel";
        Size = new Size(780, 140);
        Load += StatisticsPanel_Load;
        Resize += StatisticsPanel_Resize;
        ResumeLayout(false);
    }

    private void StatsPanel_SizeChanged(object? sender, EventArgs e)
    {
        // Make the StatisticsPanel height match the FlowLayoutPanel height
        Height = _statsPanel.Height;
        // Make the width match the parent (form or container)
        if (Parent != null)
        {
            Width = Parent.ClientSize.Width;
            _statsPanel.Width = Width;
        }
        // Optionally, force parent to layout again if needed
        Parent?.PerformLayout();
    }

    private void InitializeStatCards()
    {
        // Create stat cards - matching WPF order
        _totalDownloadsValueLabel = CreateStatCard("Total Downloads", "0");
        _activeDownloadsValueLabel = CreateStatCard("Active", "0");
        _completedDownloadsValueLabel = CreateStatCard("Completed", "0");
        _failedDownloadsValueLabel = CreateStatCard("Failed", "0");
        _totalBytesValueLabel = CreateStatCard("Total Bytes", "0 B");
        _overallSpeedValueLabel = CreateStatCard("Overall Speed", "0.00 B/s");
        _averageLatencyValueLabel = CreateStatCard("Avg Latency", "0.00 ms");
        _totalElapsedValueLabel = CreateStatCard("Total Elapsed", "0.0s");

        _statsPanel.Controls.Add(_totalDownloadsValueLabel.Parent!);
        _statsPanel.Controls.Add(_activeDownloadsValueLabel.Parent!);
        _statsPanel.Controls.Add(_completedDownloadsValueLabel.Parent!);
        _statsPanel.Controls.Add(_failedDownloadsValueLabel.Parent!);
        _statsPanel.Controls.Add(_totalBytesValueLabel.Parent!);
        _statsPanel.Controls.Add(_overallSpeedValueLabel.Parent!);
        _statsPanel.Controls.Add(_averageLatencyValueLabel.Parent!);
        _statsPanel.Controls.Add(_totalElapsedValueLabel.Parent!);
    }

    private void StatisticsPanel_Load(object? sender, EventArgs e)
    {
        // Initial layout calculation
        StatisticsPanel_Resize(this, EventArgs.Empty);
    }

    private void StatisticsPanel_Resize(object? sender, EventArgs e)
    {
        // Always match parent width
        if (Parent != null)
        {
            Width = Parent.ClientSize.Width;
            _statsPanel.Width = Width;
        }
        // Center the flow panel horizontally and calculate required height
        if (_statsPanel is { Controls.Count: > 0 })
        {
            // Card dimensions
            int cardWidth = 185;
            int cardHeight = 55;
            int cardMarginH = 10; // 5px on each side horizontally
            int cardMarginV = 10; // 5px on each side vertically
            int cardTotalWidth = cardWidth + cardMarginH;
            int cardTotalHeight = cardHeight + cardMarginV;
            
            // Calculate how many cards fit per row
            int availableWidth = Width - 10; // Account for padding
            int cardsPerRow = Math.Max(1, availableWidth / cardTotalWidth);
            
            // Calculate required rows
            int totalCards = _statsPanel.Controls.Count;
            int totalRows = (int)Math.Ceiling((double)totalCards / cardsPerRow);
            
            // Calculate required height
            int requiredHeight = totalRows * cardTotalHeight + 10; // Add padding
            
            // Center the content horizontally
            int totalWidthNeeded = cardTotalWidth * Math.Min(cardsPerRow, totalCards);
            int leftMargin = Math.Max(5, (Width - totalWidthNeeded) / 2);
            _statsPanel.Padding = new Padding(leftMargin, 5, 5, 5);
            
            // Update the height if needed
            if (Height != requiredHeight)
            {
                Height = requiredHeight;
            }
        }
    }

    private static Label CreateStatCard(string title, string value)
    {
        Panel card = new Panel
        {
            Width = 185,
            Height = 55,
            BorderStyle = BorderStyle.FixedSingle,
            Margin = new Padding(5),
            BackColor = Color.FromArgb(240, 240, 240)
        };

        var titleLabel = new Label
        {
            Text = title,
            Location = new Point(8, 8),
            Size = new Size(169, 15),
            Font = new Font("Segoe UI", 8F, FontStyle.Regular),
            ForeColor = Color.Gray
        };

        var valueLabel = new Label
        {
            Text = value,
            Location = new Point(8, 28),
            Size = new Size(169, 20),
            Font = new Font("Segoe UI", 11F, FontStyle.Bold),
            Tag = title // Store title for later reference
        };

        card.Controls.Add(titleLabel);
        card.Controls.Add(valueLabel);

        return valueLabel;
    }

    public void Reset()
    {
        if (InvokeRequired)
        {
            Invoke(Reset);
            return;
        }

        _totalDownloads = 0;
        _totalBytes = 0;
        _totalSpeed = 0;
        _totalLatency = 0;
        _latencyCount = 0;
        _startTime = DateTime.Now;
        _activeDownloads = 0;
        _completedDownloads = 0;
        _failedDownloads = 0;

        UpdateDisplay();
    }

    public void SetTotalDownloads(int count)
    {
        _totalDownloads = count;
        _activeDownloads = count;
        _startTime = DateTime.Now;
        UpdateDisplay();
    }

    /// <summary>
    /// Updates the statistics with the current transfer state.
    /// </summary>
    /// <param name="state">The current transfer state.</param>
    public void UpdateStats(TransferState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        
        if (InvokeRequired)
        {
            Invoke(() => UpdateStats(state));
            return;
        }

        _totalBytes = Math.Max(_totalBytes, state.Total.Transferred);
        _totalSpeed = state.Chunk.RawSpeed;

        if (state.Latency is { PacketCount: > 0 })
        {
            _totalLatency += state.Latency.PacketAvgMs;
            _latencyCount++;
        }

        UpdateDisplay();
    }

    public void MarkDownloadComplete()
    {
        if (InvokeRequired)
        {
            Invoke(MarkDownloadComplete);
            return;
        }

        _activeDownloads--;
        _completedDownloads++;
        UpdateDisplay();
    }

    public void MarkDownloadFailed()
    {
        if (InvokeRequired)
        {
            Invoke(MarkDownloadFailed);
            return;
        }

        _activeDownloads--;
        _failedDownloads++;
        UpdateDisplay();
    }

    private void UpdateDisplay()
    {
        if (InvokeRequired)
        {
            Invoke(UpdateDisplay);
            return;
        }

        _totalDownloadsValueLabel.Text = _totalDownloads.ToString(System.Globalization.CultureInfo.InvariantCulture);
        _activeDownloadsValueLabel.Text = _activeDownloads.ToString(System.Globalization.CultureInfo.InvariantCulture);
        _completedDownloadsValueLabel.Text = _completedDownloads.ToString(System.Globalization.CultureInfo.InvariantCulture);
        _failedDownloadsValueLabel.Text = _failedDownloads.ToString(System.Globalization.CultureInfo.InvariantCulture);

        // Format total bytes
        double bytes = _totalBytes;
        string[] units = { "B", "KiB", "MiB", "GiB", "TiB" };
        int unitIndex = 0;
        while (bytes >= 1024 && unitIndex < units.Length - 1)
        {
            bytes /= 1024;
            unitIndex++;
        }
        _totalBytesValueLabel.Text = $@"{bytes:N2} {units[unitIndex]}";

        // Format speed
        double speed = _totalSpeed;
        unitIndex = 0;
        while (speed >= 1024 && unitIndex < units.Length - 1)
        {
            speed /= 1024;
            unitIndex++;
        }
        _overallSpeedValueLabel.Text = $@"{speed:N2} {units[unitIndex]}/s";

        // Average latency
        if (_latencyCount > 0)
        {
            _averageLatencyValueLabel.Text = $@"{_totalLatency / _latencyCount:N2} ms";
        }

        // Total elapsed
        var elapsed = DateTime.Now - _startTime;
        _totalElapsedValueLabel.Text = $@"{elapsed.TotalSeconds:N1}s";
    }

    /// <summary>
    /// Releases the unmanaged resources used by the control and optionally releases the managed resources.
    /// </summary>
    /// <param name="disposing">true to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _statsPanel?.Dispose();
            _totalDownloadsValueLabel?.Dispose();
            _totalBytesValueLabel?.Dispose();
            _overallSpeedValueLabel?.Dispose();
            _averageLatencyValueLabel?.Dispose();
            _totalElapsedValueLabel?.Dispose();
            _activeDownloadsValueLabel?.Dispose();
            _completedDownloadsValueLabel?.Dispose();
            _failedDownloadsValueLabel?.Dispose();
        }
        base.Dispose(disposing);
    }
}
