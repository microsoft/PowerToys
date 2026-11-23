// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using Awake.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Awake.UnitTests;

[TestClass]
public class ExpirationTimeParserTests
{
    [TestMethod]
    [DataRow((string)null)]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow("bananas")]
    [DataRow("not-a-date")]
    public void TryParse_WhenInputIsNullEmptyWhitespaceOrInvalid_ReturnsInvalidFormat(string input)
    {
        DateTimeOffset now = new(2026, 11, 22, 14, 0, 0, TimeSpan.FromHours(-5));

        ExpirationTimeParseStatus status = ExpirationTimeParser.TryParse(
            input,
            now,
            CultureInfo.GetCultureInfo("en-US"),
            TimeZoneInfo.Utc,
            out DateTimeOffset result);

        Assert.AreEqual(ExpirationTimeParseStatus.InvalidFormat, status);
        Assert.AreEqual(default(DateTimeOffset), result);
    }
}
