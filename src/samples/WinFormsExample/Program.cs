using Blazing.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace WinFormsExample;

internal static class Program
{
    public static IServiceProvider ServiceProvider { get; private set; } = null!;

    /// <summary>
    ///  The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();

        try
        {
            // Configure services
            var services = new ServiceCollection();
            
            // Register HttpClient with factory
            services.AddHttpClient("DownloadClient", client =>
            {
                client.Timeout = TimeSpan.FromMinutes(30);
            });

            // Auto-discover and register all services with AutoRegister attribute
            // Specify the current assembly to ensure all types are found
            services.Register(typeof(Program).Assembly);

            // Build service provider
            ServiceProvider = services.BuildServiceProvider();

            // Resolve and run main form
            var mainForm = ServiceProvider.GetRequiredService<MainForm>();
            Application.Run(mainForm);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
        {
            MessageBox.Show($"Startup Error: {ex.Message}", "Application Error", 
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
