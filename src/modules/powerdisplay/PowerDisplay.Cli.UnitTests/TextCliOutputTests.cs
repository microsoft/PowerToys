// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerDisplay.Cli.Output;
using PowerDisplay.Contracts;

namespace PowerDisplay.Cli.UnitTests;

/// <summary>
/// Tests for <see cref="TextCliOutput"/>'s human-readable table rendering: the list/profile tables
/// must preserve the full monitor/profile name exactly (no UTF-16 code-unit truncation or fixed-width
/// padding) and must render as a delimiter-separated line, since this is human output rather than a
/// machine (JSON) contract.
/// </summary>
[TestClass]
public class TextCliOutputTests
{
    // A name containing a non-BMP emoji (surrogate pair), a combining mark, and CJK characters — all
    // of which a UTF-16 code-unit Substring/PadRight would mangle or split.
    private const string NonBmpName = "🖥️Monitor-测试-e\u0301xtra-long-name-that-would-have-been-truncated";

    [TestMethod]
    public void WriteListResult_LongNonBmpName_PreservedExactlyWithDelimiterFormat()
    {
        var stdout = new StringWriter();
        var output = new TextCliOutput(stdout, new StringWriter());

        output.WriteListResult(new CliListResult
        {
            Monitors = new List<CliMonitorRef>
            {
                new() { Number = 1, Id = "MON-1", Name = NonBmpName, Method = "DDC/CI" },
            },
        });

        var lines = stdout.ToString().Split('\n').Select(l => l.TrimEnd('\r')).ToList();

        // The row must contain the full, untruncated name — no "…" ellipsis marker.
        var row = lines.Single(l => l.Contains("MON-1"));
        StringAssert.Contains(row, NonBmpName);
        Assert.IsFalse(row.Contains('…'), "the row must not contain a truncation ellipsis");

        // Delimiter-separated, not fixed-width padded columns.
        var expectedRow = $"1 | {NonBmpName} | DDC/CI | MON-1";
        Assert.AreEqual(expectedRow, row);
    }

    [TestMethod]
    public void WriteProfileListResult_LongNonBmpName_PreservedExactlyWithDelimiterFormat()
    {
        var stdout = new StringWriter();
        var output = new TextCliOutput(stdout, new StringWriter());

        output.WriteProfileListResult(new CliProfileListResult
        {
            Profiles = new List<CliProfileInfo>
            {
                new() { Id = 7, Name = NonBmpName, MonitorCount = 2, LastModified = "2025-01-01T00:00:00Z" },
            },
        });

        var lines = stdout.ToString().Split('\n').Select(l => l.TrimEnd('\r')).ToList();
        var row = lines.Single(l => l.Contains("2025-01-01T00:00:00Z"));
        StringAssert.Contains(row, NonBmpName);
        Assert.IsFalse(row.Contains('…'), "the row must not contain a truncation ellipsis");

        var expectedRow = $"7 | {NonBmpName} | 2 | 2025-01-01T00:00:00Z";
        Assert.AreEqual(expectedRow, row);
    }

    [TestMethod]
    public void WriteApplyProfileResult_UsesBestEffortText_NotAppliedClaim()
    {
        var stdout = new StringWriter();
        var output = new TextCliOutput(stdout, new StringWriter());

        output.WriteApplyProfileResult(new CliApplyProfileResult { ProfileId = 3, Profile = "Office" });

        var text = stdout.ToString().Trim();
        Assert.AreEqual("Processed profile 'Office' (best effort).", text);
    }
}
