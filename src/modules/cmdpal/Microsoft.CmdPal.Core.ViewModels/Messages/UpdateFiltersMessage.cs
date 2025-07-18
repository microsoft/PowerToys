// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;

namespace Microsoft.CmdPal.Core.ViewModels.Messages;

/// <summary>
/// Used to announce a pages filter has been updated.
/// </summary>
public record UpdateFiltersMessage(IFilterItem[] Filters, string[] CurrentFilterIds, bool IsMultiSelect)
{
}
