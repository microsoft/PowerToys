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

#pragma warning restore SA1649 // File name should match first type name
#pragma warning restore SA1402 // File may only contain a single type
