// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ManagedCommon;
using Microsoft.CmdPal.Common.Commands;
using Microsoft.CmdPal.Core.Common.Commands;
using Microsoft.CmdPal.Core.Common.Helpers;
using Microsoft.CmdPal.Ext.Bookmarks.Commands;
using Microsoft.CmdPal.Ext.Bookmarks.Helpers;
using Microsoft.CmdPal.Ext.Bookmarks.Persistence;
using Microsoft.CmdPal.Ext.Bookmarks.Services;
using Microsoft.CmdPal.Ext.Indexer;
using Microsoft.CommandPalette.Extensions;
using Windows.Foundation;

namespace Microsoft.CmdPal.Ext.Bookmarks.Pages;

internal sealed partial class BookmarkListItem : ListItem, IDisposable
{
    private readonly IBookmarksManager _bookmarksManager;
    private readonly IBookmarkResolver _commandResolver;
    private readonly IBookmarkIconLocator _iconLocator;
    private readonly IPlaceholderParser _placeholderParser;
    private readonly SupersedingAsyncValueGate<BookmarkListItemReclassifyResult> _classificationGate;
    private readonly TaskCompletionSource _initializationTcs = new();

    private BookmarkData _bookmark;

    public Task IsInitialized => _initializationTcs.Task;

    public string BookmarkAddress => _bookmark.Bookmark;

    public string BookmarkTitle => _bookmark.Name;

    public Guid BookmarkId => _bookmark.Id;

    public BookmarkListItem(BookmarkData bookmark, IBookmarksManager bookmarksManager, IBookmarkResolver commandResolver, IBookmarkIconLocator iconLocator, IPlaceholderParser placeholderParser)
    {
        ArgumentNullException.ThrowIfNull(bookmark);
        ArgumentNullException.ThrowIfNull(bookmarksManager);
        ArgumentNullException.ThrowIfNull(commandResolver);

        _bookmark = bookmark;
        _bookmarksManager = bookmarksManager;
        _bookmarksManager.BookmarkUpdated += BookmarksManagerOnBookmarkUpdated;
        _commandResolver = commandResolver;
        _iconLocator = iconLocator;
        _placeholderParser = placeholderParser;
        _classificationGate = new SupersedingAsyncValueGate<BookmarkListItemReclassifyResult>(ClassifyAsync, ApplyClassificationResult);
        _ = _classificationGate.ExecuteAsync();
    }

    private void BookmarksManagerOnBookmarkUpdated(BookmarkData original, BookmarkData @new)
    {
        if (original.Id == _bookmark.Id)
        {
            Update(@new);
        }
    }

    public void Dispose()
    {
        _classificationGate.Dispose();
        var existing = Command;
        if (existing != null)
        {
            existing.PropChanged -= CommandPropertyChanged;
        }
    }

    private void Update(BookmarkData data)
    {
        ArgumentNullException.ThrowIfNull(data);

        try
        {
            _bookmark = data;
            OnPropertyChanged(nameof(BookmarkTitle));
            OnPropertyChanged(nameof(BookmarkAddress));

            Subtitle = Resources.bookmarks_item_refreshing;
            _ = _classificationGate.ExecuteAsync();
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to update bookmark", ex);
        }
    }

    private async Task<BookmarkListItemReclassifyResult> ClassifyAsync(CancellationToken ct)
    {
        TypedEventHandler<object, BookmarkData> bookmarkSavedHandler = BookmarkSaved;
        List<IContextItem> contextMenu = [];

        var classification = (await _commandResolver.TryClassifyAsync(_bookmark.Bookmark, ct)).Result;

        var title = BuildTitle(_bookmark, classification);
        var subtitle = BuildSubtitle(_bookmark, classification);

        ICommand command = classification.IsPlaceholder
            ? new BookmarkPlaceholderPage(_bookmark, _iconLocator, _commandResolver, _placeholderParser)
            : new LaunchBookmarkCommand(_bookmark, classification, _iconLocator, _commandResolver);

        BuildSpecificContextMenuItems(classification, contextMenu);
        AddCommonContextMenuItems(_bookmark, _bookmarksManager, bookmarkSavedHandler, contextMenu);

        return new BookmarkListItemReclassifyResult(
            command,
            title,
            subtitle,
            contextMenu.ToArray());
    }

    private void ApplyClassificationResult(BookmarkListItemReclassifyResult classificationResult)
    {
        var existing = Command;
        if (existing != null)
        {
            existing.PropChanged -= CommandPropertyChanged;
        }

        classificationResult.Command.PropChanged += CommandPropertyChanged;
        Command = classificationResult.Command;
        OnPropertyChanged(nameof(Icon));
        Title = classificationResult.Title;
        Subtitle = classificationResult.Subtitle;
        MoreCommands = classificationResult.MoreCommands;

        _initializationTcs.TrySetResult();
    }

    private void CommandPropertyChanged(object sender, IPropChangedEventArgs args) =>
        OnPropertyChanged(args.PropertyName);

