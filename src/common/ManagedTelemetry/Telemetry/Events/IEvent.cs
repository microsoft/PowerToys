// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.Telemetry.Events
{
    public interface IEvent
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "Part of telem, can't adjust")]
        PartA_PrivTags PartA_PrivTags { get; }
    }
}
