// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Text.Json;

using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace ViewModelTests
{
    [TestClass]
    public class MouseWithoutBorders
    {
        private sealed class TestMachine
        {
            public string Name { get; init; }
        }

        [TestMethod]
        public void SaveMachineMatrixPersistsReorderedMachinesOnce()
        {
            var settings = new MouseWithoutBordersSettings();
            var settingsUtils = new Mock<SettingsUtils>(new FileSystem(), null);
            var expected = new List<string> { "second", "first", string.Empty, string.Empty };
            var machines = new IndexedObservableCollection<TestMachine>(
                new List<TestMachine>
                {
                    new TestMachine { Name = "first" },
                    new TestMachine { Name = "second" },
                    new TestMachine { Name = string.Empty },
                    new TestMachine { Name = string.Empty },
                });

            machines.Swap(0, 1);

            MouseWithoutBordersMachineMatrixPersistence.Save(
                settingsUtils.Object,
                settings,
                machines.ToEnumerable(),
                static machine => machine.Name);

            CollectionAssert.AreEqual(expected, settings.Properties.MachineMatrixString);
            settingsUtils.Verify(
                utils => utils.SaveSettings(
                    It.Is<string>(json => MatrixMatches(json, expected)),
                    MouseWithoutBordersSettings.ModuleName,
                    SettingsUtils.DefaultFileName),
                Times.Once);
        }

        [TestMethod]
        public void SaveMachineMatrixLeavesSettingsUnchangedWhenNameProjectionFails()
        {
            var settings = new MouseWithoutBordersSettings();
            settings.Properties.MachineMatrixString.Add("existing");
            var settingsUtils = new Mock<SettingsUtils>(new FileSystem(), null);
            var machines = new List<TestMachine> { new TestMachine { Name = "first" } };

            Assert.ThrowsException<InvalidOperationException>(() =>
                MouseWithoutBordersMachineMatrixPersistence.Save(
                    settingsUtils.Object,
                    settings,
                    machines,
                    static _ => throw new InvalidOperationException()));

            CollectionAssert.AreEqual(new List<string> { "existing" }, settings.Properties.MachineMatrixString);
            settingsUtils.Verify(
                utils => utils.SaveSettings(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
                Times.Never);
        }

        private static bool MatrixMatches(string json, List<string> expected)
        {
            var persisted = JsonSerializer.Deserialize<MouseWithoutBordersSettings>(json);
            CollectionAssert.AreEqual(expected, persisted.Properties.MachineMatrixString);
            return true;
        }
    }
}
