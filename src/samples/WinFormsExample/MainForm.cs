using Blazing.Extensions.DependencyInjection;
using Blazing.Extensions.Http.Models;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WinFormsExample;

[AutoRegister(ServiceLifetime.Transient)]
internal sealed class MainForm : Form
{
    private readonly DownloadService _downloadService;
    private Button _startButton = null!;
    private Button _stopButton = null!;
    private Label _titleLabel = null!;
    private Panel _scrollPanel = null!;
    private Panel _downloadsPanel = null!;
    private Panel _buttonPanel = null!;
    private CancellationTokenSource? _globalCancellationTokenSource;
    private readonly List<(DownloadProgressControl Control, CancellationTokenSource Cts)> _downloadControls = new();
    private List<Task> _downloadTasks = new(); // Track running download tasks
    private bool _isClosing; // Prevent reentrancy

    // Download URLs for testing
    private readonly string[] _downloadUrls =
    [
        "https://download.visualstudio.microsoft.com/download/pr/89a2923a-18df-4dce-b069-51e687b04a53/9db4348b561703e622de7f03b1f11e93/dotnet-sdk-7.0.203-win-x64.exe",
        "https://download.visualstudio.microsoft.com/download/pr/89a2923a-18df-4dce-b069-51e687b04a53/9db4348b561703e622de7f03b1f11e93/dotnet-sdk-7.0.203-win-x64.exe",
        "https://download.visualstudio.microsoft.com/download/pr/89a2923a-18df-4dce-b069-51e687b04a53/9db4348b561703e622de7f03b1f11e93/dotnet-sdk-7.0.203-win-x64.exe",
        "https://download.visualstudio.microsoft.com/download/pr/89a2923a-18df-4dce-b069-51e687b04a53/9db4348b561703e622de7f03b1f11e93/dotnet-sdk-7.0.203-win-x64.exe"
    ];

    // Statistics state
    private int _totalDownloads;
    private int _activeDownloads;
    private int _completedDownloads;
    private int _failedDownloads;
    private long _totalBytes;
    private double _totalSpeed;
    private double _totalLatency;
    private int _latencyCount;
    private StatisticsPanelCustom _statisticsPanel = null!;
    private DateTime _startTime;

