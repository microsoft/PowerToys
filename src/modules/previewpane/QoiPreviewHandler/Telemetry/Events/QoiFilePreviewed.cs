// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.Tracing;
using Microsoft.PowerToys.Telemetry;
using Microsoft.PowerToys.Telemetry.Events;

namespace Microsoft.PowerToys.PreviewHandler.Qoi.Telemetry.Events
{
    /// <summary>
    /// A telemetry event to be raised when a Qoi file has been viewed in the preview pane.
    /// </summary>
    [EventData]
    public class QoiFilePreviewed : EventBase, IEvent
    {
        /// <inheritdoc/>
        public PartA_PrivTags PartA_PrivTags => PartA_PrivTags.ProductAndServiceUsage;
    }
}
