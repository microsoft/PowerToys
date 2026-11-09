// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using ManagedCommon;
using Microsoft.CmdPal.Common.Helpers;
using Microsoft.CmdPal.Ext.Bookmarks.Services;
using Microsoft.CommandPalette.Extensions;
using Microsoft.UI;
using Microsoft.Windows.Storage.Pickers;

namespace Microsoft.CmdPal.Ext.Bookmarks.Commands;

internal sealed partial class TransferBookmarksCommand : InvokableCommand, IRequiresHostHwnd
{
    private static readonly CompositeFormat ImportSuccessFormat = CompositeFormat.Parse(Resources.bookmarks_import_success);
    private static readonly CompositeFormat ExportSuccessFormat = CompositeFormat.Parse(Resources.bookmarks_export_success);

    private readonly BookmarkTransferService _transferService;
    private readonly BookmarkTransferOperation _operation;
    private nint _hostHwnd;
    private InterlockedBoolean _running;

    public TransferBookmarksCommand(BookmarkTransferService transferService, BookmarkTransferOperation operation)
    {
        _transferService = transferService;
        _operation = operation;
        Name = operation == BookmarkTransferOperation.Import ? Resources.bookmarks_import_name : Resources.bookmarks_export_name;
        Icon = operation == BookmarkTransferOperation.Import ? Icons.ImportBookmarksIcon : Icons.ExportBookmarksIcon;
    }

    public void SetHostHwnd(nint hostHwnd) => _hostHwnd = hostHwnd;

    public override ICommandResult Invoke()
    {
        if (!_running.Set())
        {
            return CommandResult.KeepOpen();
        }

        try
        {
            return TransferAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            Logger.LogError(_operation == BookmarkTransferOperation.Import ? "Failed to import bookmarks" : "Failed to export bookmarks", ex);
            return CommandResult.ShowToast(new ToastArgs
            {
                Message = string.Format(CultureInfo.CurrentCulture, _operation == BookmarkTransferOperation.Import ? Resources.bookmarks_import_failed : Resources.bookmarks_export_failed, ex.Message),
                Result = CommandResult.KeepOpen(),
            });
        }
        finally
        {
            _running.Clear();
        }
    }

    private async Task<ICommandResult> TransferAsync()
    {
        var ownerHwnd = _hostHwnd;
        if (ownerHwnd == 0)
        {
            throw new InvalidOperationException("Could not resolve a window to own the picker.");
        }

        var windowId = Win32Interop.GetWindowIdFromWindow(ownerHwnd);
        string resultMessage;
        if (_operation == BookmarkTransferOperation.Import)
        {
            var picker = new FileOpenPicker(windowId)
            {
                CommitButtonText = Name,
                Title = Resources.bookmarks_import_title,
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
                SettingsIdentifier = "Bookmarks.Transfer",
                ViewMode = PickerViewMode.List,
                InitialFileTypeIndex = 0,
            };
            picker.FileTypeChoices.Add(Resources.bookmarks_file_supported, [".json", ".tsv", ".txt"]);
            picker.FileTypeChoices.Add(Resources.bookmarks_file_json, [".json"]);
            picker.FileTypeChoices.Add(Resources.bookmarks_file_tsv, [".tsv", ".txt"]);
            picker.FileTypeChoices.Add(Resources.bookmarks_file_all, ["*"]);

            var file = await picker.PickSingleFileAsync();
            if (file is null)
            {
                return CommandResult.KeepOpen();
            }

            var (imported, skipped) = _transferService.Import(file.Path);
            resultMessage = imported + skipped == 0
                ? Resources.bookmarks_import_empty
                : string.Format(CultureInfo.CurrentCulture, ImportSuccessFormat, imported, skipped);
        }
        else
        {
            var picker = new FileSavePicker(windowId)
            {
                CommitButtonText = Name,
                Title = Resources.bookmarks_export_title,
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
                SettingsIdentifier = "Bookmarks.Transfer",
                SuggestedFileName = "bookmarks-export",
                DefaultFileExtension = ".json",
            };
            picker.FileTypeChoices.Add(Resources.bookmarks_file_json, [".json"]);
            picker.FileTypeChoices.Add(Resources.bookmarks_file_tsv, [".tsv", ".txt"]);

            var file = await picker.PickSaveFileAsync();
            if (file is null)
            {
                return CommandResult.KeepOpen();
            }

            var count = _transferService.Export(file.Path);
            resultMessage = string.Format(CultureInfo.CurrentCulture, ExportSuccessFormat, count);
        }

        return CommandResult.ShowToast(new ToastArgs
        {
            Message = resultMessage,
            Result = CommandResult.KeepOpen(),
        });
    }
}
