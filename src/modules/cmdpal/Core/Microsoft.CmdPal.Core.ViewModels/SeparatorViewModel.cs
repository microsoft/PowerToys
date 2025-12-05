// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.CodeAnalysis;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Core.ViewModels;

[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
public partial class SeparatorViewModel() :
    CommandItem,
    IContextItemViewModel,
    IFilterItemViewModel,
    ISeparatorContextItem,
    ISeparatorFilterItem
{
}
