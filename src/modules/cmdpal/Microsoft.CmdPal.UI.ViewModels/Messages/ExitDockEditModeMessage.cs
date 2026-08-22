// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.ViewModels.Messages;

/// <summary>
/// Broadcast when the user exits dock edit mode on any monitor.
/// All DockControls should respond by saving or discarding their changes.
/// </summary>
/// <param name="Discard">True to discard changes; false to save them.</param>
public record ExitDockEditModeMessage(bool Discard);