    public MainForm(DownloadService downloadService)
    {
        _downloadService = downloadService;
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        _titleLabel = new Label();
        _buttonPanel = new Panel();
        _startButton = new Button();
        _stopButton = new Button();
        _scrollPanel = new Panel();
        _downloadsPanel = new Panel();
        _statisticsPanel = new StatisticsPanelCustom();
        _buttonPanel.SuspendLayout();
        _scrollPanel.SuspendLayout();
        SuspendLayout();
        // 
        // _titleLabel
        // 
        _titleLabel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        _titleLabel.BackColor = Color.Transparent;
        _titleLabel.Font = new Font("Segoe UI", 14F, FontStyle.Bold);
        _titleLabel.Location = new Point(20, 20);
        _titleLabel.Name = "_titleLabel";
        _titleLabel.Size = new Size(780, 30);
        _titleLabel.TabIndex = 0;
        _titleLabel.Text = @"HTTP Download Manager with Progress & Latency Tracking";
        _titleLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // _buttonPanel
        // 
        _buttonPanel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        _buttonPanel.BackColor = Color.Transparent;
        _buttonPanel.Controls.Add(_startButton);
        _buttonPanel.Controls.Add(_stopButton);
        _buttonPanel.Location = new Point(20, 55);
        _buttonPanel.Name = "_buttonPanel";
        _buttonPanel.Size = new Size(780, 40);
        _buttonPanel.TabIndex = 1;
        // 
        // _startButton
        // 
        _startButton.BackColor = Color.FromArgb(0, 120, 215);
        _startButton.Cursor = Cursors.Hand;
        _startButton.FlatAppearance.BorderSize = 0;
        _startButton.FlatStyle = FlatStyle.Flat;
        _startButton.Font = new Font("Segoe UI", 10F);
        _startButton.ForeColor = Color.White;
        _startButton.Location = new Point(0, 0);
        _startButton.Name = "_startButton";
        _startButton.Size = new Size(150, 35);
        _startButton.TabIndex = 0;
        _startButton.Text = @"Start Downloads";
        _startButton.UseVisualStyleBackColor = false;
        _startButton.EnabledChanged += Button_EnabledChanged;
        _startButton.Click += StartButton_Click;
        // 
        // _stopButton
        // 
        _stopButton.BackColor = Color.FromArgb(232, 17, 35);
        _stopButton.Cursor = Cursors.Hand;
        _stopButton.Enabled = false;
        _stopButton.FlatAppearance.BorderSize = 0;
        _stopButton.FlatStyle = FlatStyle.Flat;
        _stopButton.Font = new Font("Segoe UI", 10F);
        _stopButton.ForeColor = Color.White;
        _stopButton.Location = new Point(160, 0);
        _stopButton.Name = "_stopButton";
        _stopButton.Size = new Size(160, 35);
        _stopButton.TabIndex = 1;
        _stopButton.Text = @"Stop All Downloads";
        _stopButton.UseVisualStyleBackColor = false;
        _stopButton.EnabledChanged += Button_EnabledChanged;
        _stopButton.Click += StopButton_Click;
        // 
        // _scrollPanel
        // 
        _scrollPanel.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        _scrollPanel.AutoScroll = true;
        _scrollPanel.BackColor = Color.White;
        _scrollPanel.BorderStyle = BorderStyle.FixedSingle;
        _scrollPanel.Controls.Add(_downloadsPanel);
        _scrollPanel.Location = new Point(20, 312);
        _scrollPanel.Name = "_scrollPanel";
        _scrollPanel.Size = new Size(780, 476);
        _scrollPanel.TabIndex = 3;
        _scrollPanel.AutoScrollMargin = new Size(20, 0);
        _scrollPanel.HorizontalScroll.Enabled = false;
        _scrollPanel.HorizontalScroll.Visible = false;
        _scrollPanel.HorizontalScroll.Maximum = 0;
        _scrollPanel.HorizontalScroll.SmallChange = 0;
        _scrollPanel.HorizontalScroll.LargeChange = 0;
        _scrollPanel.Paint += ScrollPanel_Paint;
        // 
        // _downloadsPanel
        // 
        _downloadsPanel.AutoSize = false;
        _downloadsPanel.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        _downloadsPanel.Location = new Point(0, 0);
        _downloadsPanel.Name = "_downloadsPanel";
        _downloadsPanel.Size = new Size(0, 0);
        _downloadsPanel.TabIndex = 0;
        _downloadsPanel.Dock = DockStyle.Fill;
        _downloadsPanel.AutoScroll = true;
        _downloadsPanel.VerticalScroll.Enabled = true;
        _downloadsPanel.HorizontalScroll.Enabled = false;
        _downloadsPanel.HorizontalScroll.Visible = false;
        _downloadsPanel.BorderStyle = BorderStyle.None;
        _downloadsPanel.BackColor = Color.Transparent;
        // 
        // _statisticsPanel
        // 
        _statisticsPanel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        _statisticsPanel.BackColor = Color.White;
        _statisticsPanel.BorderStyle = BorderStyle.None;
        _statisticsPanel.Location = new Point(20, 101);
        _statisticsPanel.MinimumSize = new Size(100, 70);
        _statisticsPanel.Name = "_statisticsPanel";
        _statisticsPanel.Padding = new Padding(20, 0, 20, 0);
        _statisticsPanel.Size = new Size(780, 205);
        _statisticsPanel.TabIndex = 4;
        _statisticsPanel.Paint += StatisticsPanel_Paint;
        // 
        // MainForm
        // 
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        BackColor = Color.FromArgb(245, 245, 245);
        ClientSize = new Size(820, 800);
        Controls.Add(_statisticsPanel);
        Controls.Add(_titleLabel);
        Controls.Add(_buttonPanel);
        Controls.Add(_scrollPanel);
        Name = "MainForm";
        StartPosition = FormStartPosition.CenterScreen;
        Text = @"Blazing.Extensions.Http - Download Manager (WinForms)";
        Load += MainForm_Load;
        Resize += MainForm_Resize;
        _buttonPanel.ResumeLayout(false);
        _scrollPanel.ResumeLayout(false);
        _scrollPanel.PerformLayout();
        ResumeLayout(false);
    }

