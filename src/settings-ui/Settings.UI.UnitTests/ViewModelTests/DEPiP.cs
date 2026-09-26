// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading;

using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Controls;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.UnitTests.BackwardsCompatibility;
using Microsoft.PowerToys.Settings.UI.UnitTests.Mocks;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerToys.Interop;

namespace ViewModelTests;

[TestClass]
public class DEPiP
{
    [TestMethod]
    public void EnablingModuleSendsUpdatedGeneralSettings()
    {
        var repository = new BackCompatTestProperties.MockSettingsRepository<GeneralSettings>(
            ISettingsUtilsMocks.GetStubSettingsUtils<GeneralSettings>().Object);
        string sentMessage = null;
        var viewModel = new DEPiPViewModel(repository, new DEPiPSettings(), message =>
        {
            sentMessage = message;
            return 0;
        });

        viewModel.IsEnabled = true;

        Assert.IsTrue(repository.SettingsConfig.Enabled.DEPiP);
        Assert.IsFalse(string.IsNullOrWhiteSpace(sentMessage));
        StringAssert.Contains(sentMessage, "\"DEPiP\":true");
    }

    [TestMethod]
    public void LaunchSignalsSharedEvent()
    {
        var repository = new BackCompatTestProperties.MockSettingsRepository<GeneralSettings>(
            ISettingsUtilsMocks.GetStubSettingsUtils<GeneralSettings>().Object);
        var viewModel = new DEPiPViewModel(repository, new DEPiPSettings(), _ => 0);
        using var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, Constants.ShowDEPiPSharedEvent());

        viewModel.Launch();

        Assert.IsTrue(eventHandle.WaitOne(0));
    }

    [TestMethod]
    public void InactiveTransparencySendsUpdatedModuleSettings()
    {
        var repository = new BackCompatTestProperties.MockSettingsRepository<GeneralSettings>(
            ISettingsUtilsMocks.GetStubSettingsUtils<GeneralSettings>().Object);
        var moduleSettings = new DEPiPSettings();
        string sentMessage = null;
        var viewModel = new DEPiPViewModel(repository, moduleSettings, message =>
        {
            sentMessage = message;
            return 0;
        });

        viewModel.InactiveTransparency = 35;

        Assert.AreEqual(35, moduleSettings.Properties.InactiveTransparency.Value);
        StringAssert.Contains(sentMessage, "\"inactiveTransparency\":{\"value\":35}");
    }

    [TestMethod]
    public void LockAspectRatioSendsUpdatedModuleSettings()
    {
        var repository = new BackCompatTestProperties.MockSettingsRepository<GeneralSettings>(
            ISettingsUtilsMocks.GetStubSettingsUtils<GeneralSettings>().Object);
        var moduleSettings = new DEPiPSettings();
        string sentMessage = null;
        var viewModel = new DEPiPViewModel(repository, moduleSettings, message =>
        {
            sentMessage = message;
            return 0;
        });

        viewModel.LockAspectRatio = true;

        Assert.IsTrue(moduleSettings.Properties.LockAspectRatio.Value);
        StringAssert.Contains(sentMessage, "\"lockAspectRatio\":{\"value\":true}");
    }

    [TestMethod]
    public void AlwaysOnTopSendsUpdatedModuleSettings()
    {
        var repository = new BackCompatTestProperties.MockSettingsRepository<GeneralSettings>(
            ISettingsUtilsMocks.GetStubSettingsUtils<GeneralSettings>().Object);
        var moduleSettings = new DEPiPSettings();
        string sentMessage = null;
        var viewModel = new DEPiPViewModel(repository, moduleSettings, message =>
        {
            sentMessage = message;
            return 0;
        });

        viewModel.AlwaysOnTop = true;

        Assert.IsTrue(moduleSettings.Properties.AlwaysOnTop.Value);
        StringAssert.Contains(sentMessage, "\"alwaysOnTop\":{\"value\":true}");
    }

    [TestMethod]
    public void QuickAccessLaunchSignalsSharedEvent()
    {
        var launcher = new QuickAccessLauncher(false);
        using var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, Constants.ShowDEPiPSharedEvent());

        bool launched = launcher.Launch(ModuleType.DEPiP);

        Assert.IsTrue(launched);
        Assert.IsTrue(eventHandle.WaitOne(0));
    }
}
