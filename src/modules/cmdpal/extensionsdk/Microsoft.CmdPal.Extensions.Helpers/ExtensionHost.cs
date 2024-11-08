// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.Extensions.Helpers;

public class ExtensionHost
{
    private static IExtensionHost? _host;

    public static IExtensionHost? Host => _host;

    public static void Initialize(IExtensionHost host)
    {
        _host = host;
    }

    public static void LogMessage(ILogMessage message)
    {
        // TODO this feels like bad async
        if (Host != null)
        {
            // really just fire-and-forget
            new Task(async () =>
            {
                try
                {
                    await Host.LogMessage(message);
                }
                catch (Exception)
                {
                }
            }).Start();
        }
    }
}
