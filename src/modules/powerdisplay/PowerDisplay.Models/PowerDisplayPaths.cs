// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;

namespace PowerDisplay.Models
{
    /// <summary>
    /// Single source of truth for PowerDisplay-related filesystem paths. Lives in
    /// PowerDisplay.Models because Settings UI references this project but not
    /// PowerDisplay.Lib; PowerDisplay.Lib's <c>PathConstants</c> delegates to this type
    /// for the same paths.
    /// </summary>
    public static class PowerDisplayPaths
    {
        /// <summary>
        /// %LOCALAPPDATA%\Microsoft\PowerToys
        /// </summary>
        public static string PowerToysBaseFolder
            => Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Microsoft",
                "PowerToys");

        /// <summary>
        /// %LOCALAPPDATA%\Microsoft\PowerToys\PowerDisplay
        /// </summary>
        public static string PowerDisplayFolder
            => Path.Combine(PowerToysBaseFolder, "PowerDisplay");

        /// <summary>
        /// %LOCALAPPDATA%\Microsoft\PowerToys\PowerDisplay\discovery.lock
        /// Existence at PowerDisplay.exe startup indicates the previous run crashed
        /// inside DDC/CI capability fetch.
        /// </summary>
        public static string DiscoveryLockPath
            => Path.Combine(PowerDisplayFolder, "discovery.lock");

        /// <summary>
        /// %LOCALAPPDATA%\Microsoft\PowerToys\PowerDisplay\crash_detected.flag
        /// UI signal — Settings UI shows the auto-disable InfoBar when this exists.
        /// </summary>
        public static string CrashDetectedFlagPath
            => Path.Combine(PowerDisplayFolder, "crash_detected.flag");

        /// <summary>
        /// %LOCALAPPDATA%\Microsoft\PowerToys\settings.json — global PowerToys settings
        /// (NOT the per-module file). PowerDisplay.exe Phase 0 mutates enabled.PowerDisplay here.
        /// </summary>
        public static string GlobalPowerToysSettingsPath
            => Path.Combine(PowerToysBaseFolder, "settings.json");
    }
}
