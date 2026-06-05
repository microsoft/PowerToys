// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;

using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ViewModelTests
{
    [TestClass]
    public class AwakeSettingsTests
    {
        [TestMethod]
        public void Deserialization_FromPartialJson_PreservesProvidedValues()
        {
            string json = "{\"keepDisplayOn\":true,\"mode\":1}";

            AwakeProperties properties = JsonSerializer.Deserialize<AwakeProperties>(json);

            Assert.IsNotNull(properties);
            Assert.IsTrue(properties.KeepDisplayOn);
            Assert.AreEqual(AwakeMode.INDEFINITE, properties.Mode);
            Assert.AreEqual(1u, properties.IntervalMinutes);
        }
    }
}
