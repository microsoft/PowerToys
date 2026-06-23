// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

using MEL = Microsoft.Extensions.Logging;

namespace Microsoft.CmdPal.Common.Logging;

public static class CmdPalLoggingExtensions
{
    /// <summary>
    /// Registers the Microsoft.Extensions.Logging infrastructure and adds a
    /// <see cref="CmdPalLoggerProvider"/> that routes all <see cref="MEL.ILogger"/>
    /// output to <see cref="ManagedCommon.Logger"/>.
    /// </summary>
    public static IServiceCollection AddCmdPalLogging(this IServiceCollection services)
    {
        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.Services.TryAddEnumerable(
                ServiceDescriptor.Singleton<MEL.ILoggerProvider, CmdPalLoggerProvider>());
        });

        return services;
    }
}
