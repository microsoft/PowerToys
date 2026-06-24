// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Tracing;
using Microsoft.PowerToys.Telemetry;
using Microsoft.PowerToys.Telemetry.Events;

namespace Microsoft.CmdPal.UI.Events;

/// <summary>
/// Tracks Command Palette session duration from launch to close.
/// Purpose: Understand user engagement patterns - quick actions vs. browsing behavior.
/// </summary>
[EventData]
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]
public class CmdPalSessionDuration : EventBase, IEvent
{
    /// <summary>
    /// Gets or sets the session duration in milliseconds.
    /// </summary>
    public ulong DurationMs { get; set; }

    /// <summary>
    /// Gets or sets the number of commands executed during the session.
    /// </summary>
    public int CommandsExecuted { get; set; }

    /// <summary>
    /// Gets or sets the number of pages visited during the session.
    /// </summary>
    public int PagesVisited { get; set; }

    /// <summary>
    /// Gets or sets the reason for dismissal (Escape, LostFocus, Command, etc.).
    /// </summary>
    public string DismissalReason { get; set; }

    /// <summary>
    /// Gets or sets the number of search queries executed during the session.
    /// </summary>
    public int SearchQueriesCount { get; set; }

    /// <summary>
    /// Gets or sets the maximum navigation depth reached during the session.
    /// </summary>
    public int MaxNavigationDepth { get; set; }

    /// <summary>
    /// Gets or sets the number of errors encountered during the session.
    /// </summary>
    public int ErrorCount { get; set; }

    public CmdPalSessionDuration(ulong durationMs, int commandsExecuted, int pagesVisited, string dismissalReason, int searchQueriesCount, int maxNavigationDepth, int errorCount)
    {
        EventName = "CmdPal_SessionDuration";
        DurationMs = durationMs;
        CommandsExecuted = commandsExecuted;
        PagesVisited = pagesVisited;
        DismissalReason = dismissalReason;
        SearchQueriesCount = searchQueriesCount;
        MaxNavigationDepth = maxNavigationDepth;
        ErrorCount = errorCount;
    }

    public PartA_PrivTags PartA_PrivTags => PartA_PrivTags.ProductAndServiceUsage;
}
