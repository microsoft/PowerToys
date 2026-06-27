// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;

namespace Microsoft.CommandPalette.Extensions.Toolkit;

public partial class ExtensionHost
{
    public static IExtensionHost? Host { get; private set; }

    public static void Initialize(IExtensionHost host) => Host = host;

    /// <summary>
    /// Fire-and-forget a log message to the Command Palette host app. Since
    /// the host is in another process, we do this in a try/catch in a
    /// background thread, as to not block the calling thread, nor explode if
    /// the host app is gone.
    /// </summary>
    /// <param name="message">The log message to send</param>
    private static void LogHostForwardingFailure(string message, Exception ex)
    {
        Trace.WriteLine($"CmdPal extension host forwarding failed: {message}\n{ex}");
    }

    public static void LogMessage(ILogMessage message)
    {
        if (Host is not null)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await Host.LogMessage(message);
                }
                catch (Exception ex)
                {
                    LogHostForwardingFailure("LogMessage", ex);
                }
            });
        }
    }

    public static void LogMessage(string message)
    {
        var logMessage = new LogMessage() { Message = message };
        LogMessage(logMessage);
    }

    public static void ShowStatus(IStatusMessage message, StatusContext context)
    {
        if (Host is not null)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await Host.ShowStatus(message, context);
                }
                catch (Exception ex)
                {
                    LogHostForwardingFailure("ShowStatus", ex);
                }
            });
        }
    }

    public static void HideStatus(IStatusMessage message)
    {
        if (Host is not null)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await Host.HideStatus(message);
                }
                catch (Exception ex)
                {
                    LogHostForwardingFailure("HideStatus", ex);
                }
            });
        }
    }
}
