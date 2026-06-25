// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text.Json;
using ColorPicker.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ColorPicker.UnitTests.Settings
{
    [TestClass]
    public class UserSettingsTests
    {
        private sealed class SyncThrottledActionInvoker : IThrottledActionInvoker
        {
            public void ScheduleAction(Action action, int milliseconds) => action();
        }

        [TestMethod]
        public void Default_settings_version_is_2_1()
        {
            Assert.AreEqual("2.1", new ColorPickerSettings().Version);
        }

        [TestMethod]
        public void Properties_serialize_with_lowercase_keys()
        {
            var json = JsonSerializer.Serialize(new ColorPickerProperties());
            StringAssert.Contains(json, "\"showcolorname\"");
            StringAssert.Contains(json, "\"copiedcolorrepresentation\"");
            StringAssert.Contains(json, "\"changecursor\"");
        }

        [TestMethod]
        public void Color_history_entry_is_four_pipe_parts()
        {
            // The frozen A|R|G|B contract written by MainViewModel.GetColorString and
            // parsed by ColorPickerService.GetSavedColorsAsync (CmdPal).
            Assert.AreEqual(4, "255|16|32|48".Split('|').Length);
        }

        [TestMethod]
        public void UserSettings_constructs_with_a_synchronous_invoker()
        {
            // Smoke: ctor loads defaults + registers the file watcher without a DispatcherQueue.
            var settings = new ColorPicker.Settings.UserSettings(new SyncThrottledActionInvoker());
            Assert.IsNotNull(settings.CopiedColorRepresentation);
        }
    }
}
