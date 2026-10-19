// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PowerToys.DSC.UnitTests.SettingsResourceTests;

[TestClass]
public sealed class SettingsResourceCursorBrakeModuleTest : SettingsResourceModuleTest<CursorBrakeSettings>
{
    public SettingsResourceCursorBrakeModuleTest()
        : base(nameof(ModuleType.CursorBrake))
    {
    }

    protected override Action<CursorBrakeSettings> GetSettingsModifier()
    {
        return s =>
        {
            s.Properties.PushDistance.Value = 450;
            s.Properties.FreeReturn.Value = !s.Properties.FreeReturn.Value;
            s.Properties.EdgeFilter.Value = 2;
            s.Properties.DisableWhileDragging.Value = !s.Properties.DisableWhileDragging.Value;
            s.Properties.CrossModifier.Value = 1;
        };
    }
}
