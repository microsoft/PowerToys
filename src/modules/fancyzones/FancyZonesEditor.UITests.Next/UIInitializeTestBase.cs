// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using FancyZonesEditor.UITests.Utils;
using FancyZonesEditorCommon.Data;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FancyZonesEditor.UITests;

public abstract class UIInitializeTestBase : FancyZonesEditorTestBase
{
    private const string Monitor1Name = "Monitor 1";
    private const string Monitor2Name = "Monitor 2";

    protected UIInitializeTestBase(Action<FancyZonesEditorFiles> seedFixture)
    {
        seedFixture(Files);
    }

    protected static Element FindLayoutByExactName(Session session, string layoutName)
    {
        var candidates = session.FindAll<Element>(By.Name(layoutName), 2_000)
            .Where(x =>
                string.Equals(x.Name, layoutName, StringComparison.Ordinal) &&
                string.Equals(x.ControlType, "ListItem", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.AreEqual(1, candidates.Count, $"Expected one exact layout named '{layoutName}', but found {candidates.Count}.");
        return candidates[0];
    }

    protected static Element FindMonitorByExactName(Session session, string monitorName)
    {
        var candidates = session.FindAll<Element>(By.Name(monitorName), 2_000)
            .Where(x =>
                string.Equals(x.Name, monitorName, StringComparison.Ordinal) &&
                string.Equals(x.ControlType, "ListItem", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.AreEqual(1, candidates.Count, $"Expected one exact monitor named '{monitorName}', but found {candidates.Count}.");
        return candidates[0];
    }

    protected static AppliedLayouts.AppliedLayoutsListWrapper ReadAppliedLayoutsDurably(
        UITestBase test,
        Func<AppliedLayouts.AppliedLayoutsListWrapper, bool> predicate,
        string reason)
    {
        EditorUiTestHelper.Step(test, $"Waiting for applied-layouts file update: {reason}");

        var stableResult = WaitHelper.WaitForStable(
            observe: () =>
            {
                try
                {
                    return EditorUiTestHelper.ReadAppliedLayouts();
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
                {
                    return (AppliedLayouts.AppliedLayoutsListWrapper?)null;
                }
            },
            isMatch: data => data.HasValue && predicate(data.Value),
            timeoutMS: 10_000,
            requiredConsecutiveMatches: 2,
            pollIntervalMS: 100);

        Assert.IsTrue(
            stableResult.Succeeded && stableResult.LastObservation.HasValue,
            $"The applied-layouts file did not reach the expected state for '{reason}'.");

        return stableResult.LastObservation.Value;
    }

    protected static void SelectAndAssertMonitor(UITestBase test, Session session, string monitorName)
    {
        EditorUiTestHelper.Step(test, $"Selecting {monitorName}");
        var monitor = FindMonitorByExactName(session, monitorName);
        monitor.Click();

        monitor = FindMonitorByExactName(session, monitorName);
        Assert.IsTrue(monitor.Selected, $"Expected {monitorName} to be selected.");
    }

    protected static string Monitor1 => Monitor1Name;

    protected static string Monitor2 => Monitor2Name;
}
