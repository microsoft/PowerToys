// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using ManagedCommon;
using Microsoft.Extensions.Logging;

namespace Microsoft.CmdPal.UI;

internal sealed class LogWrapper : Microsoft.Extensions.Logging.ILogger
{
    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        var message = formatter(state, exception);
        switch (logLevel)
        {
            case LogLevel.Trace:
                Logger.LogTrace();
                break;
            case LogLevel.Debug:
                Logger.LogDebug(message);
                break;
            case LogLevel.Information:
                Logger.LogInfo(message);
                break;
            case LogLevel.Warning:
                Logger.LogWarning(message);
                break;
            case LogLevel.Error:
            case LogLevel.Critical:
                if (exception is not null)
                {
                    Logger.LogError(message, exception);
                }
                else
                {
                    Logger.LogError(message);
                }

                break;
        }
    }
}
