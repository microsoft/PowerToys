// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PowerToys.DSC.UnitTests.SettingsResourceTests;

[TestClass]
public sealed class SettingsResourceCropAndLockModuleTest : SettingsResourceModuleTest<CropAndLockSettings>
{
    public SettingsResourceCropAndLockModuleTest()
        : base(nameof(ModuleType.CropAndLock))
    {
    }

    protected override Action<CropAndLockSettings> GetSettingsModifier()
    {
        return s =>
        {
            s.Properties.ThumbnailHotkey = new KeyboardKeysProperty()
            {
                Value = new HotkeySettings
                {
                    Key = "mock",
                    Alt = true,
                },
            };
        };
    }
}
