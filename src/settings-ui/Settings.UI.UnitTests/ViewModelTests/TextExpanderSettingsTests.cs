// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

extern alias PowerToysSettings;

using System;
using System.ComponentModel;
using System.IO;
using System.IO.Abstractions;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.PowerToys.TextExpander.Storage;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using GpoRuleConfigured = PowerToysSettings::PowerToys.GPOWrapper.GpoRuleConfigured;

namespace ViewModelTests;

[TestClass]
public sealed class TextExpanderSettingsTests
{
    [TestMethod]
    public void ManageSnippetsWorksWhileExpansionIsDisabled()
    {
        int launches = 0;
        TextExpanderViewModel model = Create(launch: () => launches++);
        Assert.IsFalse(model.IsEnabled);
        Assert.IsTrue(model.CanLaunchEditor);
        model.LaunchEditor();
        Assert.AreEqual(1, launches);
    }

    [TestMethod]
    public void DisabledPolicyPreventsEditorLaunchAndEnablement()
    {
        int launches = 0;
        TextExpanderViewModel model = Create(launch: () => launches++, policy: GpoRuleConfigured.Disabled);
        model.IsEnabled = true;
        model.LaunchEditor();
        Assert.IsFalse(model.IsEnabled);
        Assert.IsTrue(model.IsEnabledGpoConfigured);
        Assert.IsFalse(model.CanLaunchEditor);
        Assert.AreEqual(0, launches);
    }

    [TestMethod]
    public void ElevatedSettingsDoesNotLaunchAnElevatedSnippetEditor()
    {
        int launches = 0;
        TextExpanderViewModel model = Create(launch: () => launches++, isElevated: true);
        model.LaunchEditor();
        Assert.IsFalse(model.CanLaunchEditor);
        Assert.AreEqual(0, launches);
    }

    [TestMethod]
    public void FailedLaunchIsVisibleAndCanBeRetried()
    {
        int attempts = 0;
        TextExpanderViewModel model = Create(launch: () =>
        {
            if (++attempts == 1)
            {
                throw new Win32Exception("Test launch failure.");
            }
        });
        model.LaunchEditor();
        Assert.IsTrue(model.HasEditorLaunchError);
        model.LaunchEditor();
        Assert.IsFalse(model.HasEditorLaunchError);
        Assert.AreEqual(2, attempts);
    }

    [TestMethod]
    public void SettingsPageDoesNotExposeSnippetManagementState()
    {
        Assert.IsNull(typeof(TextExpanderViewModel).GetProperty("Snippets"));
        Assert.IsNull(typeof(TextExpanderViewModel).GetProperty("SnippetFileSummary"));
        Assert.IsNull(typeof(TextExpanderViewModel).GetMethod("ApplyFolderChange"));
    }

    [TestMethod]
    public void FailedOptionsSaveIsVisibleAndCanBeRetried()
    {
        var store = new Mock<TextExpanderSettingsUtils>(new FileSystem(), null);
        store.SetupSequence(instance => instance.UpdateOptions(It.IsAny<Action<TextExpanderProperties>>()))
            .Throws(new IOException("Test save failure."))
            .Returns(new TextExpanderSettings());
        TextExpanderViewModel model = Create(() => { }, settingsStore: store);
        model.CaseSensitive = false;
        Assert.IsTrue(model.HasSettingsSaveError);
        model.CaseSensitive = false;
        Assert.IsFalse(model.HasSettingsSaveError);
    }

    [TestMethod]
    public void UnreadableLatestOptionsAreReportedInsteadOfOverwritten()
    {
        var store = new Mock<TextExpanderSettingsUtils>(new FileSystem(), null);
        store.Setup(instance => instance.UpdateOptions(It.IsAny<Action<TextExpanderProperties>>()))
            .Throws(new InvalidOperationException("The settings file could not be read consistently."));
        TextExpanderViewModel model = Create(() => { }, settingsStore: store);
        model.RequireWordBoundary = false;
        Assert.IsTrue(model.HasSettingsSaveError);
        Assert.IsTrue(model.RequireWordBoundary);
    }

    private static TextExpanderViewModel Create(
        Action launch,
        GpoRuleConfigured policy = GpoRuleConfigured.NotConfigured,
        bool isElevated = false,
        Mock<TextExpanderSettingsUtils> settingsStore = null)
    {
        var utils = settingsStore ?? new Mock<TextExpanderSettingsUtils>(new FileSystem(), null);
        utils.Setup(instance => instance.GetSettingsOrDefault<TextExpanderSettings>(
            TextExpanderSettings.ModuleName,
            SettingsUtils.DefaultFileName)).Returns(new TextExpanderSettings());
        var repository = new Mock<ISettingsRepository<GeneralSettings>>();
        repository.SetupGet(instance => instance.SettingsConfig).Returns(new GeneralSettings());
        return new TextExpanderViewModel(utils.Object, repository.Object, _ => 0, isElevated, launch, () => policy);
    }
}
