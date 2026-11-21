// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using Awake.Core;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Awake.UnitTests;

[TestClass]
public class TrayOptionsTests
{
    [TestMethod]
    public void GetDefaultTrayOptions_ReturnsSharedDefaultsInAscendingOrder()
    {
        Dictionary<string, uint> options = Manager.GetDefaultTrayOptions();

        CollectionAssert.AreEqual(AwakeDefaults.TrayTimeSeconds.ToList(), options.Values.ToList());
        CollectionAssert.AreEqual(options.Values.OrderBy(seconds => seconds).ToList(), options.Values.ToList());
    }

    [TestMethod]
    public void GetDefaultTrayOptions_IncludesDurationsLongerThanTwoHours()
    {
        Dictionary<string, uint> options = Manager.GetDefaultTrayOptions();

        Assert.IsTrue(options.Values.Any(seconds => seconds > 2 * 3600));
        Assert.AreEqual(12u * 3600, options.Values.Max());
    }

    [TestMethod]
    [DataRow(1800u, "30")]
    [DataRow(3600u, "1")]
    [DataRow(28800u, "8")]
    public void FormatTrayDuration_IncludesTheDurationNumber(uint seconds, string expectedNumber)
    {
        string label = Manager.FormatTrayDuration(seconds);

        Assert.IsFalse(string.IsNullOrWhiteSpace(label));
        StringAssert.Contains(label, expectedNumber);
    }
}