    private void ScrollPanel_Paint(object? sender, PaintEventArgs e)
    {
        using var pen = new Pen(Color.FromArgb(200, 200, 200), 1);
        var rect = new Rectangle(0, 0, _scrollPanel.Width - 1, _scrollPanel.Height - 1);
        e.Graphics.DrawRectangle(pen, rect);
    }

    private void StatisticsPanel_Paint(object? sender, PaintEventArgs e)
    {
        using var pen = new Pen(Color.FromArgb(200, 200, 200), 1);
        var rect = new Rectangle(0, 0, _statisticsPanel.Width - 1, _statisticsPanel.Height - 1);
        e.Graphics.DrawRectangle(pen, rect);
    }

    private void AdjustDownloadControlWidthsAndPadding()
    {
        int controlWidth = GetAdjustedControlWidth();
        int cardMargin = 12; // Match WPF card margin
        foreach (var (control, _) in _downloadControls)
        {
            control.Width = controlWidth - 2 * cardMargin;
            control.Margin = new Padding(cardMargin, cardMargin, cardMargin, cardMargin); // Outer margin for padding
        }
        // Add padding to the downloads panel itself for outermost spacing
        _downloadsPanel.Padding = new Padding(cardMargin, cardMargin, cardMargin, cardMargin);
    }

    private void Button_EnabledChanged(object? sender, EventArgs e)
    {
        if (sender is Button button)
        {
            button.UseVisualStyleBackColor = false; // Ensure custom colors are always used
            if (!button.Enabled)
            {
                // Use gray background and white text for disabled state (matches WPF look)
                button.BackColor = Color.FromArgb(204, 204, 204); // #CCCCCC
                button.ForeColor = Color.White;
                button.Cursor = Cursors.Arrow;
            }
            else
            {
                // Restore original colors based on which button
                if (button == _startButton)
                {
                    button.BackColor = Color.FromArgb(0, 120, 215);
                    button.ForeColor = Color.White;
                }
                else if (button == _stopButton)
                {
                    button.BackColor = Color.FromArgb(232, 17, 35);
                    button.ForeColor = Color.White;
                }
                button.Cursor = Cursors.Hand;
            }
        }
    }

    private void MainForm_Load(object? sender, EventArgs e)
    {
        UpdateScrollPanelPosition();
    }

    private void UpdateScrollPanelPosition()
    {
        // Position scroll panel 15px below the statistics panel
        int scrollPanelTop = _statisticsPanel.Bottom + 15;
        if (_scrollPanel.Top != scrollPanelTop)
        {
            _scrollPanel.Top = scrollPanelTop;
            int formPadding = 20;
            int availableHeight = ClientSize.Height - scrollPanelTop - formPadding;
            _scrollPanel.Height = Math.Max(100, availableHeight);
        }
    }

