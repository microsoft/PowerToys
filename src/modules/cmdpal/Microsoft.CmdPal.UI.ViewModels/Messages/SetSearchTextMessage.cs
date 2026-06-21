// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.ViewModels.Messages;

/// <summary>
/// Sent after an alias triggers navigation to forward any text the user
/// typed beyond the alias prefix to the destination page (#41736).
/// </summary>
public record SetSearchTextMessage(string Text);
