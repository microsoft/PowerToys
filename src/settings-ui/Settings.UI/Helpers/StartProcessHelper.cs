// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using ManagedCommon;

namespace Microsoft.PowerToys.Settings.UI.Helpers
{
    public static class StartProcessHelper
    {
        public const string ColorsSettings = "ms-settings:colors";
        public const string DiagnosticsAndFeedback = "ms-settings:privacy-feedback";
        public const string NightLightSettings = "ms-settings:nightlight";

        public static string AnimationsSettings => OSVersionHelper.IsWindows11()
            ? "ms-settings:easeofaccess-visualeffects"
            : "ms-settings:easeofaccess-display";

        public static void Start(string process)
        {
            Process.Start(new ProcessStartInfo(process) { UseShellExecute = true });
        }
    }
}