    // Win32 API for getting the vertical scrollbar width
    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);
    private const int SM_CXVSCROLL = 2;

    private static int GetVerticalScrollBarWidth()
    {
        // Use Win32 API for accurate DPI-aware scrollbar width
        return GetSystemMetrics(SM_CXVSCROLL);
    }

    private int GetAdjustedControlWidth()
    {
        int width = _scrollPanel.ClientSize.Width - 20;
        // If vertical scrollbar is visible, subtract its width
        if (_scrollPanel.VerticalScroll.Visible)
        {
            width -= GetVerticalScrollBarWidth();
        }
        return Math.Max(100, width);
    }

    private async void StartButton_Click(object? sender, EventArgs e)
    {
        _startButton.Enabled = false;
        _stopButton.Enabled = true;

        _downloadsPanel.Controls.Clear();
        foreach (var (_, cts) in _downloadControls) cts.Dispose();
        _downloadControls.Clear();

        _totalDownloads = _downloadUrls.Length;
        _activeDownloads = _totalDownloads;
        _completedDownloads = 0;
        _failedDownloads = 0;
        _totalBytes = 0;
        _totalSpeed = 0;
        _totalLatency = 0;
        _latencyCount = 0;
        _startTime = DateTime.Now;
        UpdateStatisticsPanel(_totalDownloads, _activeDownloads, _completedDownloads, _failedDownloads, _totalBytes, _totalSpeed, 0, 0);

        _globalCancellationTokenSource = new CancellationTokenSource();
        int controlWidth = GetAdjustedControlWidth();
        for (int i = 0; i < _downloadUrls.Length; i++)
        {
            var individualCts = CancellationTokenSource.CreateLinkedTokenSource(_globalCancellationTokenSource.Token);
            var downloadControl = new DownloadProgressControl
            {
                Size = new Size(controlWidth, 175),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                Dock = DockStyle.Top,
                Margin = new Padding(10, 10, 10, 10) // Add margin for spacing between controls
            };
            downloadControl.SetFileName($"Download #{i + 1}: File{i + 1}.exe");
            downloadControl.SetCancellationTokenSource(individualCts);
            _downloadControls.Add((downloadControl, individualCts));
            _downloadsPanel.Controls.Add(downloadControl);
        }
        AdjustDownloadControlWidthsAndPadding();
        var downloadTasks = new List<Task>();
        for (int i = 0; i < _downloadUrls.Length; i++)
        {
            int index = i;
            downloadTasks.Add(DownloadFileAsync(index, _downloadUrls[i], _downloadControls[i].Cts.Token));
        }
        _downloadTasks = downloadTasks; // Track tasks for closing logic
        try
        {
            await Task.WhenAll(downloadTasks).ConfigureAwait(false);
            MessageBox.Show(@"All downloads completed!", @"Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (OperationCanceledException)
        {
            MessageBox.Show(@"Downloads were cancelled.", @"Cancelled", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        catch (HttpRequestException ex)
        {
            MessageBox.Show($@"Network error during downloads: {ex.Message}", @"Network Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        catch (UnauthorizedAccessException ex)
        {
            MessageBox.Show($@"Access denied: {ex.Message}", @"Access Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        catch (IOException ex)
        {
            MessageBox.Show($@"File I/O error: {ex.Message}", @"File Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _startButton.Enabled = true;
            _stopButton.Enabled = false;
            _downloadTasks.Clear(); // Clear when done
        }
    }

    private void StopButton_Click(object? sender, EventArgs e)
    {
        _globalCancellationTokenSource?.Cancel();
        _stopButton.Enabled = false;
    }

    private async Task DownloadFileAsync(int index, string url, CancellationToken cancellationToken)
    {
        var (control, _) = _downloadControls[index];
        var destinationPath = Path.Combine(Path.GetTempPath(), $"download_{index}_{Guid.NewGuid()}.tmp");
        try
        {
            var progress = new Progress<TransferState>(state =>
            {
                control.UpdateProgress(state);
                // Update statistics
                _totalBytes = Math.Max(_totalBytes, state.Total.Transferred);
                _totalSpeed = state.Chunk.RawSpeed;
                if (state.Latency is { PacketCount: > 0 })
                {
                    _totalLatency += state.Latency.PacketAvgMs;
                    _latencyCount++;
                }
                double avgLatency = _latencyCount > 0 ? _totalLatency / _latencyCount : 0;
                double elapsed = (DateTime.Now - _startTime).TotalSeconds;
                UpdateStatisticsPanel(_totalDownloads, _activeDownloads, _completedDownloads, _failedDownloads, _totalBytes, _totalSpeed, avgLatency, elapsed);
            });
            var latencyTracker = new LatencyTracker();
            await _downloadService.DownloadFileAsync(url, destinationPath, progress, latencyTracker, cancellationToken).ConfigureAwait(false);
            control.MarkComplete();
            _activeDownloads--;
            _completedDownloads++;
        }
        catch (OperationCanceledException)
        {
            control.MarkError();
            _activeDownloads--;
            _failedDownloads++;
            throw;
        }
        catch (Exception)
        {
            control.MarkError();
            _activeDownloads--;
            _failedDownloads++;
            throw;
        }
        finally
        {
            double avgLatency = _latencyCount > 0 ? _totalLatency / _latencyCount : 0;
            double elapsed = (DateTime.Now - _startTime).TotalSeconds;
            UpdateStatisticsPanel(_totalDownloads, _activeDownloads, _completedDownloads, _failedDownloads, _totalBytes, _totalSpeed, avgLatency, elapsed);
            // Clean up downloaded file
            if (File.Exists(destinationPath))
            {
                File.Delete(destinationPath);
            }
        }
    }

    private void UpdateStatisticsPanel(int total, int active, int completed, int failed, long totalBytes, double speed, double avgLatency, double elapsedSeconds)
    {
        _statisticsPanel.UpdateStats(new[]
        {
            ("Total Downloads", total.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            ("Active", active.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            ("Completed", completed.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            ("Failed", failed.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            ("Total Bytes", FormatBytes(totalBytes)),
            ("Overall Speed", $"{speed:N2} B/s"),
            ("Avg Latency", $"{avgLatency:N2} ms"),
            ("Total Elapsed", $"{elapsedSeconds:N1}s")
        });
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = { "B", "KiB", "MiB", "GiB", "TiB" };
        double b = bytes;
        int unit = 0;
        while (b >= 1024 && unit < units.Length - 1)
        {
            b /= 1024;
            unit++;
        }
        return $"{b:N2} {units[unit]}";
    }

    /// <summary>
    /// Raises the <see cref="Form.FormClosing"/> event.
    /// </summary>
    /// <param name="e">A <see cref="FormClosingEventArgs"/> that contains the event data.</param>
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        
        if (_isClosing)
        {
            base.OnFormClosing(e);
            return;
        }

        // If there are running downloads, cancel and wait for them
        if (_downloadTasks is { Count: > 0 } && !_downloadTasks.All(t => t.IsCompleted))
        {
            e.Cancel = true;
            _globalCancellationTokenSource?.Cancel();
            _stopButton.Enabled = false;
            _isClosing = true;
            // Wait for downloads to finish, then close
            Task.Run(async () =>
            {
                try 
                { 
                    await Task.WhenAll(_downloadTasks).ConfigureAwait(false); 
                } 
                catch (OperationCanceledException) 
                { 
                    // Expected when canceling downloads
                } 
                catch (AggregateException ex) when (ex.InnerExceptions.All(e => e is OperationCanceledException))
                { 
                    // Expected when multiple downloads are cancelled
                }
                catch (ObjectDisposedException)
                {
                    // Expected during shutdown when objects are disposed
                }
                
                if (InvokeRequired)
                {
                    Invoke(Close);
                }
                else
                {
                    Close();
                }
            });
            return;
        }

        // Dispose all cancellation token sources
        foreach (var (_, cts) in _downloadControls)
        {
            cts.Dispose();
        }

        base.OnFormClosing(e);
    }

    private void MainForm_Resize(object? sender, EventArgs e)
    {
        UpdateScrollPanelPosition();
        AdjustDownloadControlWidthsAndPadding();
    }

    /// <summary>
    /// Releases the unmanaged resources used by the form and optionally releases the managed resources.
    /// </summary>
    /// <param name="disposing">true to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _globalCancellationTokenSource?.Dispose();
            _startButton?.Dispose();
            _stopButton?.Dispose();
            _titleLabel?.Dispose();
            _scrollPanel?.Dispose();
            _downloadsPanel?.Dispose();
            _buttonPanel?.Dispose();
            _statisticsPanel?.Dispose();
        }
        base.Dispose(disposing);
    }
}
