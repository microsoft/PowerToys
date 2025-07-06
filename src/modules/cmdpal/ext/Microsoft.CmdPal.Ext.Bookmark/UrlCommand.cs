// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;
using ManagedCommon;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.Storage.Streams;
using Windows.System;

namespace Microsoft.CmdPal.Ext.Bookmarks;

public partial class UrlCommand : InvokableCommand
{
    private readonly Lazy<IconInfo> _icon;

    public string Url { get; }

    public override IconInfo Icon { get => _icon.Value; set => base.Icon = value; }

    public UrlCommand(BookmarkData data)
        : this(data.Name, data.Bookmark)
    {
    }

    public UrlCommand(string name, string url)
    {
        Name = Properties.Resources.bookmarks_command_name_open;

        Url = url;

        _icon = new Lazy<IconInfo>(() =>
        {
            ShellHelpers.ParseExecutableAndArgs(Url, out var exe, out var args);
            var t = GetIconForPath(exe);
            t.Wait();
            return t.Result;
        });
    }

    public override CommandResult Invoke()
    {
        var success = LaunchCommand(Url);

        return success ? CommandResult.Dismiss() : CommandResult.KeepOpen();
    }

    internal static bool LaunchCommand(string target)
    {
        ShellHelpers.ParseExecutableAndArgs(target, out var exe, out var args);
        return LaunchCommand(exe, args);
    }

    internal static bool LaunchCommand(string exe, string args)
    {
        if (string.IsNullOrEmpty(exe))
        {
            var message = "No executable found in the command.";
            Logger.LogError(message);

            return false;
        }

        if (ShellHelpers.OpenInShell(exe, args))
        {
            return true;
        }

        // If we reach here, it means the command could not be executed
        // If there aren't args, then try again as a https: uri
        if (string.IsNullOrEmpty(args))
        {
            var uri = GetUri(exe);
            if (uri != null)
            {
                _ = Launcher.LaunchUriAsync(uri);
            }
            else
            {
                Logger.LogError("The provided URL is not valid.");
            }

            return true;
        }

        return false;
    }

    internal static Uri? GetUri(string url)
    {
        Uri? uri;
        if (!Uri.TryCreate(url, UriKind.Absolute, out uri))
        {
            if (!Uri.TryCreate("https://" + url, UriKind.Absolute, out uri))
            {
                return null;
            }
        }

        return uri;
    }

    public static async Task<IconInfo> GetIconForPath(string target)
    {
        IconInfo? icon = null;

        // First, try to get the icon from the thumbnail helper
        // This works for local files and folders
        icon = await MaybeGetIconForPath(target);
        if (icon != null)
        {
            return icon;
        }

        // Okay, that failed. Try to resolve the full path of the executable
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
            if (icon != null)
            {
                return icon;
            }
        }

        // Get the base url up to the first placeholder
        var placeholderIndex = target.IndexOf('{');
        var baseString = placeholderIndex > 0 ? target.Substring(0, placeholderIndex) : target;
        try
        {
            var uri = GetUri(baseString);
            if (uri != null)
            {
                var hostname = uri.Host;
                var faviconUrl = $"{uri.Scheme}://{hostname}/favicon.ico";
                icon = new IconInfo(faviconUrl);
            }
        }
        catch (UriFormatException)
        {
        }

        // If we still don't have an icon, use the target as the icon
        icon = icon ?? new IconInfo(target);

        return icon;
    }

    private static async Task<IconInfo?> MaybeGetIconForPath(string target)
    {
        try
        {
            var stream = await ThumbnailHelper.GetThumbnail(target);
            if (stream != null)
            {
                var data = new IconData(RandomAccessStreamReference.CreateFromStream(stream));
                return new IconInfo(data, data);
            }
        }
        catch
        {
        }

        return null;
    }
}
