// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.CmdPal.UI.ViewModels.Messages;

/// <summary>
/// Sent when a dock band is dropped onto a different monitor's dock.
/// The source DockControl should remove the band from its ViewModel.
/// </summary>
public sealed class CrossMonitorBandDropMessage
{
    public string BandId { get; }

    public string SourceMonitorDeviceId { get; }

    public CrossMonitorBandDropMessage(string bandId, string sourceMonitorDeviceId)
    {
        ArgumentNullException.ThrowIfNull(bandId);
        ArgumentNullException.ThrowIfNull(sourceMonitorDeviceId);

        BandId = bandId;
        SourceMonitorDeviceId = sourceMonitorDeviceId;
    }
}
