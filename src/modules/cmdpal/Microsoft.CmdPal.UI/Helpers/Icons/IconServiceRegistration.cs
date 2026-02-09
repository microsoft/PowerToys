// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Dispatching;

namespace Microsoft.CmdPal.UI.Helpers;

internal static class IconServiceRegistration
{
    public static IServiceCollection AddIconServices(this IServiceCollection services, DispatcherQueue dispatcherQueue)
    {
        // Single shared loader
        var loader = new IconLoaderService(dispatcherQueue);
        services.AddSingleton<IIconLoaderService>(loader);

        // Keyed providers by size
        services.AddKeyedSingleton<IIconSourceProvider>(
            WellKnownIconSize.Size20,
            (_, _) => new CachedIconSourceProvider(loader, 20, 1024));

        services.AddKeyedSingleton<IIconSourceProvider>(
            WellKnownIconSize.Size32,
            (_, _) => new IconSourceProvider(loader, 32));

        services.AddKeyedSingleton<IIconSourceProvider>(
            WellKnownIconSize.Size64,
            (_, _) => new CachedIconSourceProvider(loader, 64, 256));

        services.AddKeyedSingleton<IIconSourceProvider>(
            WellKnownIconSize.Size256,
            (_, _) => new CachedIconSourceProvider(loader, 256, 64));

        return services;
    }
}
