// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;

using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ViewModelTests
{
    [TestClass]
    public class WorkspacesSettingsTests
    {
        [TestMethod]
        public void Deserialization_FromPartialJson_PreservesProvidedValues()
        {
            const string json = "{\"sortby\":2}";

            var deserialized = JsonSerializer.Deserialize<WorkspacesProperties>(json);

            Assert.IsNotNull(deserialized);
            Assert.AreEqual(WorkspacesProperties.SortByProperty.Name, deserialized.SortBy);
            Assert.IsTrue(deserialized.Hotkey.Value.Win);
            Assert.IsTrue(deserialized.Hotkey.Value.Ctrl);
            Assert.IsFalse(deserialized.Hotkey.Value.Alt);
            Assert.IsFalse(deserialized.Hotkey.Value.Shift);
            Assert.AreEqual(0xC0, deserialized.Hotkey.Value.Code);
        }
    }
}
