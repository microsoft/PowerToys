// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

extern alias PowerToysSettings;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using System.Reflection;
using System.Text;
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

    [TestMethod]
    public void UnreadableOptionsFileIsReportedAndLeftUnchanged()
    {
        // The engine accepts a wrapped bool, but the Settings model can't read it.
        byte[] unreadable = Encoding.UTF8.GetBytes("{\"properties\":{\"snippets_path\":{\"value\":\"C:\\\\Snippets\"},\"case_sensitive\":{\"value\":false}}}");
        var fileSystem = new MockFileSystem();
        string path = new SettingsUtils(fileSystem).GetSettingsFilePath(TextExpanderSettings.ModuleName);
        fileSystem.AddFile(path, new MockFileData(unreadable));

        TextExpanderViewModel model = Create(() => { }, fileSystem: fileSystem);
        model.CaseSensitive = false;

        Assert.IsTrue(model.HasSettingsReadError);
        Assert.IsTrue(model.HasSettingsSaveError);
        Assert.IsTrue(model.CaseSensitive, "Defaults are shown while the file can't be read.");
        CollectionAssert.AreEqual(unreadable, fileSystem.File.ReadAllBytes(path));

        fileSystem.File.WriteAllText(path, "{\"properties\":{\"case_sensitive\":false}}");
        model.RefreshEnabledState();
        Assert.IsFalse(model.HasSettingsReadError);
        Assert.IsFalse(model.CaseSensitive);
    }

    [TestMethod]
    public void MissingOptionsFileShowsDefaultsWithoutAnError()
    {
        var fileSystem = new MockFileSystem();

        TextExpanderViewModel model = Create(() => { }, fileSystem: fileSystem);

        Assert.IsFalse(model.HasSettingsReadError);
        Assert.IsTrue(model.RequireWordBoundary);
        Assert.AreEqual(TextExpanderSettings.DefaultClipboardThresholdChars, model.ClipboardThresholdChars);
        Assert.IsFalse(fileSystem.File.Exists(new SettingsUtils(fileSystem).GetSettingsFilePath(TextExpanderSettings.ModuleName)));
    }

    [TestMethod]
    public void MinimumLengthToPasteAppliesOnlyToAutomatic()
    {
        var settings = new TextExpanderSettings();
        var store = new Mock<TextExpanderSettingsUtils>(new FileSystem(), null);
        store.Setup(instance => instance.UpdateOptions(It.IsAny<Action<TextExpanderProperties>>()))
            .Returns((Action<TextExpanderProperties> update) =>
            {
                update(settings.Properties);
                return settings;
            });
        TextExpanderViewModel model = Create(() => { }, settingsStore: store);
        var changed = new List<string>();
        model.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        Assert.IsTrue(model.IsClipboardThresholdEnabled, "Automatic is the default.");

        model.InjectionBackendIndex = 1;
        Assert.AreEqual("clipboard", settings.Properties.InjectionBackend.Value);
        Assert.IsFalse(model.IsClipboardThresholdEnabled, "Always paste ignores the length.");
        CollectionAssert.Contains(changed, nameof(TextExpanderViewModel.IsClipboardThresholdEnabled));

        model.InjectionBackendIndex = 2;
        Assert.AreEqual("type", settings.Properties.InjectionBackend.Value);
        Assert.IsFalse(model.IsClipboardThresholdEnabled, "Always type ignores the length.");

        changed.Clear();
        model.InjectionBackendIndex = 0;
        Assert.AreEqual("auto", settings.Properties.InjectionBackend.Value);
        Assert.IsTrue(model.IsClipboardThresholdEnabled, "Automatic pastes or types by length.");
        CollectionAssert.Contains(changed, nameof(TextExpanderViewModel.IsClipboardThresholdEnabled));
    }

    [TestMethod]
    public void ExternalEnableAndDisableRefreshEveryBinding()
    {
        var general = new GeneralSettings();
        TextExpanderViewModel model = Create(() => { }, general: general);
        var changed = new List<string>();
        model.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        // Quick Access and the Dashboard change the shared general settings, then ShellPage refreshes the open page.
        foreach (bool enabled in new[] { true, false })
        {
            changed.Clear();
            general.Enabled.TextExpander = enabled;
            model.RefreshEnabledState();

            Assert.AreEqual(enabled, model.IsEnabled);
            Assert.IsTrue(model.CanLaunchEditor, "Manage snippets doesn't depend on expansion being on.");
            foreach (PropertyInfo property in typeof(TextExpanderViewModel).GetProperties())
            {
                Assert.IsTrue(changed.Exists(name => string.IsNullOrEmpty(name) || name == property.Name), $"{property.Name} is not refreshed.");
            }
        }
    }

    private static TextExpanderViewModel Create(
        Action launch,
        GpoRuleConfigured policy = GpoRuleConfigured.NotConfigured,
        bool isElevated = false,
        Mock<TextExpanderSettingsUtils> settingsStore = null,
        GeneralSettings general = null,
        IFileSystem fileSystem = null)
    {
        var utils = settingsStore ?? new Mock<TextExpanderSettingsUtils>(new FileSystem(), null);
        utils.Setup(instance => instance.GetSettingsOrDefaultReadOnly<TextExpanderSettings>(
            TextExpanderSettings.ModuleName,
            SettingsUtils.DefaultFileName)).Returns(new TextExpanderSettings());
        var repository = new Mock<ISettingsRepository<GeneralSettings>>();
        repository.SetupGet(instance => instance.SettingsConfig).Returns(general ?? new GeneralSettings());
        TextExpanderSettingsUtils store = fileSystem is null ? utils.Object : new TextExpanderSettingsUtils(fileSystem);
        return new TextExpanderViewModel(store, repository.Object, _ => 0, isElevated, launch, () => policy);
    }
}
