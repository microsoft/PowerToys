// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.ViewModels.Messages;

/// <summary>
/// Reveals and focuses the dock, or cycles through items in a focused dock in either direction.
/// </summary>
public record FocusDockMessage(bool Reverse = false);
