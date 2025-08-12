// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using ManagedCommon;

namespace Microsoft.PowerToys.Settings.UI.Library.Helpers
{
    /// <summary>
    /// Centralized constants for PowerToys module names to avoid hardcoding strings throughout the codebase
    /// </summary>
    public static class ModuleNames
    {
        // Core module names - use these as the single source of truth
        public const string AdvancedPaste = "advancedpaste";
        public const string AlwaysOnTop = "alwaysontop";
        public const string CmdPal = "cmdpal";
        public const string ColorPicker = "colorpicker";
        public const string CropAndLock = "cropandlock";
        public const string FancyZones = "fancyzones";
        public const string FindMyMouse = "findmymouse";
        public const string MeasureTool = "measure tool";
        public const string MouseHighlighter = "mousehighlighter";
        public const string MouseJump = "mousejump";
        public const string MousePointerCrosshairs = "mousepointercrosshairs";
        public const string MouseWithoutBorders = "mousewithoutborders";
        public const string Peek = "peek";
        public const string PowerLauncher = "powertoys run";
        public const string PowerOcr = "powerocr";
        public const string ShortcutGuide = "shortcut guide";
        public const string TextExtractor = "textextractor";
        public const string Workspaces = "workspaces";
        public const string ZoomIt = "zoomit";
        public const string MouseUtils = "mouseutils";

        // Display names for UI
        public static class DisplayNames
        {
            public const string AdvancedPaste = "AdvancedPaste";
            public const string AlwaysOnTop = "AlwaysOnTop";
            public const string ColorPicker = "ColorPicker";
            public const string CropAndLock = "CropAndLock";
            public const string MeasureTool = "Measure Tool";
            public const string MouseUtils = "MouseUtils";
            public const string MouseWithoutBorders = "MouseWithoutBorders";
            public const string Peek = "Peek";
            public const string PowerLauncher = "PowerToys Run";
            public const string TextExtractor = "TextExtractor";
            public const string ShortcutGuide = "Shortcut Guide";
            public const string Workspaces = "Workspaces";
            public const string ZoomIt = "ZoomIt";
            public const string FancyZones = "FancyZones";
        }

        // Module groupings
        public static readonly string[] MouseUtilsModules =
        {
            MouseHighlighter,
            MouseJump,
            MousePointerCrosshairs,
            FindMyMouse,
        };

        // Module mappings
        public static readonly Dictionary<string, string> ModuleKeyMapping = new(StringComparer.OrdinalIgnoreCase)
        {
            { MouseHighlighter, "mouseutils" },
            { MouseJump, "mouseutils" },
            { MousePointerCrosshairs, "mouseutils" },
            { FindMyMouse, "mouseutils" },
            { PowerOcr, TextExtractor },
        };

        /// <summary>
        /// Module name to ModuleType mapping for conversion
        /// </summary>
        private static readonly Dictionary<string, ModuleType> ModuleNameToTypeMapping = new(StringComparer.OrdinalIgnoreCase)
        {
            { AdvancedPaste, ModuleType.AdvancedPaste },
            { AlwaysOnTop, ModuleType.AlwaysOnTop },
            { CmdPal, ModuleType.CmdPal },
            { ColorPicker, ModuleType.ColorPicker },
            { CropAndLock, ModuleType.CropAndLock },
            { FancyZones, ModuleType.FancyZones },
            { FindMyMouse, ModuleType.FindMyMouse },
            { MeasureTool, ModuleType.MeasureTool },
            { MouseHighlighter, ModuleType.MouseHighlighter },
            { MouseJump, ModuleType.MouseJump },
            { MousePointerCrosshairs, ModuleType.MousePointerCrosshairs },
            { MouseWithoutBorders, ModuleType.MouseWithoutBorders },
            { Peek, ModuleType.Peek },
            { PowerLauncher, ModuleType.PowerLauncher },
            { PowerOcr, ModuleType.PowerOCR },
            { ShortcutGuide, ModuleType.ShortcutGuide },
            { TextExtractor, ModuleType.PowerOCR },
            { Workspaces, ModuleType.Workspaces },
            { ZoomIt, ModuleType.ZoomIt },
        };

        /// <summary>
        /// Gets the module key used for ViewModel factories and internal routing
        /// </summary>
        public static string GetModuleKey(string moduleName)
        {
            if (string.IsNullOrEmpty(moduleName))
            {
                return null;
            }

            return ModuleKeyMapping.TryGetValue(moduleName, out var mappedKey)
                ? mappedKey
                : moduleName.ToLowerInvariant();
        }

        /// <summary>
        /// Checks if a module is part of MouseUtils
        /// </summary>
        public static bool IsMouseUtilsModule(string moduleName)
        {
            return MouseUtilsModules.Contains(moduleName?.ToLowerInvariant());
        }

        /// <summary>
        /// Converts a module name string to the corresponding ManagedCommon.ModuleType enum value
        /// </summary>
        /// <param name="moduleName">The module name to convert</param>
        /// <returns>The corresponding ModuleType enum value, or null if no match is found</returns>
        public static ModuleType? ToModuleType(string moduleName)
        {
            if (string.IsNullOrEmpty(moduleName))
            {
                return null;
            }

            return ModuleNameToTypeMapping.TryGetValue(moduleName.Trim(), out var moduleType)
                ? moduleType
                : null;
        }
    }
}
