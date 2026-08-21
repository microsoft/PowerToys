// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PowerToys.DSC.UnitTests.SettingsResourceTests;

[TestClass]
public sealed class SettingsResourceMouseButtonLockModuleTest : SettingsResourceModuleTest<MouseButtonLockSettings>
{
    public SettingsResourceMouseButtonLockModuleTest()
        : base(nameof(ModuleType.MouseButtonLock))
    {
    }

    protected override Action<MouseButtonLockSettings> GetSettingsModifier()
    {
        return s =>
        {
            s.Properties.LmbLockEnabled.Value = !s.Properties.LmbLockEnabled.Value;
            s.Properties.MmbLockEnabled.Value = !s.Properties.MmbLockEnabled.Value;
            s.Properties.HoldDurationMs.Value = 800;
            s.Properties.MoveCancelPixels.Value = 12;
        };
    }
}
