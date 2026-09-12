// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.IO.Abstractions;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;
using Microsoft.PowerToys.Settings.UI.Views;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Microsoft.PowerToys.Settings.UI.UnitTests;

[TestClass]
public class LightSwitchSettingsWatcherTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    [Timeout(15000)]
    public async Task InitialSettingsReadIncludesReplacementsBeforeAndDuringInitializationAsync(bool replaceDuringRead)
    {
        var directory = Directory.CreateTempSubdirectory("PowerToys-LightSwitchInitialRead-");
        try
        {
            var settingsPath = Path.Combine(directory.FullName, "settings.json");
            var temporaryPath = Path.Combine(directory.FullName, "settings.json.cli.tmp");
            var cachedSettings = new LightSwitchSettings();
            cachedSettings.Properties.ScheduleMode.Value = "FixedHours";
            cachedSettings.Properties.LightTime.Value = 497;
            cachedSettings.Properties.DarkTime.Value = 1260;
            File.WriteAllText(settingsPath, cachedSettings.ToJsonString());

            var replacement = (LightSwitchSettings)cachedSettings.Clone();
            replacement.Properties.ScheduleMode.Value = "Off";
            File.WriteAllText(temporaryPath, replacement.ToJsonString());

            var repository = new Mock<ISettingsRepository<LightSwitchSettings>>();
            repository.SetupGet(r => r.SettingsConfig).Returns(() => cachedSettings);
            repository.Setup(r => r.ReloadSettings()).Returns(() =>
            {
                if (replaceDuringRead)
                {
                    ReplaceSettingsFile(temporaryPath, settingsPath);
                }

                cachedSettings = JsonSerializer.Deserialize<LightSwitchSettings>(File.ReadAllText(settingsPath))
                    ?? throw new InvalidDataException("The test settings could not be deserialized.");
                return true;
            });

            if (!replaceDuringRead)
            {
                ReplaceSettingsFile(temporaryPath, settingsPath);
            }

            Assert.AreEqual("FixedHours", repository.Object.SettingsConfig.Properties.ScheduleMode.Value);
            var changed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var initialization = LightSwitchPage.InitializeSettings(
                new FileSystem(), settingsPath, repository.Object, () => changed.TrySetResult());
            using var watcher = initialization.Watcher;

            Assert.AreEqual("Off", initialization.Settings.Properties.ScheduleMode.Value);
            Assert.AreEqual(497, initialization.Settings.Properties.LightTime.Value);
            Assert.AreEqual(1260, initialization.Settings.Properties.DarkTime.Value);
            repository.Verify(r => r.ReloadSettings(), Times.Once);

            if (!replaceDuringRead)
            {
                File.WriteAllText(temporaryPath, replacement.ToJsonString());
                ReplaceSettingsFile(temporaryPath, settingsPath);
            }

            // A replacement during the initial reload is observed because the
            // production helper subscribes before reading the current snapshot.
            await changed.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [TestMethod]
    public void InitialSettingsReadRetainsTheRepositoryFallbackWhenReloadFails()
    {
        var directory = Directory.CreateTempSubdirectory("PowerToys-LightSwitchReadFallback-");
        try
        {
            var cachedSettings = new LightSwitchSettings();
            cachedSettings.Properties.ScheduleMode.Value = "FixedHours";
            var repository = new Mock<ISettingsRepository<LightSwitchSettings>>();
            repository.SetupGet(r => r.SettingsConfig).Returns(cachedSettings);
            repository.Setup(r => r.ReloadSettings()).Returns(false);

            var initialization = LightSwitchPage.InitializeSettings(
                new FileSystem(), Path.Combine(directory.FullName, "settings.json"), repository.Object, () => { });
            using var watcher = initialization.Watcher;

            Assert.AreSame(cachedSettings, initialization.Settings);
            repository.Verify(r => r.ReloadSettings(), Times.Once);
            Assert.IsTrue(watcher.EnableRaisingEvents);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    [Timeout(15000)]
    public async Task SettingsWritesNotifyReloadAsync(bool replaceFile)
    {
        var directory = Directory.CreateTempSubdirectory("PowerToys-LightSwitchWatcher-");
        try
        {
            var settingsPath = Path.Combine(directory.FullName, "settings.json");
            var temporaryPath = Path.Combine(directory.FullName, "settings.json.cli.tmp");
            const string updatedSettings = "{\"properties\":{\"scheduleMode\":{\"value\":\"Off\"}}}";
            File.WriteAllText(settingsPath, "{\"properties\":{\"scheduleMode\":{\"value\":\"FixedHours\"}}}");
            File.WriteAllText(temporaryPath, updatedSettings);

            var changed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var watcher = LightSwitchPage.CreateSettingsWatcher(
                new FileSystem(), settingsPath, () => changed.TrySetResult());

            if (replaceFile)
            {
                ReplaceSettingsFile(temporaryPath, settingsPath);
            }
            else
            {
                File.WriteAllText(settingsPath, updatedSettings);
            }

            await changed.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.AreEqual(updatedSettings, File.ReadAllText(settingsPath));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    private static void ReplaceSettingsFile(string temporaryPath, string settingsPath)
    {
        // Use the CLI's exact atomic replacement flags, after both files are closed.
        const uint moveFileReplaceExisting = 0x1;
        const uint moveFileWriteThrough = 0x8;
        Assert.IsTrue(
            MoveFileExW(temporaryPath, settingsPath, moveFileReplaceExisting | moveFileWriteThrough),
            $"MoveFileExW failed: {Marshal.GetLastWin32Error()}");
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool MoveFileExW(string existingFileName, string newFileName, uint flags);
}
