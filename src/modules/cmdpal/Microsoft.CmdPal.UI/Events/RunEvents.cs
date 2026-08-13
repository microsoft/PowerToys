// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Tracing;
using Microsoft.PowerToys.Telemetry;
using Microsoft.PowerToys.Telemetry.Events;

namespace Microsoft.CmdPal.UI.Events;

// Just put all the run events in one file for simplicity.
#pragma warning disable SA1402 // File may only contain a single type
#pragma warning disable SA1649 // File name should match first type name

[EventData]
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]
public class CmdPalRunQuery : EventBase, IEvent
{
    public PartA_PrivTags PartA_PrivTags => PartA_PrivTags.ProductAndServiceUsage;

    public int ResultCount { get; set; }

    public ulong DurationMs { get; set; }

    public CmdPalRunQuery(int resultCount, ulong durationMs)
    {
        EventName = "CmdPal_RunQuery";
        ResultCount = resultCount;
        DurationMs = durationMs;
    }
}

[EventData]
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]
public class CmdPalRunCommand : EventBase, IEvent
{
    public PartA_PrivTags PartA_PrivTags => PartA_PrivTags.ProductAndServiceUsage;

    public bool AsAdmin { get; set; }

    public bool Success { get; set; }

    public CmdPalRunCommand(bool asAdmin, bool success)
    {
        EventName = "CmdPal_RunCommand";
        AsAdmin = asAdmin;
        Success = success;
    }
}

[EventData]
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]
public class CmdPalOpenUri : EventBase, IEvent
{
    public PartA_PrivTags PartA_PrivTags => PartA_PrivTags.ProductAndServiceUsage;

    public bool IsWeb { get; set; }

    public bool Success { get; set; }

    public CmdPalOpenUri(bool isWeb, bool success)
    {
        EventName = "CmdPal_OpenUri";
        IsWeb = isWeb;
        Success = success;
    }
}

[EventData]
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]
public class CmdPalRunBuildListPathResolution : EventBase, IEvent
{
    public PartA_PrivTags PartA_PrivTags => PartA_PrivTags.ProductAndServiceUsage;

    public bool WithLeadingTilde { get; set; }

    public bool CouldResolvePath { get; set; }

    public bool IsFile { get; set; }

    public long DurationMs { get; set; }

    public int Result { get; set; }

    public CmdPalRunBuildListPathResolution(
        bool withLeadingTilde,
        bool couldResolvePath,
        bool isFile,
        long durationMs,
        int result)
    {
        EventName = "CmdPal_Run_BuildListPathResolution";
        WithLeadingTilde = withLeadingTilde;
        CouldResolvePath = couldResolvePath;
        IsFile = isFile;
        DurationMs = durationMs;
        Result = result;
    }
}

[EventData]
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]
public class CmdPalRunCreatePathItemsFiltered : EventBase, IEvent
{
    public PartA_PrivTags PartA_PrivTags => PartA_PrivTags.ProductAndServiceUsage;

    public int FilteredCount { get; set; }

    public CmdPalRunCreatePathItemsFiltered(int filteredCount)
    {
        EventName = "CmdPal_Run_CreatePathItemsFiltered";
        FilteredCount = filteredCount;
    }
}

[EventData]
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]
public class CmdPalRunBuildItemsForDirectory : EventBase, IEvent
{
    public PartA_PrivTags PartA_PrivTags => PartA_PrivTags.ProductAndServiceUsage;

    public int FileCount { get; set; }

    public CmdPalRunBuildItemsForDirectory(int fileCount)
    {
        EventName = "CmdPal_Run_BuildItemsForDirectory";
        FileCount = fileCount;
    }
}

[EventData]
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]
public class CmdPalRunLoadHistory : EventBase, IEvent
{
    public PartA_PrivTags PartA_PrivTags => PartA_PrivTags.ProductAndServiceUsage;

    public int ItemsToLoad { get; set; }

    public int ItemsLoaded { get; set; }

    public long DurationMs { get; set; }

    public CmdPalRunLoadHistory(int itemsToLoad, int itemsLoaded, long durationMs)
    {
        EventName = "CmdPal_Run_LoadHistory";
        ItemsToLoad = itemsToLoad;
        ItemsLoaded = itemsLoaded;
        DurationMs = durationMs;
    }
}

[EventData]
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]
public class CmdPalRunLoadHistoryItem : EventBase, IEvent
{
    public PartA_PrivTags PartA_PrivTags => PartA_PrivTags.ProductAndServiceUsage;

    public bool TimedOut { get; set; }

    public long TotalMs { get; set; }

    public long ParseMs { get; set; }

    public bool IsUri { get; set; }

    public int ParseResult { get; set; }

    public CmdPalRunLoadHistoryItem(
        bool timedOut,
        long totalMs,
        long parseMs,
        bool isUri,
        int parseResult)
    {
        EventName = "CmdPal_Run_LoadHistoryItem";
        TimedOut = timedOut;
        TotalMs = totalMs;
        ParseMs = parseMs;
        IsUri = isUri;
        ParseResult = parseResult;
    }
}

#pragma warning restore SA1649 // File name should match first type name
#pragma warning restore SA1402 // File may only contain a single type
