// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;

namespace Microsoft.CmdPal.Core.ViewModels.Messages;

/// <summary>
/// Used to announce that the current filter Id has been updated.
/// </summary>
public record UpdateCurrentFilterIdMessage(string CurrentFilterId)
{
}
