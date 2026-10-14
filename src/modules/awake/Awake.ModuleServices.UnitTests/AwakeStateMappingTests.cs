// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Awake.ModuleServices.UnitTests;

[TestClass]
public sealed class AwakeStateMappingTests
{
    [TestMethod]
    public void CreateState_TimedSettings_ReturnsTimedStateWithDuration()
    {
        var settings = new AwakeSettings
        {
            Properties =
            {
                Mode = AwakeMode.TIMED,
                KeepDisplayOn = true,
                IntervalHours = 1,
                IntervalMinutes = 30,
                ExpirationDateTime = new DateTimeOffset(2030, 1, 2, 3, 4, 5, TimeSpan.Zero),
            },
        };

        var state = AwakeService.CreateState(isRunning: true, settings);

        Assert.IsTrue(state.IsRunning);
        Assert.AreEqual(AwakeStateMode.Timed, state.Mode);
        Assert.IsTrue(state.KeepDisplayOn);
        Assert.AreEqual(TimeSpan.FromMinutes(90), state.Duration);
        Assert.IsNull(state.Expiration);
    }
}
