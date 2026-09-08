// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.CompilerServices;
using KeyboardManagerEditorUI.Settings;

namespace KeyboardManagerEditorUI.UnitTests
{
    internal static class UnitTestModuleInitializer
    {
        [ModuleInitializer]
        internal static void Initialize()
        {
            AppContext.SetSwitch(SettingsManager.DisableInitializationSwitch, true);
        }
    }
}
