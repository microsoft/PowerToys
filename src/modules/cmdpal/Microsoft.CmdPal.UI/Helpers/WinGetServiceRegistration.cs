// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using ManagedCommon;
using Microsoft.CmdPal.Common.WinGet.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.CmdPal.UI.Helpers;

internal static class WinGetServiceRegistration
{
    /// <summary>
    /// Creates and registers WinGet services. Returns the created instances so
    /// they can be used before the container is built (e.g. for built-in command setup).
    /// Returns <c>null</c> when WinGet is unavailable (not installed, too old, running as admin, etc.).
    /// </summary>
    public static (IWinGetPackageManagerService PackageManager, IWinGetOperationTrackerService OperationTracker)?
        AddWinGetServices(this IServiceCollection services)
    {
        try
        {
            var operationTracker = new WinGetOperationTrackerService();
            var packageManager = new WinGetPackageManagerService(operationTracker);

            services.AddSingleton<IWinGetOperationTrackerService>(operationTracker);
            services.AddSingleton<IWinGetPackageManagerService>(packageManager);
            services.AddSingleton<IWinGetPackageStatusService, WinGetPackageStatusService>();

            return (packageManager, operationTracker);
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to initialize WinGet services", ex);
            return null;
        }
    }
}
