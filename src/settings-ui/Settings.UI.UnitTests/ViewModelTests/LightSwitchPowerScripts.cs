// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ViewModelTests
{
    [TestClass]
    public class LightSwitchPowerScripts
    {
        [TestMethod]
        public void Defaults_DoNotRunPowerScripts()
        {
            var properties = new LightSwitchProperties();

            Assert.IsFalse(properties.EnableDarkModePowerScript.Value);
            Assert.IsFalse(properties.EnableLightModePowerScript.Value);
            Assert.AreEqual(string.Empty, properties.DarkModePowerScript.Value);
            Assert.AreEqual(string.Empty, properties.LightModePowerScript.Value);
            Assert.AreEqual("{}", properties.DarkModePowerScriptParameters.Value);
            Assert.AreEqual("{}", properties.LightModePowerScriptParameters.Value);
        }

        [TestMethod]
        public void Clone_PreservesPowerScriptAssignments()
        {
            var settings = new LightSwitchSettings();
            settings.Properties.EnableDarkModePowerScript.Value = true;
            settings.Properties.EnableLightModePowerScript.Value = true;
            settings.Properties.DarkModePowerScript.Value = "dark-script";
            settings.Properties.LightModePowerScript.Value = "light-script";
            settings.Properties.DarkModePowerScriptParameters.Value = """{"level":"2"}""";
            settings.Properties.LightModePowerScriptParameters.Value = """{"message":"hello"}""";

            var clone = (LightSwitchSettings)settings.Clone();

            Assert.IsTrue(clone.Properties.EnableDarkModePowerScript.Value);
            Assert.IsTrue(clone.Properties.EnableLightModePowerScript.Value);
            Assert.AreEqual("dark-script", clone.Properties.DarkModePowerScript.Value);
            Assert.AreEqual("light-script", clone.Properties.LightModePowerScript.Value);
            Assert.AreEqual("""{"level":"2"}""", clone.Properties.DarkModePowerScriptParameters.Value);
            Assert.AreEqual("""{"message":"hello"}""", clone.Properties.LightModePowerScriptParameters.Value);
        }
    }
}
