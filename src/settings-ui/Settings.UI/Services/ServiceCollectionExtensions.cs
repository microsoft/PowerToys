// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;
using Microsoft.PowerToys.Settings.UI.ViewModels;

namespace Microsoft.PowerToys.Settings.UI.Services
{
    /// <summary>
    /// Extension methods for configuring services in the dependency injection container.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Adds core Settings UI services to the service collection.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddSettingsServices(this IServiceCollection services)
        {
            // Register singleton services
            services.AddSingleton<ISettingsUtils>(SettingsUtils.Default);
            services.AddSingleton<ThemeService>(App.ThemeService);
            services.AddSingleton<INavigationService, NavigationServiceWrapper>();

            // Register settings repositories as singletons
            services.AddSingleton(sp =>
            {
                var settingsUtils = sp.GetRequiredService<ISettingsUtils>();
                return SettingsRepository<GeneralSettings>.GetInstance((SettingsUtils)settingsUtils);
            });

            return services;
        }

        /// <summary>
        /// Adds ViewModel registrations to the service collection.
        /// ViewModels are registered as transient to create new instances per request.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddViewModels(this IServiceCollection services)
        {
            // Register ViewModels as transient for fresh instances
            // These can be migrated incrementally as each ViewModel is updated

            // Tier 1 ViewModels (low complexity) - to be migrated first
            // services.AddTransient<FileLocksmithViewModel>();
            // services.AddTransient<RegistryPreviewViewModel>();
            // services.AddTransient<CropAndLockViewModel>();

            // Tier 2 ViewModels (medium complexity)
            // services.AddTransient<ColorPickerViewModel>();
            // services.AddTransient<AlwaysOnTopViewModel>();
            // services.AddTransient<PowerOcrViewModel>();
            // services.AddTransient<HostsViewModel>();

            // Tier 3 ViewModels (medium-high complexity)
            // services.AddTransient<FancyZonesViewModel>();
            // services.AddTransient<PowerLauncherViewModel>();
            // services.AddTransient<KeyboardManagerViewModel>();

            // Tier 4 ViewModels (high complexity) - migrate last
            // services.AddTransient<GeneralViewModel>();
            // services.AddTransient<DashboardViewModel>();
            // services.AddTransient<ShellViewModel>();
            return services;
        }
    }
}
