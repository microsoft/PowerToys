// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.TextExpander.UITests;

/// <summary>
/// Exact-name lookups. winappcli searches names and AutomationIds by substring, so "5 snippets" also finds
/// "Showing 1 of 5 snippets", and a dialog's "CloseButton" also finds an info bar's close button.
/// </summary>
internal static class SessionExtensions
{
    /// <summary>Elements whose name is exactly <paramref name="name"/>, optionally of one control type.</summary>
    public static IReadOnlyList<T> FindExact<T>(this Session session, string name, string? controlType = null)
        where T : Element, new() =>
        session.FindAll<T>(By.Name(name), 0)
            .Where(element => string.Equals(element.Name, name, StringComparison.Ordinal) &&
                (controlType is null || string.Equals(element.ControlType, controlType, StringComparison.OrdinalIgnoreCase)))
            .ToList();

    /// <summary>Waits for one element named exactly <paramref name="name"/>, and returns it.</summary>
    public static T WaitForExact<T>(this Session session, string name, int timeoutMS, string? controlType = null)
        where T : Element, new()
    {
        T? match = null;
        session.WaitFor(
            () =>
            {
                var matches = session.FindExact<T>(name, controlType);
                match = matches.Count > 0 ? matches[0] : null;
                return match is not null;
            },
            timeoutMS,
            pollIntervalMS: 250);
        Assert.IsNotNull(match, $"No {controlType ?? typeof(T).Name} named exactly '{name}' appeared within {timeoutMS} ms.");
        return match;
    }

    /// <summary>Whether an element named exactly <paramref name="name"/> appears within <paramref name="timeoutMS"/>.</summary>
    public static bool WaitForExactName(this Session session, string name, int timeoutMS) =>
        session.WaitFor(() => session.FindExact<Element>(name).Count > 0, timeoutMS, pollIntervalMS: 250);

    /// <summary>Whether no element named exactly <paramref name="name"/> remains within <paramref name="timeoutMS"/>.</summary>
    public static bool WaitForExactNameGone(this Session session, string name, int timeoutMS) =>
        session.WaitFor(() => session.FindExact<Element>(name).Count == 0, timeoutMS, pollIntervalMS: 250);

    /// <summary>The button with <paramref name="automationId"/> whose name is exactly <paramref name="name"/>.</summary>
    public static T? FindById<T>(this Session session, string automationId, string name)
        where T : Element, new() =>
        session.FindAll<T>(By.AccessibilityId(automationId), 0)
            .FirstOrDefault(element => string.Equals(element.Name, name, StringComparison.Ordinal));

    /// <summary>Waits for the element with <paramref name="automationId"/> and name <paramref name="name"/>.</summary>
    public static T WaitForId<T>(this Session session, string automationId, string name, int timeoutMS)
        where T : Element, new()
    {
        T? match = null;
        session.WaitFor(() => (match = session.FindById<T>(automationId, name)) is not null, timeoutMS, pollIntervalMS: 250);
        Assert.IsNotNull(match, $"No {typeof(T).Name} '{automationId}' named '{name}' appeared within {timeoutMS} ms.");
        return match;
    }
}
