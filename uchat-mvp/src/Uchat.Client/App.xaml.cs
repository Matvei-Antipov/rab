namespace Uchat.Client
{
    using System;
    using System.IO;
    using System.Net;
    using System.Threading.Tasks;
    using System.Windows;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Serilog;
    using Uchat.Client.Infrastructure;
    using Uchat.Client.Models;
    using Uchat.Client.Services;
    using Uchat.Client.ViewModels;
    using Uchat.Client.Views;

    /// <summary>
    /// Interaction logic for App.xaml.
    /// </summary>
    public partial class App : Application
    {
        private IHost? host;

        /// <summary>
        /// Gets the service provider for resolving services.
        /// </summary>
        public IServiceProvider? Services => this.host?.Services;

        /// <inheritdoc/>
        protected override async void OnStartup(StartupEventArgs e)
        {
            this.ConfigureLogging();
            this.ConfigureUnhandledExceptionHandling();

            try
            {
                // Parse CLI arguments
                var config = this.ParseCommandLineArguments(e.Args);
                if (config == null)
                {
                    return;
                }

                // Initialize host with configuration
                this.host = Host.CreateDefaultBuilder()
                    .ConfigureServices((context, services) =>
                    {
                        services.AddSingleton(config);
                        services.AddApplicationServices(config);
                        services.AddViewModels();
                        services.AddViews();
                    })
                    .Build();

                await this.host.StartAsync();

                var themeManager = this.Services!.GetRequiredService<IThemeManager>();
                themeManager.ApplyTheme(AppTheme.Dark);

                var mainWindow = this.Services!.GetRequiredService<MainWindow>();
                this.MainWindow = mainWindow;

                var navigationService = this.Services!.GetRequiredService<INavigationService>();
                Log.Information("Navigating to welcome screen");
                navigationService.NavigateTo<WelcomeViewModel>();

                mainWindow.Show();

                Log.Information("Application started with server configuration: {IP}:{Port}", config.IpAddress, config.Port);

                base.OnStartup(e);
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Fatal error during application startup");
                MessageBox.Show(
                    $"Failed to start application: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                this.Shutdown(1);
            }
        }

        /// <inheritdoc/>
        protected override async void OnExit(ExitEventArgs e)
        {
            Log.Information("Application shutting down");

            if (this.host != null)
            {
                try
                {
                    // Dispose messaging service first to cancel background tasks
                    var messagingService = this.Services?.GetService<IMessagingService>() as IDisposable;
                    if (messagingService != null)
                    {
                        Log.Information("Disconnecting messaging service");
                        if (messagingService is IMessagingService ms)
                        {
                            await ms.DisconnectAsync();
                        }

                        messagingService.Dispose();
                    }

                    Log.Information("Stopping host");
                    await this.host.StopAsync(TimeSpan.FromSeconds(5));
                    this.host.Dispose();
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error during application shutdown");
                }
            }

            Log.CloseAndFlush();

            base.OnExit(e);
        }

        /// <summary>
        /// Parse and validate command-line arguments.
        /// </summary>
        /// <param name="args">Command-line arguments.</param>
        /// <returns>Server configuration if valid, null otherwise.</returns>
        private ServerConfiguration? ParseCommandLineArguments(string[] args)
        {
            if (args == null || args.Length != 2)
            {
                this.ShowUsageAndExit("Missing or incorrect number of arguments");
                return null;
            }

            var ipAddress = args[0];
            var portStr = args[1];

            // Validate IP address
            if (!IPAddress.TryParse(ipAddress, out _) && ipAddress != "localhost")
            {
                this.ShowUsageAndExit($"Invalid IP address: {ipAddress}");
                return null;
            }

            // Validate port
            if (!int.TryParse(portStr, out int port))
            {
                this.ShowUsageAndExit($"Invalid port: {portStr}");
                return null;
            }

            if (port < 1 || port > 65535)
            {
                this.ShowUsageAndExit($"Port must be between 1 and 65535, got {port}");
                return null;
            }

            var config = new ServerConfiguration
            {
                IpAddress = ipAddress,
                Port = port,
            };

            var validationError = config.Validate();
            if (validationError != null)
            {
                this.ShowUsageAndExit(validationError);
                return null;
            }

            Log.Information("Server configuration parsed: IP={IP}, Port={Port}", ipAddress, port);
            return config;
        }

        /// <summary>
        /// Show usage information and exit application.
        /// </summary>
        /// <param name="errorMessage">Optional error message to display.</param>
        private void ShowUsageAndExit(string? errorMessage = null)
        {
            const string usage = @"UChat - Real-time chat application

Usage: uchat <server_ip> <port>

Arguments:
  server_ip  - Server IP address or hostname (e.g., 127.0.0.1, localhost, 192.168.1.100)
  port       - Server port number (1-65535) (e.g., 5251, 8080)

Examples:
  uchat 127.0.0.1 5251
  uchat localhost 8080
  uchat 192.168.1.100 9000";

            var message = usage;
            if (errorMessage != null)
            {
                message = $"Error: {errorMessage}\n\n{usage}";
            }

            MessageBox.Show(message, "UChat", MessageBoxButton.OK, MessageBoxImage.Information);
            this.Shutdown(errorMessage == null ? 0 : 1);
        }

        private void ConfigureLogging()
        {
            var logDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Uchat",
                "Logs");

            Directory.CreateDirectory(logDirectory);

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.File(
                    path: Path.Combine(logDirectory, "uchat-client-.log"),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 7,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
                .CreateLogger();

            Log.Information("Logging configured");
        }

        private void ConfigureUnhandledExceptionHandling()
        {
            // Handle unhandled exceptions in UI thread
            this.DispatcherUnhandledException += (sender, e) =>
            {
                Log.Fatal(e.Exception, "Unhandled exception in UI thread");
                e.Handled = true; // Prevent application from crashing

                MessageBox.Show(
                    $"An unexpected error occurred: {e.Exception.Message}\n\nThe application will continue running.",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            };

            // Handle unhandled exceptions in background threads
            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {
                if (e.ExceptionObject is Exception ex)
                {
                    Log.Fatal(ex, "Unhandled exception in background thread");
                }
                else
                {
                    Log.Fatal("Unhandled exception (non-Exception type): {Exception}", e.ExceptionObject);
                }

                // Note: Can't prevent shutdown for AppDomain.UnhandledException
                // but we can log it before the application terminates
            };

            // Handle unhandled exceptions in tasks
            TaskScheduler.UnobservedTaskException += (sender, e) =>
            {
                Log.Error(e.Exception, "Unobserved task exception");
                e.SetObserved(); // Mark exception as observed to prevent app crash
            };

            Log.Information("Unhandled exception handling configured");
        }
    }
}
