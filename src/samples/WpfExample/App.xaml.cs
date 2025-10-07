using System.Windows;
using Blazing.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using WpfExample.Views;

namespace WpfExample;

/// <summary>
/// Main application class for the WPF Example.
/// </summary>
public partial class App : Application
{
    /// <summary>
    /// Handles the application startup event and configures services.
    /// </summary>
    /// <param name="e">Startup event arguments.</param>
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Configure services using Blazing.Extensions.DependencyInjection
        this.ConfigureServices(services =>
        {
            // Register HttpClient with factory
            services.AddHttpClient("DownloadClient", client =>
            {
                client.Timeout = TimeSpan.FromMinutes(30);
            });

            // Auto-discover and register all services, ViewModels, and Views with AutoRegister attribute
            // Specify the current assembly to ensure all types are found
            services.Register(typeof(App).Assembly);
        });

        // Resolve and show main window
        var serviceProvider = this.GetServices();
        var mainWindow = serviceProvider!.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }
}
