// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;

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

            // B/C/D add their singletons here, e.g.:
            //   services.AddSingleton<IUserSettings, UserSettings>();
            //   services.AddSingleton<IMainViewModel, MainViewModel>();
        }
    }
}
