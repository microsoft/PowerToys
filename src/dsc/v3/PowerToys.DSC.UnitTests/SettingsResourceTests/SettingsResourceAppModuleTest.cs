// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerToys.DSC.DSCResources;

namespace PowerToys.DSC.UnitTests.SettingsResourceTests;

[TestClass]
public sealed class SettingsResourceAppModuleTest : SettingsResourceModuleTest<GeneralSettings>
{
    public SettingsResourceAppModuleTest()
        : base(SettingsResource.AppModule)
    {
    }

    protected override Action<GeneralSettings> GetSettingsModifier()
    {
        return s =>
        {
            s.Startup = !s.Startup;
            s.ShowSysTrayIcon = !s.ShowSysTrayIcon;
            s.Enabled.Awake = !s.Enabled.Awake;
            s.Enabled.ColorPicker = !s.Enabled.ColorPicker;
        };
    }
}
