// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.ViewModels.Dock;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.CmdPal.UI.ViewModels.Settings;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;

namespace Microsoft.CmdPal.UI.Helpers;

internal readonly record struct DockDropTarget(DockPinSide Side, string? MonitorDeviceId)
{
    internal static DockDropTarget? Resolve(
        DataPackageView data,
        bool isEditMode,
        DockSide dockSide,
        Point position,
        Size size,
        string? monitorDeviceId,
        int monitorCount)
    {
        if (isEditMode
            || data.Properties.ContainsKey("DockBandId")
            || (!data.Contains(StandardDataFormats.StorageItems)
                && !data.Contains(StandardDataFormats.ApplicationLink)
                && !data.Contains(StandardDataFormats.WebLink)
                && !data.Contains(StandardDataFormats.Uri)))
        {
            return null;
        }

        if (!(position.X >= 0 && position.X < size.Width && position.Y >= 0 && position.Y < size.Height))
        {
            return null;
        }

        var isVertical = dockSide is DockSide.Left or DockSide.Right;
        var offset = isVertical ? position.Y : position.X;
        var extent = isVertical ? size.Height : size.Width;
        var side = offset < extent / 3 ? DockPinSide.Start :
            offset < extent * 2 / 3 ? DockPinSide.Center : DockPinSide.End;

        return new(side, monitorCount > 1 ? monitorDeviceId : null);
    }

    internal static async Task<Uri?> ReadLinkAsync(DataPackageView data)
    {
        if (data.Contains(StandardDataFormats.ApplicationLink))
        {
            return await data.GetApplicationLinkAsync();
        }

        if (data.Contains(StandardDataFormats.WebLink))
        {
            return await data.GetWebLinkAsync();
        }

        if (data.Contains(StandardDataFormats.Uri))
        {
            return await data.GetUriAsync();
        }

        return null;
    }

    internal PinToDockMessage CreatePinMessage(string providerId, string commandId)
    {
        return new PinToDockMessage(providerId, commandId, true, WithReload: false, Side: Side, MonitorDeviceId: MonitorDeviceId);
    }
}
