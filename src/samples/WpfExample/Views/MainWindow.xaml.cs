using System.Windows;
using Blazing.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using WpfExample.ViewModels;
using System.ComponentModel;
using System.Threading.Tasks;

namespace WpfExample.Views;

[AutoRegister(ServiceLifetime.Transient)]
public partial class MainWindow : Window
{
    private bool _isClosing;
    
    /// <summary>
    /// Initializes a new instance of the <see cref="MainWindow"/> class.
    /// </summary>
    /// <param name="viewModel">The main view model.</param>
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Closing += MainWindow_Closing;
    }

    private async void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        if (_isClosing) return;
        if (DataContext is MainViewModel vm && vm.IsDownloading)
        {
            e.Cancel = true;
            vm.StopDownloadsCommand.Execute(null);
            _isClosing = true;
            // Wait for downloads to finish cancelling
            await Task.Delay(100).ConfigureAwait(false); // Let cancellation propagate
            while (vm.IsDownloading)
            {
                await Task.Delay(100).ConfigureAwait(false);
            }
            Close();
        }
    }
}
