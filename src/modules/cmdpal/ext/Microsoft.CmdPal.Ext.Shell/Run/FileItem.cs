// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Ext.Shell;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.Storage.Streams;

namespace Microsoft.CmdPal.Ext.Run;

/// <summary>
/// Helper class for both directory paths and executables.
/// Sets this list item up so that its icon will come from the file path, but
/// it will be initialized async. Not only that, but the actual icon loading
/// happens only after the icon was requested.
/// </summary>
internal abstract partial class FileItem : ListItem, IFileItem
{
    // Use a Lazy<bool> to track if we've been asked for the icon.
    private readonly Lazy<bool> fetchedIcon;

    // This member actually tracks any value we've retrieved
    private IIconInfo? _icon;

    // When we're asked for the icon, run the Lazy to generate the icon. That
    // lazy will also OnPropertyChanged(Icon) when it is done, so that we
    // won't block the caller on us generating the icon
    public override IIconInfo? Icon { get => fetchedIcon.Value ? _icon : _icon; set => base.Icon = value; }

    public string FullPath { get; }

    internal virtual bool? IsDirectory { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="FileItem"/> class.
    /// Create a new instance of a FileItem, which can lazy-load the icon for
    /// the file at the given `fullPath`
    /// </summary>
    /// <param name="fullPath">The path of the file this item represents. We'll pull the icon from it.</param>
    /// <param name="isDirectory">true iff this path is a directory, false if it is a file</param>
    internal FileItem(string fullPath, bool? isDirectory)
    {
        FullPath = fullPath;
        this.IsDirectory = isDirectory;
        fetchedIcon = new Lazy<bool>(() =>
        {
            _ = Task.Run(FetchIconAsync);

            // FetchIconAsync will eventually raise an
            // OnPropertyChanged(nameof(Icon)) when it is done
            return true;
        });
    }

    private async Task FetchIconAsync()
    {
        // As of CmdPal Toolkit 0.5, the ThumbnailHelper.GetThumbnail converter
        // has a hard time with rooted file paths that don't include the drive
        // letter.
        // In that case, add the current drive letter.
        var expanded = Environment.ExpandEnvironmentVariables(FullPath);
        var isNetworkPath = expanded.StartsWith("\\\\", StringComparison.InvariantCultureIgnoreCase);
        var pathWithDrive = expanded;
        if (!isNetworkPath)
        {
            var driveLetter = Path.GetPathRoot(pathWithDrive)?.TrimEnd(Path.DirectorySeparatorChar);

            // if we don't have a drive letter, try to get one
            if (string.IsNullOrEmpty(driveLetter) || driveLetter.Length < 2 || driveLetter[1] != ':')
            {
                var drive = Path.GetPathRoot(Environment.CurrentDirectory);
                if (!string.IsNullOrEmpty(drive))
                {
                    pathWithDrive = Path.GetFullPath(Path.Join(drive, ".", expanded));
                }
            }
        }

        if (string.IsNullOrEmpty(pathWithDrive))
        {
            return;
        }

        var stream = await SafeGetThumbnailStream(pathWithDrive);

        IconInfo? icon;
        if (stream is not null)
        {
            icon = IconInfo.FromStream(stream);
        }
        else
        {
            // Failed to retrieve the icon from the shell.
            // No matter. If we're a directory, fall back to using the Folder
            // emoji.
            // Historically, we also tried to use the exe itself as the icon
            // here for files. This did lead to intermittent crashes,
            // unfortunately. As of CmdPal Toolkit 0.6, ThumbnailHelper should
            // be able to successfully get the icon of exes.
            // For more context, see OsClient!14168447
            var actuallyIsDir = (bool)(IsDirectory is null ?
                Directory.Exists(pathWithDrive) :
                (IsDirectory!));
            icon = actuallyIsDir ? Icons.FolderIcon : null;
        }

        if (icon is not null)
        {
            _icon = icon;
            OnPropertyChanged(nameof(Icon));
        }
    }

    private static async Task<IRandomAccessStream?> SafeGetThumbnailStream(string path)
    {
        IRandomAccessStream? stream = null;
        try
        {
            stream = await ThumbnailHelper.GetThumbnail(path);
        }
        catch (ObjectDisposedException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
        catch (FileNotFoundException)
        {
        }
        catch (PathTooLongException)
        {
        }
        catch (IOException)
        {
        }
        catch (OperationCanceledException)
        {
        }

        return stream;
    }
}

internal interface IFileItem
{
    string FullPath { get; }
}
