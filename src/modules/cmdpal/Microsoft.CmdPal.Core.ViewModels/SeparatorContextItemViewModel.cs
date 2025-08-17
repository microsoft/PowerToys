// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.CodeAnalysis;
using Microsoft.CmdPal.Core.ViewModels;
using Microsoft.CommandPalette.Extensions;

namespace Microsoft.CmdPal.Core.ViewModels;

[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
public partial class SeparatorContextItemViewModel() : IContextItemViewModel, ISeparatorContextItem
{
}
