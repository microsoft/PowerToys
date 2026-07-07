// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PowerDisplay.Ipc.UnitTests;

[TestClass]
public class LightSwitchProfileResolverTests
{
    [TestMethod]
    public void LightSwitchProperties_ProfileIds_RoundTripThroughJson_DefaultZero()
    {
        var props = new LightSwitchProperties();
        Assert.AreEqual(0, props.LightModeProfileId.Value);
        Assert.AreEqual(0, props.DarkModeProfileId.Value);

        props.LightModeProfileId.Value = 3;
        props.DarkModeProfileId.Value = 5;

        var json = JsonSerializer.Serialize(props);
        var back = JsonSerializer.Deserialize<LightSwitchProperties>(json);

        Assert.IsNotNull(back);
        Assert.AreEqual(3, back!.LightModeProfileId.Value);
        Assert.AreEqual(5, back.DarkModeProfileId.Value);
    }
}
