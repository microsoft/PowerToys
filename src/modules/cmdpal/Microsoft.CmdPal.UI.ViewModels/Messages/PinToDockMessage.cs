// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.ViewModels.Dock;

namespace Microsoft.CmdPal.UI.ViewModels.Messages;

public record PinToDockMessage(
    string ProviderId,
    string CommandId,
    bool Pin,
    DockPinSide Side = DockPinSide.Start,
    bool? ShowTitles = null,
    bool? ShowSubtitles = null);
