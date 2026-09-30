// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.ViewModels.Messages;

/// <summary>
/// Sent when the dock focus hotkey fires. The dock decides whether that means
/// "reveal and focus me" or "give focus back", since only it knows if it already has it.
/// </summary>
public record FocusDockMessage;
