// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Tracing;
using Microsoft.PowerToys.Telemetry;
using Microsoft.PowerToys.Telemetry.Events;

namespace Microsoft.CommandPalette.UI.Models.Events;

[EventData]
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]
public class OpenUriEvent : EventBase, IEvent
{
    public PartA_PrivTags PartA_PrivTags => PartA_PrivTags.ProductAndServiceUsage;

    public string Uri { get; set; }

    public bool IsWeb { get; set; }

    public bool Success { get; set; }

    public OpenUriEvent(string uri, bool isWeb, bool success)
    {
        EventName = "CmdPal_OpenUri";
        Uri = uri;
        IsWeb = isWeb;
        Success = success;
    }
}
