// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Common;

public partial class ExtensionHostInstance
{
    public IExtensionHost? Host { get; private set; }

    public void Initialize(IExtensionHost host) => Host = host;

    /// <summary>
    /// Fire-and-forget a log message to the Command Palette host app. Since
    /// the host is in another process, we do this in a try/catch in a
    /// background thread, as to not block the calling thread, nor explode if
    /// the host app is gone.
    /// </summary>
    /// <param name="message">The log message to send</param>
    public void LogMessage(ILogMessage message)
    {
        if (Host != null)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await Host.LogMessage(message);
                }
                catch (Exception)
                {
                }
            });
        }
    }

    public void LogMessage(string message)
    {
        var logMessage = new LogMessage() { Message = message };
        LogMessage(logMessage);
    }

    public void ShowStatus(IStatusMessage message, StatusContext context)
    {
        if (Host != null)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await Host.ShowStatus(message, context);
                }
                catch (Exception)
                {
                }
            });
        }
    }

    public void HideStatus(IStatusMessage message)
    {
        if (Host != null)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await Host.HideStatus(message);
                }
                catch (Exception)
                {
                }
            });
        }
    }
}
