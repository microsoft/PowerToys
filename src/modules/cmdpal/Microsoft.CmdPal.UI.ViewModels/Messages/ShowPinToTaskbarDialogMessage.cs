// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.ViewModels.Messages;

public record ShowPinToTaskbarDialogMessage(
    string ProviderId,
    string CommandId,
    string Title,
    string Subtitle,
    IconInfoViewModel? Icon);
