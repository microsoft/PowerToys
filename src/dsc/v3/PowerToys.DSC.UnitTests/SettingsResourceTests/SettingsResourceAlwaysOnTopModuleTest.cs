// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PowerToys.DSC.UnitTests.SettingsResourceTests;

[TestClass]
public sealed class SettingsResourceAlwaysOnTopModuleTest : SettingsResourceModuleTest<AlwaysOnTopSettings>
{
    public SettingsResourceAlwaysOnTopModuleTest()
        : base(nameof(ModuleType.AlwaysOnTop))
    {
    }

    protected override Action<AlwaysOnTopSettings> GetSettingsModifier()
    {
        return s =>
        {
            s.Properties.RoundCornersEnabled.Value = !s.Properties.RoundCornersEnabled.Value;
            s.Properties.FrameEnabled.Value = !s.Properties.FrameEnabled.Value;
        };
    }
}
