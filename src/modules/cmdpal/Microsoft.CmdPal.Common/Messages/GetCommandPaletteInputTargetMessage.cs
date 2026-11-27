// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.Common.Messages;

/// <summary>Requests the input target of the source palette, or the main palette when no source HWND is supplied.</summary>
public sealed class GetCommandPaletteInputTargetMessage(nint sourceHwnd, Guid sourceRouteId)
{
    public nint SourceHwnd { get; } = sourceHwnd;

    public Guid SourceRouteId { get; } = sourceRouteId;

    public CommandPaletteInputTarget? Target { get; set; }
}
