// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Awake.Core;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Awake.UnitTests;

[TestClass]
public class ManagerExpirableTests
{
    [TestMethod]
    public void SetExpirableKeepAwake_WhenExpireAtIsInThePast_ReturnsFalseAndDoesNotSetExpirableMode()
    {
        AwakeMode previousMode = Manager.CurrentOperatingMode;
        DateTimeOffset past = DateTimeOffset.Now.AddMinutes(-5);

        bool started = Manager.SetExpirableKeepAwake(past, keepDisplayOn: false);

        Assert.IsFalse(started);
        Assert.AreEqual(previousMode, Manager.CurrentOperatingMode);
    }

    [TestMethod]
    public void SetExpirableKeepAwake_WhenExpireAtIsNowOrEarlier_ReturnsFalse()
    {
        bool started = Manager.SetExpirableKeepAwake(DateTimeOffset.Now, keepDisplayOn: false);

        Assert.IsFalse(started);
    }
}
