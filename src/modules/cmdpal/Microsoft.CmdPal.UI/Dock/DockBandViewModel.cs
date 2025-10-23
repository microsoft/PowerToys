// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using Microsoft.CmdPal.Core.ViewModels;

namespace Microsoft.CmdPal.UI.Dock;

internal sealed class DockBandViewModel
{
    public ObservableCollection<CommandItemViewModel> Items { get; } = new();
}
#pragma warning restore SA1402 // File may only contain a single type
