// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PowerToys.DSC.UnitTests.SettingsResourceTests;

[TestClass]
public sealed class SettingsResourceAdvancedPasteModuleTest : SettingsResourceModuleTest<AdvancedPasteSettings>
{
    public SettingsResourceAdvancedPasteModuleTest()
        : base(nameof(ModuleType.AdvancedPaste))
    {
    }

    protected override Action<AdvancedPasteSettings> GetSettingsModifier()
    {
        return s =>
        {
            s.Properties.ShowCustomPreview = !s.Properties.ShowCustomPreview;
            s.Properties.CloseAfterLosingFocus = !s.Properties.CloseAfterLosingFocus;

            // s.Properties.IsAdvancedAIEnabled = !s.Properties.IsAdvancedAIEnabled;
            s.Properties.AdvancedPasteUIShortcut = new HotkeySettings
            {
                Key = "mock",
                Alt = true,
            };
        };
    }
}
