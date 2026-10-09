// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading;
using System.Threading.Tasks;
using ManagedCommon;
using Microsoft.CmdPal.Ext.Bookmarks.Helpers;
using Microsoft.CommandPalette.Extensions;
using Microsoft.Win32;

namespace Microsoft.CmdPal.Ext.Bookmarks.Services;

internal class IconLocator : IBookmarkIconLocator
{
    private readonly IFaviconLoader _faviconLoader;

    public IconLocator()
        : this(new FaviconLoader())
    {
    }

    private IconLocator(IFaviconLoader faviconLoader)
    {
        ArgumentNullException.ThrowIfNull(faviconLoader);
        _faviconLoader = faviconLoader;
    }

    public async Task<IIconInfo> GetIconForPath(
        Classification classification,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(classification);

        var icon = classification.Kind switch
        {
            CommandKind.WebUrl => await TryGetWebIcon(classification.Target),
            CommandKind.Protocol => await TryGetProtocolIcon(classification.Target),
            CommandKind.FileExecutable => await TryGetExecutableIcon(classification.Target),
            CommandKind.Unknown => FallbackIcon(classification),
            _ => await MaybeGetIconForPath(classification.Target),
        };

        return icon ?? FallbackIcon(classification);
    }

    private async Task<IIconInfo?> TryGetWebIcon(string target)
    {
        // Get the base url up to the first placeholder
        var placeholderIndex = target.IndexOf('{');
        var baseString = placeholderIndex > 0 ? target[..placeholderIndex] : target;
        try
        {
            var uri = new Uri(baseString);
            var iconStream = await _faviconLoader.TryGetFaviconAsync(uri, CancellationToken.None);
            if (iconStream != null)
            {
                return IconInfo.FromStream(iconStream);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to get web bookmark favicon for " + baseString, ex);
        }

        return null;
    }

    private static async Task<IIconInfo?> TryGetExecutableIcon(string target)
    {
        IIconInfo? icon = null;
        var exeExists = false;
        var fullExePath = string.Empty;
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

            // Use Task.Run with timeout - this will actually timeout even if the sync operations don't respond to cancellation
            var pathResolutionTask = Task.Run(
                () =>
                {
                    // Don't check cancellation token here - let the Task timeout handle it
                    exeExists = ShellHelpers.FileExistInPath(target, out fullExePath);
                },
                CancellationToken.None);

            // Wait for either completion or timeout
            pathResolutionTask.Wait(cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Debug.WriteLine("Operation was canceled.");
        }

        if (exeExists)
        {
            // If the executable exists, try to get the icon from the file
            icon = await MaybeGetIconForPath(fullExePath);
            if (icon is not null)
            {
                return icon;
            }
        }

        return icon;
    }

    private static async Task<IconInfo?> TryGetProtocolIcon(string target)
    {
        // Special case for steam: protocol - use game icon
        // Steam protocol have only a file name (steam.exe) associated with it, but is not
        // in PATH or AppPaths. So we can't resolve it to an executable. But at the same time,
        // this is a very common protocol, so we special-case it here.
        if (target.StartsWith("steam:", StringComparison.OrdinalIgnoreCase))
        {
            return Icons.BookmarkTypes.Game;
        }

        // extract protocol from classification.Target (until the first ':'):
        IconInfo? icon = null;
        var colonIndex = target.IndexOf(':');
        string protocol;
        if (colonIndex > 0)
        {
            protocol = target[..colonIndex];
        }
        else
        {
            return icon;
        }

        icon = await ThumbnailHelper.GetProtocolIconStream(protocol, true) is { } stream
            ? IconInfo.FromStream(stream)
            : null;

        if (icon is null)
        {
            var protocolIconPath = ProtocolIconResolver.GetIconString(protocol);
            if (protocolIconPath is not null)
            {
                icon = new IconInfo(protocolIconPath);
            }
        }

        return icon;
    }

    private static IconInfo FallbackIcon(Classification classification)
    {
        return classification.Kind switch
        {
            CommandKind.FileExecutable => Icons.BookmarkTypes.Application,
            CommandKind.FileDocument => Icons.BookmarkTypes.FilePath,
            CommandKind.Directory => Icons.BookmarkTypes.FolderPath,
            CommandKind.PathCommand => Icons.BookmarkTypes.Command,
            CommandKind.Aumid => Icons.BookmarkTypes.Application,
            CommandKind.Shortcut => Icons.BookmarkTypes.Application,
            CommandKind.InternetShortcut => Icons.BookmarkTypes.WebUrl,
            CommandKind.WebUrl => Icons.BookmarkTypes.WebUrl,
            CommandKind.Protocol => Icons.BookmarkTypes.Application,
            _ => Icons.BookmarkTypes.Unknown,
        };
    }

    private static async Task<IconInfo?> MaybeGetIconForPath(string target)
    {
        try
        {
            var stream = await ThumbnailHelper.GetThumbnail(target);
            if (stream is not null)
            {
                return IconInfo.FromStream(stream);
            }

            if (ShellNames.TryGetFileSystemPath(target, out var fileSystemPath))
            {
                stream = await ThumbnailHelper.GetThumbnail(fileSystemPath);
                if (stream is not null)
                {
                    return IconInfo.FromStream(stream);
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogDebug($"Failed to load icon for {target}\n" + ex);
        }

        return null;
    }

    internal static class ProtocolIconResolver
    {
        /// <summary>
        /// Gets the icon resource string for a given URI protocol (e.g. "steam" or "mailto").
        /// Returns something like "C:\Path\app.exe,0" or null if not found.
        /// </summary>
        public static string? GetIconString(string protocol)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(protocol))
                {
                    return null;
                }

                protocol = protocol.TrimEnd(':').ToLowerInvariant();

                // Try HKCR\<protocol>\DefaultIcon
                using (var di = Registry.ClassesRoot.OpenSubKey(protocol + "\\DefaultIcon"))
                {
                    var value = di?.GetValue(null) as string;
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        return value;
                    }
                }

                // Fallback: HKCR\<protocol>\shell\open\command
                using (var cmd = Registry.ClassesRoot.OpenSubKey(protocol + "\\shell\\open\\command"))
                {
                    var command = cmd?.GetValue(null) as string;
                    if (!string.IsNullOrWhiteSpace(command))
                    {
                        var exe = ExtractExecutable(command);
                        if (!string.IsNullOrWhiteSpace(exe))
                        {
                            return exe; // default index 0 implied
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to get protocol information from registry; will return nothing instead", ex);
            }

            return null;
        }

        private static string ExtractExecutable(string command)
        {
            command = command.Trim();

            if (command.StartsWith('\"'))
            {
                var end = command.IndexOf('"', 1);
                if (end > 1)
                {
                    return command[1..end];
                }
            }

            var space = command.IndexOf(' ');
            return space > 0 ? command[..space] : command;
        }
    }
}
