// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Ext.Bookmarks.Commands;
using Microsoft.CmdPal.Ext.Bookmarks.Services;

namespace Microsoft.CmdPal.Ext.Bookmarks.Pages;

internal sealed partial class TransferBookmarksPage : ListPage
{
    public TransferBookmarksPage(BookmarkTransferService transferService, BookmarkTransferOperation operation)
    {
        Id = operation == BookmarkTransferOperation.Import ? "Bookmarks.Import" : "Bookmarks.Export";
        Name = Title = operation == BookmarkTransferOperation.Import ? Resources.bookmarks_import_title : Resources.bookmarks_export_title;
        Icon = operation == BookmarkTransferOperation.Import ? Icons.ImportBookmarksIcon : Icons.ExportBookmarksIcon;
        EmptyContent = new CommandItem(new TransferBookmarksCommand(transferService, operation))
        {
            Title = Title,
            Subtitle = operation == BookmarkTransferOperation.Import ? Resources.bookmarks_import_description : Resources.bookmarks_export_description,
            Icon = Icon,
        };
    }
}
