// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CmdPal.UI.ViewModels.Services.JsonRpc;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public class BoundedStderrReaderTests
{
    private static readonly TimeSpan LongWindow = TimeSpan.FromMinutes(5);
    private static readonly string[] HelloWorldLines = { "hello", "world" };
    private static readonly string[] TrailingLine = { "no trailing newline" };
    private static readonly string[] RealLine = { "real" };

    [TestMethod]
    public async Task Pump_ForwardsLines_AndHandlesCrLf()
    {
        var lines = new List<string>();
        var reader = new BoundedStderrReader(lines.Add, rateWindow: LongWindow);

        await reader.PumpAsync(StreamFrom("hello\r\nworld\n"), CancellationToken.None);

        CollectionAssert.AreEqual(HelloWorldLines, lines);
        Assert.AreEqual(2, reader.LinesEmitted);
    }

    [TestMethod]
    public async Task Pump_ForwardsTrailingLine_WithoutNewline()
    {
        var lines = new List<string>();
        var reader = new BoundedStderrReader(lines.Add, rateWindow: LongWindow);

        await reader.PumpAsync(StreamFrom("no trailing newline"), CancellationToken.None);

        CollectionAssert.AreEqual(TrailingLine, lines);
    }

    [TestMethod]
    public async Task Pump_TruncatesOversizedSingleLine()
    {
        var lines = new List<string>();
        var reader = new BoundedStderrReader(lines.Add, maxLineBytes: 16, rateWindow: LongWindow);

        var oversized = new string('x', 5000);
        await reader.PumpAsync(StreamFrom(oversized + "\n"), CancellationToken.None);

        Assert.AreEqual(1, lines.Count);
        Assert.AreEqual(1, reader.LinesTruncated);
        StringAssert.Contains(lines[0], "(truncated)");

        // The retained content must be bounded by the per-line cap (which has a 64-byte floor)
        // regardless of the 5000-byte input size.
        Assert.IsTrue(lines[0].Length <= 64 + " ... (truncated)".Length, $"Line length {lines[0].Length} exceeded the cap.");
    }

    [TestMethod]
    public async Task Pump_KeepsContinuousStderrBounded()
    {
        var lines = new List<string>();
        const long MaxTotal = 256;
        var reader = new BoundedStderrReader(lines.Add, maxTotalBytes: MaxTotal, maxLineBytes: 64, maxLinesPerWindow: int.MaxValue, rateWindow: LongWindow);

        var builder = new StringBuilder();
        for (var i = 0; i < 10_000; i++)
        {
            builder.Append("this is a noisy extension log line ").Append(i).Append('\n');
        }

        await reader.PumpAsync(StreamFrom(builder.ToString()), CancellationToken.None);

        Assert.IsTrue(reader.BudgetExhausted, "The total-volume cap should have been reached.");
        Assert.IsTrue(reader.LinesSuppressed > 0, "Lines beyond the cap should have been suppressed.");

        // Total forwarded content stays within the cap plus at most one final line.
        Assert.IsTrue(reader.TotalLoggedBytes <= MaxTotal + 64, $"Total logged bytes {reader.TotalLoggedBytes} exceeded the bound.");
    }

    [TestMethod]
    public async Task Pump_RateLimitsLinesPerWindow()
    {
        var lines = new List<string>();
        var reader = new BoundedStderrReader(lines.Add, maxLinesPerWindow: 2, rateWindow: LongWindow);

        await reader.PumpAsync(StreamFrom("a\nb\nc\nd\ne\n"), CancellationToken.None);

        Assert.AreEqual(2, reader.LinesEmitted);
        Assert.AreEqual(3, reader.LinesSuppressed);
    }

    [TestMethod]
    public async Task Pump_IgnoresBlankLines()
    {
        var lines = new List<string>();
        var reader = new BoundedStderrReader(lines.Add, rateWindow: LongWindow);

        await reader.PumpAsync(StreamFrom("\n   \n\t\nreal\n"), CancellationToken.None);

        CollectionAssert.AreEqual(RealLine, lines);
    }

    private static Stream StreamFrom(string text) => new MemoryStream(Encoding.UTF8.GetBytes(text));
}
