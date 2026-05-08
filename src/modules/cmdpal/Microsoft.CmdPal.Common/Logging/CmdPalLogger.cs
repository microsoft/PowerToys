// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Extensions.Logging;
using MEL = Microsoft.Extensions.Logging;

namespace Microsoft.CmdPal.Common.Logging;

/// <summary>
/// An <see cref="ILogger"/> implementation that delegates to <see cref="ManagedCommon.Logger"/>.
/// Instances are created by <see cref="CmdPalLoggerProvider"/>.
/// </summary>
public sealed class CmdPalLogger(string categoryName) : MEL.ILogger
{
    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
        {
            return;
        }

        ArgumentNullException.ThrowIfNull(formatter);

        var message = $"[{categoryName}] {formatter(state, exception)}";

        switch (logLevel)
        {
            case LogLevel.Trace:
                ManagedCommon.Logger.LogTrace(message);
                break;
            case LogLevel.Debug:
                ManagedCommon.Logger.LogDebug(message);
                break;
            case LogLevel.Information:
                ManagedCommon.Logger.LogInfo(message);
                break;
            case LogLevel.Warning:
                ManagedCommon.Logger.LogWarning(message);
                break;
            case LogLevel.Error:
            case LogLevel.Critical:
                if (exception is not null)
                {
                    ManagedCommon.Logger.LogError(message, exception);
                }
                else
                {
                    ManagedCommon.Logger.LogError(message);
                }

                break;
            case LogLevel.None:
            default:
                break;
        }
    }
}
