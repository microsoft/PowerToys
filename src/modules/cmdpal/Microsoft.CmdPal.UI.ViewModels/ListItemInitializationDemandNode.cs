// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.ViewModels;

/// <summary>
/// A link owned by one item or coordinator. Demand can belong to multiple lists
/// during replacement. Registrations must not retain those lists after completion.
/// </summary>
internal sealed class ListItemInitializationDemandNode(ListItemInitializationDemand demand)
{
    internal ListItemInitializationDemand Demand { get; } = demand;

    internal ListItemInitializationDemandNode? Next { get; set; }
}
