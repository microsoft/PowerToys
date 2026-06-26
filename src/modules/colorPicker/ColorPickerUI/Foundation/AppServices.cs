// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;

using ColorPicker.Helpers;
using Microsoft.Extensions.DependencyInjection;

namespace ColorPicker.Foundation
{
    /// <summary>
    /// Builds the application's dependency-injection container.
    /// Sub-projects B (editor), C (overlay), and D (infrastructure) add their
    /// own registrations to <see cref="Register"/> as each service is migrated.
    /// </summary>
    /// <remarks>
    /// Public (not internal) so the unit-test project can call <see cref="Configure"/>
    /// without depending on an <c>InternalsVisibleTo</c> assembly-name match.
    /// </remarks>
    public static class AppServices
    {
        public static IServiceProvider Configure()
        {
            var services = new ServiceCollection();
            Register(services);
            return services.BuildServiceProvider();
        }

        private static void Register(IServiceCollection services)
        {
            // Replaces the WPF App's [Export] ExitToken: a single source whose
            // token is cancelled when the runner exits / the app shuts down.
            services.AddSingleton<CancellationTokenSource>();
            services.AddSingleton(typeof(CancellationToken), sp =>
                (object)sp.GetRequiredService<CancellationTokenSource>().Token);

            // D6 infrastructure singletons.
            // SINGLETON: replaces the WPF [Export(typeof(IThrottledActionInvoker))]; holds a single
            // UI-thread DispatcherQueueTimer. Resolve only on the UI thread (ctor binds the timer).
            services.AddSingleton<IThrottledActionInvoker, ThrottledActionInvoker>();

            // D7 settings singleton: holds the live settings.json FileSystemWatcher + in-memory
            // ColorHistory. SINGLETON, registered AFTER IThrottledActionInvoker (its ctor dep).
            services.AddSingleton<ColorPicker.Settings.IUserSettings, ColorPicker.Settings.UserSettings>();

            // Sub-project C overlay + editor wiring (7e-1). All singletons; the graph is acyclic:
            // MainViewModel -> {MouseInfoProvider, ZoomWindowHelper, AppStateHandler, KeyboardMonitor},
            // and AppStateHandler -> IColorEditorViewModel. AppStateHandler reads App.Window, which
            // App.OnLaunched assigns before resolving IMainViewModel.
            services.AddSingleton<ColorPicker.ViewModelContracts.IColorEditorViewModel, ColorPicker.ViewModels.ColorEditorViewModel>();
            services.AddSingleton<ColorPicker.ViewModelContracts.IZoomViewModel, ColorPicker.ViewModels.ZoomViewModel>();
            services.AddSingleton<AppStateHandler>();
            services.AddSingleton<ZoomWindowHelper>();
            services.AddSingleton<ColorPicker.Mouse.IMouseInfoProvider, ColorPicker.Mouse.MouseInfoProvider>();
            services.AddSingleton<ColorPicker.Keyboard.KeyboardMonitor>();
            services.AddSingleton<ColorPicker.ViewModelContracts.IMainViewModel, ColorPicker.ViewModels.MainViewModel>();
        }
    }
}
