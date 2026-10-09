// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PowerToys.DSC.UnitTests.SettingsResourceTests;

[TestClass]
public sealed class SettingsResourceAwakeModuleTest : SettingsResourceModuleTest<AwakeSettings>
{
    public SettingsResourceAwakeModuleTest()
        : base(nameof(ModuleType.Awake))
    {
    }

    protected override Action<AwakeSettings> GetSettingsModifier()
    {
        return s =>
        {
            s.Properties.ExpirationDateTime = DateTimeOffset.MinValue;
            s.Properties.IntervalHours = DefaultSettings.Properties.IntervalHours + 1;
            s.Properties.IntervalMinutes = DefaultSettings.Properties.IntervalMinutes + 1;
            s.Properties.Mode = s.Properties.Mode == AwakeMode.PASSIVE ? AwakeMode.TIMED : AwakeMode.PASSIVE;
            s.Properties.KeepDisplayOn = !s.Properties.KeepDisplayOn;
            s.Properties.CustomTrayTimes = new Dictionary<string, uint>
            {
                { "08:00", 1 },
                { "12:00", 2 },
                { "16:00", 3 },
            };
        };
    }
}
