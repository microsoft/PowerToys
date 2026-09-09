// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.IO.Abstractions;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.PowerToys.Settings.UI.Views;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.Settings.UI.UnitTests;

[TestClass]
public class LightSwitchSettingsWatcherTests
{
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
                // Use the CLI's exact atomic replacement flags, after both files are closed.
                const uint moveFileReplaceExisting = 0x1;
                const uint moveFileWriteThrough = 0x8;
                Assert.IsTrue(
                    MoveFileExW(temporaryPath, settingsPath, moveFileReplaceExisting | moveFileWriteThrough),
                    $"MoveFileExW failed: {Marshal.GetLastWin32Error()}");
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

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool MoveFileExW(string existingFileName, string newFileName, uint flags);
}
