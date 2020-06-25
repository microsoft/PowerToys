// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.Tracing;
using Microsoft.PowerToys.Telemetry;
using Microsoft.PowerToys.Telemetry.Events;

namespace SvgPreviewHandler.Telemetry.Events
{
    /// <summary>
    /// A telemetry event to be raised when a svg file has been viewed in the preview pane.
    /// </summary>
    [EventData]
    public class SvgFileHandlerLoaded : EventBase, IEvent
    {
        /// <summary>
        /// Gets The version string. TODO: This should be replaced by a P/Invoke call to get_product_version.
        /// </summary>
        public string Version => "v0.18.3";

        /// <inheritdoc/>
        public PartA_PrivTags PartA_PrivTags => PartA_PrivTags.ProductAndServiceUsage;
    }
}
