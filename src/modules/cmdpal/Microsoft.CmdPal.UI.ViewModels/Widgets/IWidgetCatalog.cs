// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;

namespace Microsoft.CmdPal.UI.ViewModels.Widgets;

public interface IWidgetCatalog
{
    IReadOnlyList<WidgetCatalogEntry> Entries { get; }

    Task RefreshAsync(CancellationToken cancellationToken);

    WidgetCatalogEntry? Find(WidgetBinding binding);

    Task<IWidgetContent?> CreateAsync(WidgetBinding binding, string instanceId, CancellationToken cancellationToken);
}
