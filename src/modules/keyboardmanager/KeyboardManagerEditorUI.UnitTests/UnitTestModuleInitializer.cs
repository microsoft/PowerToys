// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Runtime.CompilerServices;
using KeyboardManagerEditorUI.Settings;

namespace KeyboardManagerEditorUI.UnitTests
{
    internal static class UnitTestModuleInitializer
    {
        /// <summary>Gets the temporary folder that stands in for the Keyboard Manager settings folder.</summary>
        internal static string TestRoot { get; } = Path.Combine(Path.GetTempPath(), "KeyboardManagerEditorUI.UnitTests", Guid.NewGuid().ToString("N"));

        [ModuleInitializer]
        internal static void Initialize()
        {
            AppContext.SetSwitch(SettingsManager.DisableInitializationSwitch, true);

            // Point every Keyboard Manager store at a temporary folder before any test can touch a
            // file, so the tests never read or write the user's live settings.
            Directory.CreateDirectory(TestRoot);
            KeyboardManagerPaths.RedirectForTests(TestRoot);
        }
    }
}
