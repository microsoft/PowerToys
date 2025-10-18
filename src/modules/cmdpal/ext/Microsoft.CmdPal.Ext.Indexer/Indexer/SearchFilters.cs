// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Ext.Indexer.Properties;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Indexer.Indexer;

internal sealed partial class SearchFilters : Filters
{
    public SearchFilters()
    {
        CurrentFilterId = "all";
    }

    public override IFilterItem[] GetFilters()
    {
        return [
            new Filter() { Id = "all", Name = Resources.Indexer_Filter_All, Icon = Icons.FilterIcon },
            new Separator(),
            new Filter() { Id = "folders", Name = Resources.Indexer_Filter_Folders_Only, Icon = Icons.FolderOpenIcon },
            new Filter() { Id = "files", Name = Resources.Indexer_Filter_Files_Only, Icon = Icons.FilesIcon },
        ];
    }
}
