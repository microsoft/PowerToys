// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.PowerToys.Settings.UI.Library;

namespace Microsoft.PowerToys.Settings.UI.Services
{
    /// <summary>
    /// Central service provider for the application's dependency injection container.
    /// </summary>
    public static class AppServices
    {
        private static readonly object _lock = new object();
        private static IServiceProvider _serviceProvider;

        /// <summary>
        /// Gets the service provider instance.
        /// </summary>
        public static IServiceProvider ServiceProvider
        {
            get
            {
                if (_serviceProvider == null)
                {
                    throw new InvalidOperationException(
                        "ServiceProvider has not been initialized. Call Configure() during app startup.");
                }

                return _serviceProvider;
            }
        }

        /// <summary>
        /// Gets a value indicating whether the service provider has been configured.
        /// </summary>
        public static bool IsConfigured => _serviceProvider != null;

        /// <summary>
        /// Configures the dependency injection container with all required services.
        /// This should be called once during application startup.
        /// </summary>
        /// <param name="configureServices">Optional action to add additional services.</param>
        public static void Configure(Action<IServiceCollection> configureServices = null)
        {
            lock (_lock)
            {
                if (_serviceProvider != null)
                {
                    return; // Already configured
                }

                var services = new ServiceCollection();

                // Register core services
                ConfigureCoreServices(services);

                // Allow additional configuration
                configureServices?.Invoke(services);

                _serviceProvider = services.BuildServiceProvider();
            }
        }

        /// <summary>
        /// Gets a service of type T from the DI container.
        /// </summary>
        /// <typeparam name="T">The type of service to get.</typeparam>
        /// <returns>The service instance, or null if not registered.</returns>
        public static T GetService<T>()
            where T : class
        {
            return ServiceProvider.GetService<T>();
        }

        /// <summary>
        /// Gets a required service of type T from the DI container.
        /// Throws if the service is not registered.
        /// </summary>
        /// <typeparam name="T">The type of service to get.</typeparam>
        /// <returns>The service instance.</returns>
        public static T GetRequiredService<T>()
            where T : class
        {
            return ServiceProvider.GetRequiredService<T>();
        }

        private static void ConfigureCoreServices(IServiceCollection services)
        {
            // Navigation service - singleton for app-wide navigation
            services.AddSingleton<INavigationService, NavigationServiceAdapter>();

            // Settings utilities - singleton
            services.AddSingleton(_ => SettingsUtils.Default);

            // Settings service - singleton for centralized settings management
            services.AddSingleton<ISettingsService, SettingsService>();

            // IPC service - singleton for inter-process communication
            services.AddSingleton<IIPCService, IPCService>();

            // General settings repository - singleton to share settings across pages
            services.AddSingleton(sp =>
            {
                var settingsUtils = sp.GetRequiredService<SettingsUtils>();
                return SettingsRepository<GeneralSettings>.GetInstance(settingsUtils);
            });

            // Resource loader - singleton
            services.AddSingleton(_ => Helpers.ResourceLoaderInstance.ResourceLoader);

            // ViewModelLocator for XAML binding (future use)
            services.AddSingleton(sp => new ViewModelLocator(sp, enableCaching: false));
        }
    }
}
