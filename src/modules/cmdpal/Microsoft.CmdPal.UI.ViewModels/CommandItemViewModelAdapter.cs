// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Mvvm.ComponentModel;

namespace Microsoft.CmdPal.UI.ViewModels;

/// <summary>
/// A host-side SDK adapter that exposes a cached command without querying an extension interface.
/// </summary>
public abstract partial class CommandItemViewModelAdapter : ObservableObject
{
    public abstract CommandViewModel? CommandViewModel { get; }
}
