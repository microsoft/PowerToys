// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.IO;
using System.Threading;
using Microsoft.PowerToys.Settings.UI.Library;

namespace RunnerV2.Helpers
{
    internal static class AIHelper
    {
        public static void DetectAiCapabilities(bool skipSettingsCheck = false)
        {
            new Thread(() => DetectAiCapabilitiesInternal(skipSettingsCheck)).Start();
        }

        private static void DetectAiCapabilitiesInternal(bool skipSettingsCheck)
        {
            if (!skipSettingsCheck)
            {
                var generalSettings = SettingsUtils.Default.GetSettings<GeneralSettings>();
                if (!generalSettings.Enabled.ImageResizer)
                {
                    return;
                }
            }

            if (!Path.Exists("WinUI3Apps\\PowerToys.ImageResizer.exe"))
            {
                return;
            }

            var p = Process.Start("WinUI3Apps\\PowerToys.ImageResizer.exe", "--detect-ai");
            p.WaitForExit(30000);
            p.Close();
        }
    }
}
