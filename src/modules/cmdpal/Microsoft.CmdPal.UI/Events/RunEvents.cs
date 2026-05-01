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

    public string Query { get; set; }

    public int ResultCount { get; set; }

    public ulong DurationMs { get; set; }

    public CmdPalRunQuery(string query, int resultCount, ulong durationMs)
    {
        EventName = "CmdPal_RunQuery";
        Query = query;
        ResultCount = resultCount;
        DurationMs = durationMs;
    }
}

[EventData]
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]
public class CmdPalRunCommand : EventBase, IEvent
{
    public PartA_PrivTags PartA_PrivTags => PartA_PrivTags.ProductAndServiceUsage;

    public string Command { get; set; }

    public bool AsAdmin { get; set; }

    public bool Success { get; set; }

    public CmdPalRunCommand(string command, bool asAdmin, bool success)
    {
        EventName = "CmdPal_RunCommand";
        Command = command;
        AsAdmin = asAdmin;
        Success = success;
    }
}

[EventData]
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]
public class CmdPalOpenUri : EventBase, IEvent
{
    public PartA_PrivTags PartA_PrivTags => PartA_PrivTags.ProductAndServiceUsage;

    public string Uri { get; set; }

    public bool IsWeb { get; set; }

    public bool Success { get; set; }

    public CmdPalOpenUri(string uri, bool isWeb, bool success)
    {
        EventName = "CmdPal_OpenUri";
        Uri = uri;
        IsWeb = isWeb;
        Success = success;
    }
}

[EventData]
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]
public class CmdPalRunBuildListPathResolution : EventBase, IEvent
{
    public PartA_PrivTags PartA_PrivTags => PartA_PrivTags.ProductAndServiceUsage;

    public string NewSearch { get; set; }

    public string CorrectedSearchText { get; set; }

    public string Expanded { get; set; }

    public bool WithLeadingTilde { get; set; }

    public bool CouldResolvePath { get; set; }

    public bool IsFile { get; set; }

    public long DurationMs { get; set; }

    public int Result { get; set; }

    public CmdPalRunBuildListPathResolution(
        string newSearch,
        string correctedSearchText,
        string expanded,
        bool withLeadingTilde,
        bool couldResolvePath,
        bool isFile,
        long durationMs,
        int result)
    {
        EventName = "CmdPal_Run_BuildListPathResolution";
        NewSearch = newSearch;
        CorrectedSearchText = correctedSearchText;
        Expanded = expanded;
        WithLeadingTilde = withLeadingTilde;
        CouldResolvePath = couldResolvePath;
        IsFile = isFile;
        DurationMs = durationMs;
        Result = result;
    }
}

[EventData]
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]
public class CmdPalRunCreatePathItemsResolvedPath : EventBase, IEvent
{
    public PartA_PrivTags PartA_PrivTags => PartA_PrivTags.ProductAndServiceUsage;

    public string FullFilePath { get; set; }

    public string SearchText { get; set; }

    public string DirectoryPath { get; set; }

    public CmdPalRunCreatePathItemsResolvedPath(string fullFilePath, string searchText, string directoryPath)
    {
        EventName = "CmdPal_Run_CreatePathItemsResolvedPath";
        FullFilePath = fullFilePath;
        SearchText = searchText;
        DirectoryPath = directoryPath;
    }
}

[EventData]
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]
public class CmdPalRunCreatePathItemsFiltered : EventBase, IEvent
{
    public PartA_PrivTags PartA_PrivTags => PartA_PrivTags.ProductAndServiceUsage;

    public string Dir { get; set; }

    public string FuzzyString { get; set; }

    public int FilteredCount { get; set; }

    public CmdPalRunCreatePathItemsFiltered(string dir, string fuzzyString, int filteredCount)
    {
        EventName = "CmdPal_Run_CreatePathItemsFiltered";
        Dir = dir;
        FuzzyString = fuzzyString;
        FilteredCount = filteredCount;
    }
}

[EventData]
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]
public class CmdPalRunCreatePathItemsChangedDirectory : EventBase, IEvent
{
    public PartA_PrivTags PartA_PrivTags => PartA_PrivTags.ProductAndServiceUsage;

    public string OldDir { get; set; }

    public string NewDir { get; set; }

    public CmdPalRunCreatePathItemsChangedDirectory(string oldDir, string newDir)
    {
        EventName = "CmdPal_Run_CreatePathItemsChangedDirectory";
        OldDir = oldDir;
        NewDir = newDir;
    }
}

[EventData]
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]
public class CmdPalRunBuildItemsForDirectory : EventBase, IEvent
{
    public PartA_PrivTags PartA_PrivTags => PartA_PrivTags.ProductAndServiceUsage;

    public string Dir { get; set; }

    public int FileCount { get; set; }

    public CmdPalRunBuildItemsForDirectory(string dir, int fileCount)
    {
        EventName = "CmdPal_Run_BuildItemsForDirectory";
        Dir = dir;
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

    public string Type { get; set; }

    public bool TimedOut { get; set; }

    public long TotalMs { get; set; }

    public long ParseMs { get; set; }

    public bool IsUri { get; set; }

    public string Target { get; set; }

    public string Args { get; set; }

    public int ParseResult { get; set; }

    public CmdPalRunLoadHistoryItem(
        string type,
        bool timedOut,
        long totalMs,
        long parseMs,
        bool isUri,
        string target,
        string args,
        int parseResult)
    {
        EventName = "CmdPal_Run_LoadHistoryItem";
        Type = type;
        TimedOut = timedOut;
        TotalMs = totalMs;
        ParseMs = parseMs;
        IsUri = isUri;
        Target = target;
        Args = args;
        ParseResult = parseResult;
    }
}

#pragma warning restore SA1649 // File name should match first type name
#pragma warning restore SA1402 // File may only contain a single type
