// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Awake.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Awake.UnitTests;

[TestClass]
public class CountdownFormattingTests
{
    [DataRow(0, "0:00")]
    [DataRow(1, "0:01")]
    [DataRow(59, "0:59")]
    [DataRow(60, "1:00")]
    [DataRow(3599, "59:59")]
    [DataRow(3600, "1:00:00")]
    [DataRow(5400, "1:30:00")]
    [DataRow(90061, "25:01:01")]
    [DataTestMethod]
    public void ToHumanReadableString_Countdown_FormatsWithoutWrappingDays(int seconds, string expected)
    {
        Assert.AreEqual(expected, TimeSpan.FromSeconds(seconds).ToHumanReadableString());
    }
}
