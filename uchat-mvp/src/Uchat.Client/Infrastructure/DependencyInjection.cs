namespace Uchat.Client.Infrastructure
{
    using System;
    using System.Net.Http;
    using Microsoft.Extensions.DependencyInjection;
    using Serilog;
    using Uchat.Client.Models;
    using Uchat.Client.Services;
    using Uchat.Client.ViewModels;
    using Uchat.Client.Views;

    /// <summary>
    /// Configures dependency injection for the application.
    /// </summary>
    public static class DependencyInjection
    {
        /// <summary>
        /// Adds application services to the service collection.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="serverConfig">Server configuration from CLI arguments.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddApplicationServices(this IServiceCollection services, ServerConfiguration serverConfig)
        {
            services.AddSingleton<IMessageBus, MessageBus>();
            services.AddSingleton<INavigationService, NavigationService>();
            services.AddSingleton<IThemeManager, ThemeManager>();
            services.AddSingleton<ILogger>(sp => Log.Logger);
            services.AddSingleton<ICredentialStorageService, CredentialStorageService>();
            services.AddSingleton<IErrorHandlingService, ErrorHandlingService>();

            // Register AuthenticationService as singleton with named HttpClient
            services.AddHttpClient("AuthClient", client =>
            {
                client.BaseAddress = new Uri(serverConfig.HttpBaseUrl);
                client.Timeout = TimeSpan.FromSeconds(30);
            });
            services.AddSingleton<IAuthenticationService>(sp =>
            {
                var factory = sp.GetRequiredService<IHttpClientFactory>();
                var httpClient = factory.CreateClient("AuthClient");
                var credStore = sp.GetRequiredService<ICredentialStorageService>();
                var logger = sp.GetRequiredService<ILogger>();
                return new AuthenticationService(httpClient, credStore, logger);
            });

            services.AddTransient<AuthenticationMessageHandler>();

            // OfflineMessageQueue requires IAuthenticationService for per-user isolation
            services.AddSingleton<IOfflineMessageQueue>(sp =>
            {
                var logger = sp.GetRequiredService<ILogger>();
                var authService = sp.GetRequiredService<IAuthenticationService>();
                return new OfflineMessageQueue(logger, authService);
            });

            // MessagingService as Singleton with dedicated HttpClient
            services.AddHttpClient("MessagingClient", client =>
            {
                client.BaseAddress = new Uri(serverConfig.HttpBaseUrl);
                client.Timeout = TimeSpan.FromSeconds(30);
            })
            .AddHttpMessageHandler<AuthenticationMessageHandler>();

            services.AddSingleton<IMessagingService>(sp =>
            {
                var factory = sp.GetRequiredService<IHttpClientFactory>();
                var httpClient = factory.CreateClient("MessagingClient");
                var logger = sp.GetRequiredService<ILogger>();
                var offlineQueue = sp.GetRequiredService<IOfflineMessageQueue>();
                var errorHandler = sp.GetRequiredService<IErrorHandlingService>();
                var authService = sp.GetRequiredService<IAuthenticationService>();
                var config = sp.GetRequiredService<ServerConfiguration>();
                return new MessagingService(httpClient, logger, offlineQueue, errorHandler, authService, config);
            });

            // UserDirectoryService with handler
            services.AddHttpClient<IUserDirectoryService, UserDirectoryService>(client =>
            {
                client.BaseAddress = new Uri(serverConfig.HttpBaseUrl);
                client.Timeout = TimeSpan.FromSeconds(30);
            })
            .AddHttpMessageHandler<AuthenticationMessageHandler>();

            // FileAttachmentService with authenticated HttpClient
            services.AddHttpClient("FileAttachmentClient", client =>
            {
                client.BaseAddress = new Uri(serverConfig.HttpBaseUrl);
                client.Timeout = TimeSpan.FromMinutes(5); // Longer timeout for file uploads
            })
            .AddHttpMessageHandler<AuthenticationMessageHandler>();

            services.AddSingleton<IFileAttachmentService>(sp =>
            {
                var factory = sp.GetRequiredService<IHttpClientFactory>();
                var httpClient = factory.CreateClient("FileAttachmentClient");
                var logger = sp.GetRequiredService<ILogger>();
                var errorHandler = sp.GetRequiredService<IErrorHandlingService>();
                return new FileAttachmentService(httpClient, logger, errorHandler);
            });

            services.AddSingleton<IImageCompressionService>(sp =>
            {
                var logger = sp.GetRequiredService<ILogger>();
                return new ImageCompressionService(logger);
            });

            return services;
        }

        /// <summary>
        /// Adds view models to the service collection.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddViewModels(this IServiceCollection services)
        {
            services.AddSingleton<MainWindowViewModel>();
            services.AddTransient<WelcomeViewModel>();
            services.AddTransient<LoginViewModel>();
            services.AddTransient<RegisterViewModel>();
            services.AddTransient<ChatViewModel>();
            services.AddTransient<GeneratorViewModel>();

            return services;
        }

        /// <summary>
        /// Adds views to the service collection.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddViews(this IServiceCollection services)
        {
            services.AddSingleton<MainWindow>();
            services.AddTransient<WelcomeView>();
            services.AddTransient<LoginView>();
            services.AddTransient<RegisterView>();
            services.AddTransient<ChatView>();
            services.AddTransient<GeneratorView>();

            return services;
        }
    }
}
