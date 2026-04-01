// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.ViewModels.Settings;

namespace Microsoft.CmdPal.UI.ViewModels.Messages;

public record ShowPinToDockDialogMessage(
    string ProviderId,
    string CommandId,
    string Title,
    string Subtitle,
    IconInfoViewModel? Icon,
    DockSide DockSide);
