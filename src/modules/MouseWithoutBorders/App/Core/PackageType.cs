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

internal enum PackageType // : int
{
    // Search for PACKAGE_TYPE_RELATED before changing these!
    Invalid = 0xFF,

    Error = 0xFE,

    Hi = 2,
    Hello = 3,
    ByeBye = 4,

    Heartbeat = 20,
    Awake = 21,
    HideMouse = 50,
    Heartbeat_ex = 51,
    Heartbeat_ex_l2 = 52,
    Heartbeat_ex_l3 = 53,

    Clipboard = 69,
    ClipboardDragDrop = 70,
    ClipboardDragDropEnd = 71,
    ExplorerDragDrop = 72,
    ClipboardCapture = 73,
    CaptureScreenCommand = 74,
    ClipboardDragDropOperation = 75,
    ClipboardDataEnd = 76,
    MachineSwitched = 77,
    ClipboardAsk = 78,
    ClipboardPush = 79,

    NextMachine = 121,
    Keyboard = 122,
    Mouse = 123,
    ClipboardText = 124,
    ClipboardImage = 125,

    Handshake = 126,
    HandshakeAck = 127,

    Matrix = 128,
    MatrixSwapFlag = 2,
    MatrixTwoRowFlag = 4,
}
