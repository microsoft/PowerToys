// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO.Abstractions.TestingHelpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using static Microsoft.PowerToys.Settings.UI.Library.SetSettingCommandLineCommand;

namespace Settings.UI.UnitTests.Cmd;

[TestClass]
public class SetSettingCommandTests
{
    private SettingsUtils settingsUtils;

    [TestInitialize]
    public void Setup()
    {
        settingsUtils = new SettingsUtils(new MockFileSystem());
    }

    private void SetSetting(Type moduleSettingsType, string settingName, string newValueStr)
    {
        var settings = CommandLineUtils.GetSettingsConfigFor(moduleSettingsType, settingsUtils);
        var defaultValue = CommandLineUtils.GetPropertyValue(settingName, settings);
        var qualifiedName = moduleSettingsType.Name.Replace("Settings", string.Empty) + "." + settingName;
        var type = CommandLineUtils.GetSettingPropertyInfo(settingName, settings).PropertyType;
        var newValue = ICmdLineRepresentable.ParseFor(type, newValueStr);

        Execute(qualifiedName, newValueStr, settingsUtils);

        Assert.AreNotEqual(defaultValue, newValue);
        Assert.AreEqual(newValue, CommandLineUtils.GetPropertyValue(settingName, settings));
    }

    // Each setting has a different type.
    [TestMethod]
    [DataRow(typeof(PowerRenameSettings), nameof(PowerRenameProperties.MaxMRUSize), "123")]
    [DataRow(typeof(FancyZonesSettings), nameof(FZConfigProperties.FancyzonesBorderColor), "#00FF00")]
    [DataRow(typeof(MeasureToolSettings), nameof(MeasureToolProperties.ActivationShortcut), "Ctrl+Alt+Delete")]
    [DataRow(typeof(AlwaysOnTopSettings), nameof(AlwaysOnTopProperties.SoundEnabled), "False")]
    [DataRow(typeof(PowerAccentSettings), nameof(PowerAccentProperties.ShowUnicodeDescription), "true")]
    [DataRow(typeof(AwakeSettings), nameof(AwakeProperties.Mode), "EXPIRABLE")]
    [DataRow(typeof(AwakeSettings), nameof(AwakeProperties.ExpirationDateTime), "March 31, 2020 15:00 +00:00")]
    [DataRow(typeof(PowerLauncherSettings), nameof(PowerLauncherProperties.MaximumNumberOfResults), "322")]

    [DataRow(typeof(ColorPickerSettings), nameof(ColorPickerProperties.CopiedColorRepresentation), "RGB")]
    public void SetModuleSetting(Type moduleSettingsType, string settingName, string newValueStr)
    {
        SetSetting(moduleSettingsType, settingName, newValueStr);
    }

    [DataRow(typeof(GeneralSettings), "Enabled.MouseWithoutBorders", "true")]
    [DataRow(typeof(GeneralSettings), nameof(GeneralSettings.AutoDownloadUpdates), "true")]
    [TestMethod]
    public void SetGeneralSetting(Type moduleSettingsType, string settingName, string newValueStr)
    {
        SetSetting(moduleSettingsType, settingName, newValueStr);
    }
}
