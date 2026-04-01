// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// <summary>
//     Package format/conversion.
// </summary>
// <history>
//     2008 created by Truong Do (ductdo).
//     2009-... modified by Truong Do (TruongDo).
//     2023- Included in PowerToys.
// </history>
namespace MouseWithoutBorders.Core;

internal struct PackageMonitor
{
    internal ulong Keyboard;
    internal ulong Mouse;
    internal ulong Heartbeat;
    internal ulong ByeBye;
    internal ulong Hello;
    internal ulong Matrix;
    internal ulong ClipboardText;
    internal ulong ClipboardImage;
    internal ulong Clipboard;
    internal ulong ClipboardDragDrop;
    internal ulong ClipboardDragDropEnd;
    internal ulong ClipboardAsk;
    internal ulong ExplorerDragDrop;
    internal ulong Nil;

    internal PackageMonitor(ulong value)
    {
        ClipboardDragDrop = ClipboardDragDropEnd = ExplorerDragDrop =
            Keyboard = Mouse = Heartbeat = ByeBye = Hello = Clipboard =
            Matrix = ClipboardImage = ClipboardText = Nil = ClipboardAsk = value;
    }
}
