// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.CommandPalette.Extensions;

namespace Microsoft.CmdPal.Core.ViewModels;

// DANGER: Make sure that this is derived from a WinRT class. If not, it'll fail with AOT at runtime, in any bindings.
public partial class SeparatorContextItemViewModel() : ObservableObject, IContextItemViewModel, ISeparatorContextItem
{
}
