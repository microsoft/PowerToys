// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.Tracing;
using Microsoft.PowerToys.Telemetry;
using Microsoft.PowerToys.Telemetry.Events;
using Peek.Common.Models;

namespace Peek.UI.Telemetry.Events
{
    [EventData]
    public class ErrorEvent : EventBase, IEvent
    {
        public class FailureType
        {
            public static readonly string PreviewFail = "Preview fail, cannot render file";
            public static readonly string FileNotSupported = "Default view shown due to file not supported";
            public static readonly string AppCrash = "App crash";
        }

        public ErrorEvent()
        {
            EventName = "Peek_Error";
        }

        public HResult HResult { get; set; }

        public string Message { get; set; } = string.Empty;

        public string Failure { get; set; } = string.Empty;

        public PartA_PrivTags PartA_PrivTags => PartA_PrivTags.ProductAndServiceUsage;
    }
}
