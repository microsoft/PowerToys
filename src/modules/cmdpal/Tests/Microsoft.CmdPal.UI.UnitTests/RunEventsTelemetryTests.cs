// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Reflection;
using Microsoft.CmdPal.UI.Events;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.UnitTests;

[TestClass]
public class RunEventsTelemetryTests
{
    [TestMethod]
    public void RunEventsDoNotExposeStringPayloads()
    {
        Type[] eventTypes =
        [
            typeof(CmdPalRunQuery),
            typeof(CmdPalRunCommand),
            typeof(CmdPalOpenUri),
            typeof(CmdPalRunBuildListPathResolution),
            typeof(CmdPalRunCreatePathItemsFiltered),
            typeof(CmdPalRunBuildItemsForDirectory),
            typeof(CmdPalRunLoadHistory),
            typeof(CmdPalRunLoadHistoryItem),
        ];

        foreach (Type eventType in eventTypes)
        {
            List<PropertyInfo> stringProperties = eventType
                .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(property => property.PropertyType == typeof(string))
                .ToList();

            Assert.AreEqual(0, stringProperties.Count, $"{eventType.Name} must not carry user-generated text or paths.");
        }
    }

    [TestMethod]
    public void AggregateRunEventsRetainExpectedValues()
    {
        var query = new CmdPalRunQuery(resultCount: 12, durationMs: 34);
        var pathResolution = new CmdPalRunBuildListPathResolution(
            withLeadingTilde: true,
            couldResolvePath: true,
            isFile: false,
            durationMs: 56,
            result: 0);
        var historyItem = new CmdPalRunLoadHistoryItem(
            timedOut: false,
            totalMs: 78,
            parseMs: 9,
            isUri: true,
            parseResult: 0);

        Assert.AreEqual(12, query.ResultCount);
        Assert.AreEqual(34UL, query.DurationMs);
        Assert.IsTrue(pathResolution.WithLeadingTilde);
        Assert.IsTrue(pathResolution.CouldResolvePath);
        Assert.IsFalse(pathResolution.IsFile);
        Assert.AreEqual(56L, pathResolution.DurationMs);
        Assert.AreEqual(0, pathResolution.Result);
        Assert.IsFalse(historyItem.TimedOut);
        Assert.AreEqual(78L, historyItem.TotalMs);
        Assert.AreEqual(9L, historyItem.ParseMs);
        Assert.IsTrue(historyItem.IsUri);
        Assert.AreEqual(0, historyItem.ParseResult);
    }
}
