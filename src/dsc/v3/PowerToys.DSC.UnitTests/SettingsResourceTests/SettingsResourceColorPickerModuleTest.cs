// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PowerToys.DSC.UnitTests.SettingsResourceTests;

[TestClass]
public sealed class SettingsResourceColorPickerModuleTest : SettingsResourceModuleTest<ColorPickerSettings>
{
    public SettingsResourceColorPickerModuleTest()
        : base(nameof(ModuleType.ColorPicker))
    {
    }

    protected override Action<ColorPickerSettings> GetSettingsModifier()
    {
        return s =>
        {
            s.Properties.ShowColorName = !s.Properties.ShowColorName;
            s.Properties.ColorHistoryLimit = s.Properties.ColorHistoryLimit == 0 ? 10 : 0;
        };
    }
}
