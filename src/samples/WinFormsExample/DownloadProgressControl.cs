using Blazing.Extensions.DependencyInjection;
using Blazing.Extensions.Http.Models;
using Microsoft.Extensions.DependencyInjection;

namespace WinFormsExample;

/// <summary>
/// Control that displays detailed progress for a single download operation.
/// </summary>
[AutoRegister(ServiceLifetime.Transient)]
internal sealed class DownloadProgressControl : UserControl
{
    private Panel _mainPanel = null!;
    private Label _fileNameLabel = null!;
    private ProgressBar _progressBar = null!;
    private Button _cancelButton = null!;
    private TableLayoutPanel _statsTable = null!;

    private CancellationTokenSource? _cancellationTokenSource;

    public DownloadProgressControl()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        _mainPanel = new Panel();
        _fileNameLabel = new Label();
        _cancelButton = new Button();
        _progressBar = new ProgressBar();
        _statsTable = new TableLayoutPanel();
        
        _mainPanel.SuspendLayout();
        SuspendLayout();
        // 
        // _mainPanel
        // 
        _mainPanel.BackColor = Color.White;
        _mainPanel.BorderStyle = BorderStyle.None;
        _mainPanel.Controls.Add(_fileNameLabel);
        _mainPanel.Controls.Add(_cancelButton);
        _mainPanel.Controls.Add(_progressBar);
        _mainPanel.Controls.Add(_statsTable);
        _mainPanel.Dock = DockStyle.Fill;
        _mainPanel.Location = new Point(0, 0);
        _mainPanel.Name = "_mainPanel";
        _mainPanel.Padding = new Padding(16, 12, 16, 12);
        _mainPanel.Size = new Size(780, 175);
        _mainPanel.TabIndex = 0;
        _mainPanel.Paint += MainPanel_Paint;
        // 
        // _fileNameLabel
        // 
        _fileNameLabel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        _fileNameLabel.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
        _fileNameLabel.Location = new Point(16, 12);
        _fileNameLabel.Name = "_fileNameLabel";
        _fileNameLabel.Size = new Size(652, 25);
        _fileNameLabel.TabIndex = 0;
        _fileNameLabel.Text = "Download #1";
        _fileNameLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // _cancelButton
        // 
        _cancelButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _cancelButton.BackColor = Color.FromArgb(232, 17, 35);
        _cancelButton.Cursor = Cursors.Hand;
        _cancelButton.FlatAppearance.BorderSize = 0;
        _cancelButton.FlatStyle = FlatStyle.Flat;
        _cancelButton.Font = new Font("Segoe UI", 9F);
        _cancelButton.ForeColor = Color.White;
        _cancelButton.Location = new Point(664, 12);
        _cancelButton.Name = "_cancelButton";
        _cancelButton.Size = new Size(100, 25);
        _cancelButton.TabIndex = 1;
        _cancelButton.Text = "Cancel";
        _cancelButton.UseVisualStyleBackColor = false;
        _cancelButton.Click += CancelButton_Click;
        _cancelButton.EnabledChanged += CancelButton_EnabledChanged;
        // 
        // _progressBar
        // 
        _progressBar.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        _progressBar.Location = new Point(16, 45);
        _progressBar.Maximum = 100;
        _progressBar.Minimum = 0;
        _progressBar.Name = "_progressBar";
        _progressBar.Size = new Size(748, 20);
        _progressBar.Style = ProgressBarStyle.Continuous;
        _progressBar.TabIndex = 2;
        // 
        // _statsTable
        // 
        _statsTable.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        _statsTable.BackColor = Color.Transparent;
        _statsTable.CellBorderStyle = TableLayoutPanelCellBorderStyle.None;
        _statsTable.ColumnCount = 2;
        _statsTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        _statsTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        _statsTable.Location = new Point(16, 73);
        _statsTable.Name = "_statsTable";
        _statsTable.RowCount = 5;
        _statsTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 18F));
        _statsTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 18F));
        _statsTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 18F));
        _statsTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 18F));
        _statsTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 18F));
        _statsTable.Size = new Size(748, 90);
        _statsTable.TabIndex = 3;
        // Add stat labels to table
        AddStatLabel(_statsTable, "Progress: 0%", 0, 0, "progress");
        AddStatLabel(_statsTable, "Transferred: 0 / 0 bytes", 0, 1, "transferred");
        AddStatLabel(_statsTable, "Current Speed: 0.00 B/s (0.00 bit)", 1, 0, "currentSpeed");
        AddStatLabel(_statsTable, "Average Speed: 0.00 B/s", 1, 1, "averageSpeed");
        AddStatLabel(_statsTable, "Maximum Speed: 0.00 B/s", 2, 0, "maximumSpeed");
        AddStatLabel(_statsTable, "Elapsed: 0.0s", 2, 1, "elapsed");
        AddStatLabel(_statsTable, "Latency: 0.00 ms (0.00 - 0.00 ms)", 3, 0, "latency");
        AddStatLabel(_statsTable, "Remaining: 0.0s", 3, 1, "remaining");
        AddStatLabel(_statsTable, "TTFB: 0 ms", 4, 0, "ttfb");
        // 
        // DownloadProgressControl
        // 
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        Controls.Add(_mainPanel);
        Name = "DownloadProgressControl";
        Size = new Size(780, 175);
        Layout += DownloadProgressControl_Layout;
        Resize += DownloadProgressControl_Resize;
        _mainPanel.ResumeLayout(false);
        ResumeLayout(false);
    }

    private void DownloadProgressControl_Layout(object? sender, LayoutEventArgs e)
    {
        // Position the cancel button at the right edge, and adjust the file name label width
        if (_cancelButton != null && _fileNameLabel != null)
        {
            int leftPadding = 16;
            int topPadding = 12;
            int rightPadding = 16;
            int verticalSpacing = 8;
            int fileNameHeight = 25;
            int progressBarHeight = 20;
            int cancelButtonWidth = 100;
            int contentWidth = Width - leftPadding - rightPadding - 2; // -2 for border

            _cancelButton.Top = topPadding;
            _cancelButton.Left = Width - rightPadding - cancelButtonWidth - 2; // -2 for border
            _fileNameLabel.Top = topPadding;
            _fileNameLabel.Left = leftPadding;
            _fileNameLabel.Width = _cancelButton.Left - _fileNameLabel.Left - 10;
            _fileNameLabel.Height = fileNameHeight;

            // Progress bar
            _progressBar.Top = topPadding + fileNameHeight + verticalSpacing;
            _progressBar.Left = leftPadding;
            _progressBar.Width = contentWidth;
            _progressBar.Height = progressBarHeight;

            // Stats table
            _statsTable.Top = _progressBar.Top + progressBarHeight + verticalSpacing;
            _statsTable.Left = leftPadding;
            _statsTable.Width = contentWidth;
        }
    }

    private void DownloadProgressControl_Resize(object? sender, EventArgs e)
    {
        DownloadProgressControl_Layout(sender, null!);
    }

    private void MainPanel_Paint(object? sender, PaintEventArgs e)
    {
        // Draw rounded gray border (like WPF with CornerRadius="3")
        using var pen = new Pen(Color.FromArgb(208, 208, 208), 1); // #D0D0D0
        
        // Add padding from edges (top, right, bottom, left)
        int padding = 2;
        int cornerRadius = 6; // 3 * 2 for better appearance at control scale
        
        var rect = new Rectangle(
            padding, 
            padding, 
            _mainPanel.Width - padding * 2 - 1, 
            _mainPanel.Height - padding * 2 - 1
        );
        
        // Enable anti-aliasing for smooth rounded corners
        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        
        // Draw rounded rectangle
        using var path = new System.Drawing.Drawing2D.GraphicsPath();
        path.AddArc(rect.X, rect.Y, cornerRadius, cornerRadius, 180, 90);
        path.AddArc(rect.Right - cornerRadius, rect.Y, cornerRadius, cornerRadius, 270, 90);
        path.AddArc(rect.Right - cornerRadius, rect.Bottom - cornerRadius, cornerRadius, cornerRadius, 0, 90);
        path.AddArc(rect.X, rect.Bottom - cornerRadius, cornerRadius, cornerRadius, 90, 90);
        path.CloseFigure();
        
        e.Graphics.DrawPath(pen, path);
    }

    private static void AddStatLabel(TableLayoutPanel table, string text, int row, int column, string tag)
    {
        var label = new Label
        {
            Text = text,
            Font = new Font("Segoe UI", 9F),
            AutoSize = false,
            Size = new Size(370, 15),
            TextAlign = ContentAlignment.MiddleLeft,
            Tag = tag,
            Dock = DockStyle.Fill
        };
        table.Controls.Add(label, column, row);
    }

    private Label? GetStatLabel(string tag)
    {
        foreach (Control control in _statsTable.Controls)
        {
            if (control is Label label && label.Tag?.ToString() == tag)
                return label;
        }
        return null;
    }

    private void CancelButton_EnabledChanged(object? sender, EventArgs e)
    {
        if (!_cancelButton.Enabled)
        {
            _cancelButton.BackColor = Color.FromArgb(204, 204, 204); // #CCCCCC
            _cancelButton.ForeColor = Color.White; // Always white text when disabled
            _cancelButton.Cursor = Cursors.Arrow;
        }
        else
        {
            _cancelButton.BackColor = Color.FromArgb(232, 17, 35);
            _cancelButton.ForeColor = Color.White;
            _cancelButton.Cursor = Cursors.Hand;
        }
    }

    public void SetFileName(string fileName)
    {
        if (InvokeRequired)
        {
            Invoke(() => SetFileName(fileName));
            return;
        }
        _fileNameLabel.Text = fileName;
    }

    public void SetCancellationTokenSource(CancellationTokenSource? cts)
    {
        _cancellationTokenSource = cts;
        if (InvokeRequired)
        {
            Invoke(() => _cancelButton.Enabled = cts != null);
        }
        else
        {
            _cancelButton.Enabled = cts != null;
        }
    }

    private void CancelButton_Click(object? sender, EventArgs e)
    {
        _cancellationTokenSource?.Cancel();
        _cancelButton.Enabled = false;
    }

    /// <summary>
    /// Updates the progress display with the current transfer state.
    /// </summary>
    /// <param name="state">The current transfer state.</param>
    public void UpdateProgress(TransferState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        
        if (InvokeRequired)
        {
            Invoke(() => UpdateProgress(state));
            return;
        }

        try
        {
            double percent = state.CalcProgressPercentage();
            if (percent >= 0)
            {
                _progressBar.Value = Math.Min((int)(percent * 100), 100);
                GetStatLabel("progress")!.Text = $@"Progress: {percent:P1}";
            }
            else
            {
                GetStatLabel("progress")!.Text = @"Progress: Unknown";
            }

            GetStatLabel("transferred")!.Text = $@"Transferred: {state.Total.Transferred:N0} / {state.TotalBytes:N0} bytes";

            var (chunkSpeed, chunkUnit) = state.Chunk.ByteUnit;
            var (chunkBitSpeed, chunkBitUnit) = state.Chunk.BitUnit;
            GetStatLabel("currentSpeed")!.Text = $@"Current Speed: {chunkSpeed:N2} {chunkUnit}/s ({chunkBitSpeed:N2} {chunkBitUnit})";

            var (avgSpeed, avgUnit) = state.Average.ByteUnit;
            GetStatLabel("averageSpeed")!.Text = $@"Average Speed: {avgSpeed:N2} {avgUnit}/s";

            var (maxSpeed, maxUnit) = state.Maximum.ByteUnit;
            GetStatLabel("maximumSpeed")!.Text = $@"Maximum Speed: {maxSpeed:N2} {maxUnit}/s";

            GetStatLabel("elapsed")!.Text = $@"Elapsed: {state.Total.Elapsed.TotalSeconds:N1}s";

            var remaining = state.CalcEstimatedRemainingTime();
            if (remaining != TimeSpan.MinValue)
            {
                GetStatLabel("remaining")!.Text = $@"Remaining: {remaining.TotalSeconds:N1}s";
            }
            else
            {
                GetStatLabel("remaining")!.Text = @"Remaining: Unknown";
            }

            if (state.Latency is { PacketCount: > 0, PacketMinMs: >= 0 })
            {
                GetStatLabel("latency")!.Text = $@"Latency: {state.Latency.PacketAvgMs:N2} ms ({state.Latency.PacketMinMs:N2} - {state.Latency.PacketMaxMs:N2} ms)";
                
                if (state.Latency.TimeToFirstByte is > 0)
                {
                    GetStatLabel("ttfb")!.Text = $@"TTFB: {state.Latency.TimeToFirstByte.Value:N0} ms";
                }
                else
                {
                    GetStatLabel("ttfb")!.Text = @"TTFB: Unknown";
                }
            }
            else
            {
                GetStatLabel("latency")!.Text = @"Latency: Unknown";
                GetStatLabel("ttfb")!.Text = @"TTFB: Unknown";
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            // Ignore errors during UI updates (cross-thread operations, control disposal, etc.)
        }
    }

    public void MarkComplete()
    {
        if (InvokeRequired)
        {
            Invoke(MarkComplete);
            return;
        }
        _fileNameLabel.ForeColor = Color.Green;
        _cancelButton.Enabled = false;
    }

    public void MarkError()
    {
        if (InvokeRequired)
        {
            Invoke(MarkError);
            return;
        }
        _fileNameLabel.ForeColor = Color.Red;
        _cancelButton.Enabled = false;
    }

    /// <summary>
    /// Releases the unmanaged resources used by the control and optionally releases the managed resources.
    /// </summary>
    /// <param name="disposing">true to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _cancellationTokenSource?.Dispose();
            _mainPanel?.Dispose();
            _fileNameLabel?.Dispose();
            _progressBar?.Dispose();
            _cancelButton?.Dispose();
            _statsTable?.Dispose();
        }
        base.Dispose(disposing);
    }
}
