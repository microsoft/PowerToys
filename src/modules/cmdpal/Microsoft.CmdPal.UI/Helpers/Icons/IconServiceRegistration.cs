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
        services.AddSingleton<ManagedIconSourceFactory>();

        // Single shared loader — resolved via the container so ManagedIconSourceFactory
        // (and its ILogger) are injected automatically.
        services.AddSingleton(sp =>
        {
            var factory = sp.GetRequiredService<ManagedIconSourceFactory>();
            return new IconLoaderService(dispatcherQueue, factory);
        });
        services.AddSingleton<IIconLoaderService>(sp => sp.GetRequiredService<IconLoaderService>());

        // Keyed providers by size
        services.AddKeyedSingleton<IIconSourceProvider>(
            WellKnownIconSize.Size16,
            (sp, _) => new IconSourceProvider(sp.GetRequiredService<IconLoaderService>(), 16));
        services.AddSingleton<IIconSourceProvider>(sp => sp.GetRequiredKeyedService<IIconSourceProvider>(WellKnownIconSize.Size16));

        services.AddKeyedSingleton(
            WellKnownIconSize.Size20,
            (sp, _) => new CachedIconSourceProvider(sp.GetRequiredService<IconLoaderService>(), 20, 1024, "Size 20"));
        services.AddKeyedSingleton<IIconSourceProvider>(
            WellKnownIconSize.Size20,
            (sp, key) => sp.GetRequiredKeyedService<CachedIconSourceProvider>(key));
        services.AddSingleton<IIconSourceProvider>(sp => sp.GetRequiredKeyedService<IIconSourceProvider>(WellKnownIconSize.Size20));

        services.AddKeyedSingleton<IIconSourceProvider>(
            WellKnownIconSize.Size32,
            (sp, _) => new IconSourceProvider(sp.GetRequiredService<IconLoaderService>(), 32));
        services.AddSingleton<IIconSourceProvider>(sp => sp.GetRequiredKeyedService<IIconSourceProvider>(WellKnownIconSize.Size32));

        services.AddKeyedSingleton(
            WellKnownIconSize.Size64,
            (sp, _) => new CachedIconSourceProvider(sp.GetRequiredService<IconLoaderService>(), 64, 256, "Size 64"));
        services.AddKeyedSingleton<IIconSourceProvider>(
            WellKnownIconSize.Size64,
            (sp, key) => sp.GetRequiredKeyedService<CachedIconSourceProvider>(key));
        services.AddSingleton<IIconSourceProvider>(sp => sp.GetRequiredKeyedService<IIconSourceProvider>(WellKnownIconSize.Size64));

        services.AddKeyedSingleton(
            WellKnownIconSize.Size256,
            (sp, _) => new CachedIconSourceProvider(sp.GetRequiredService<IconLoaderService>(), 256, 64, "Size 256"));
        services.AddKeyedSingleton<IIconSourceProvider>(
            WellKnownIconSize.Size256,
            (sp, key) => sp.GetRequiredKeyedService<CachedIconSourceProvider>(key));
        services.AddSingleton<IIconSourceProvider>(sp => sp.GetRequiredKeyedService<IIconSourceProvider>(WellKnownIconSize.Size256));

        services.AddKeyedSingleton<IIconSourceProvider>(
            WellKnownIconSize.Unbound,
            (sp, _) => new IconSourceProvider(sp.GetRequiredService<IconLoaderService>(), IconLoaderService.NoResize, isPriority: true));
        services.AddSingleton<IIconSourceProvider>(sp => sp.GetRequiredKeyedService<IIconSourceProvider>(WellKnownIconSize.Unbound));

        return services;
    }
}