    private static void BuildSpecificContextMenuItems(Classification classification, List<IContextItem> contextMenu)
    {
        // TODO: unify across all built-in extensions
        var bookmarkTargetType = classification.Kind;

        // TODO: add "Run as administrator" for executables/shortcuts
        if (!classification.IsPlaceholder)
        {
            if (bookmarkTargetType == CommandKind.FileDocument && File.Exists(classification.Target))
            {
                contextMenu.Add(new CommandContextItem(new OpenWithCommand(classification.Input)));
            }
        }

        string? directoryPath = null;
        var targetPath = classification.Target;
        switch (bookmarkTargetType)
        {
            case CommandKind.Directory:
                directoryPath = targetPath;
                contextMenu.Add(new CommandContextItem(new DirectoryPage(directoryPath))); // Browse
                break;
            case CommandKind.FileExecutable:
            case CommandKind.FileDocument:
            case CommandKind.Shortcut:
            case CommandKind.InternetShortcut:
                try
                {
                    directoryPath = Path.GetDirectoryName(targetPath);
                }
                catch
                {
                    // ignore any path parsing errors
                }

                break;
            case CommandKind.WebUrl:
            case CommandKind.Protocol:
            case CommandKind.Aumid:
            case CommandKind.PathCommand:
            case CommandKind.Unknown:
            default:
                break;
        }

        // Add "Copy Path" or "Copy Address" command
        if (!string.IsNullOrWhiteSpace(classification.Input))
        {
            var copyCommand = new CopyPathCommand(targetPath)
            {
                Name = bookmarkTargetType is CommandKind.WebUrl or CommandKind.Protocol
                    ? Resources.bookmarks_copy_address_name
                    : Resources.bookmarks_copy_path_name,
                Icon = Icons.CopyPath,
            };

            contextMenu.Add(new CommandContextItem(copyCommand) { RequestedShortcut = KeyChords.CopyPath });
        }

        // Add "Open in Console" and "Show in Folder" commands if we have a valid directory path
        if (!string.IsNullOrWhiteSpace(directoryPath) && Directory.Exists(directoryPath))
        {
            contextMenu.Add(new CommandContextItem(new ShowFileInFolderCommand(targetPath)) { RequestedShortcut = KeyChords.OpenFileLocation });
            contextMenu.Add(new CommandContextItem(OpenInConsoleCommand.FromDirectory(directoryPath)) { RequestedShortcut = KeyChords.OpenInConsole });
        }

        if (!string.IsNullOrWhiteSpace(targetPath) && (File.Exists(targetPath) || Directory.Exists(targetPath)))
        {
            contextMenu.Add(new CommandContextItem(new OpenPropertiesCommand(targetPath)));
        }
    }

    private static string BuildSubtitle(BookmarkData bookmark, Classification classification)
    {
        var subtitle = BuildSubtitleCore(bookmark, classification);
#if DEBUG
        subtitle = $" ({classification.Kind}) • " + subtitle;
#endif
        return subtitle;
    }

    private static string BuildSubtitleCore(BookmarkData bookmark, Classification classification)
    {
        if (classification.Kind == CommandKind.Unknown)
        {
            return bookmark.Bookmark;
        }

        if (classification.Kind is CommandKind.VirtualShellItem &&
            ShellNames.TryGetFriendlyName(classification.Target, out var friendlyName))
        {
            return friendlyName;
        }

        if (ShellNames.TryGetFileSystemPath(bookmark.Bookmark, out var displayName) &&
            !string.IsNullOrWhiteSpace(displayName))
        {
            return displayName;
        }

        return bookmark.Bookmark;
    }

    private static string BuildTitle(BookmarkData bookmark, Classification classification)
    {
        if (!string.IsNullOrWhiteSpace(bookmark.Name))
        {
            return bookmark.Name;
        }

        if (classification.Kind is CommandKind.Unknown or CommandKind.WebUrl or CommandKind.Protocol)
        {
            return bookmark.Bookmark;
        }

        if (ShellNames.TryGetFriendlyName(classification.Target, out var friendlyName))
        {
            return friendlyName;
        }

        if (ShellNames.TryGetFileSystemPath(bookmark.Bookmark, out var displayName) &&
            !string.IsNullOrWhiteSpace(displayName))
        {
            return displayName;
        }

        return bookmark.Bookmark;
    }

    private static void AddCommonContextMenuItems(
        BookmarkData bookmark,
        IBookmarksManager bookmarksManager,
        TypedEventHandler<object, BookmarkData> bookmarkSavedHandler,
        List<IContextItem> contextMenu)
    {
        contextMenu.Add(new Separator());

        var edit = new AddBookmarkPage(bookmark) { Icon = Icons.EditIcon };
        edit.AddedCommand += bookmarkSavedHandler;
        contextMenu.Add(new CommandContextItem(edit));

        var confirmableCommand = new ConfirmableCommand
        {
            Command = new DeleteBookmarkCommand(bookmark, bookmarksManager),
            ConfirmationTitle = Resources.bookmarks_delete_prompt_title!,
            ConfirmationMessage = Resources.bookmarks_delete_prompt_message!,
            Name = Resources.bookmarks_delete_name,
            Icon = Icons.DeleteIcon,
        };
        var delete = new CommandContextItem(confirmableCommand) { IsCritical = true, RequestedShortcut = KeyChords.DeleteBookmark };
        contextMenu.Add(delete);
    }

    private void BookmarkSaved(object sender, BookmarkData args)
    {
        ExtensionHost.LogMessage($"Saving bookmark ({args.Name},{args.Bookmark})");
        _bookmarksManager.Update(args.Id, args.Name, args.Bookmark);
    }

    private readonly record struct BookmarkListItemReclassifyResult(
        ICommand Command,
        string Title,
        string Subtitle,
        IContextItem[] MoreCommands
    );
}
