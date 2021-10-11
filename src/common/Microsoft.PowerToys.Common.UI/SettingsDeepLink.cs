// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.IO;

namespace Microsoft.PowerToys.Common.UI
{
    public static class SettingsDeepLink
    {
        public static void OpenSettings(string powerToysRelativePath, string module)
        {
            try
            {
                var assemblyPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                var fullPath = Directory.GetParent(assemblyPath).FullName;
                Process.Start(new ProcessStartInfo(fullPath + "\\" + powerToysRelativePath) { Arguments = "--open-settings=" + module });
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch
#pragma warning restore CA1031 // Do not catch general exception types
            {
                // TODO(stefan): Log exception once unified logging is implemented
            }
        }
    }
}
