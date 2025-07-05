// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using ManagedCommon;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.System;

namespace Microsoft.CmdPal.Ext.Bookmarks;

public partial class UrlCommand : InvokableCommand
{
    public string Type { get; }

    public string Url { get; }

    public UrlCommand(BookmarkData data)
        : this(data.Name, data.Bookmark, data.Type)
    {
    }

    public UrlCommand(string name, string url, string type)
    {
        Name = name;
        Type = type;
        Url = url;
        Icon = new IconInfo(IconFromUrl(Url, type));
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
            var message = "No executable found in the command."; // TODO:LOC
            Logger.LogError(message);

            var warnToast = new ToastStatusMessage(new StatusMessage
            {
                Message = message,
                State = MessageState.Warning,
            });
            warnToast.Show();

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

        var errorMessage = $"Failed to launch command {exe} {args}"; // TODO:LOC
        var toast = new ToastStatusMessage(new StatusMessage
        {
            Message = errorMessage,
            State = MessageState.Error,
        });
        toast.Show();
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

    internal static string IconFromUrl(string url, string type)
    {
        switch (type)
        {
            case "file":
                return "📄";
            case "folder":
                return "📁";
            case "web":
            default:
                // Get the base url up to the first placeholder
                var placeholderIndex = url.IndexOf('{');
                var baseString = placeholderIndex > 0 ? url.Substring(0, placeholderIndex) : url;
                try
                {
                    var uri = GetUri(baseString);
                    if (uri != null)
                    {
                        var hostname = uri.Host;
                        var faviconUrl = $"{uri.Scheme}://{hostname}/favicon.ico";
                        return faviconUrl;
                    }
                }
                catch (UriFormatException ex)
                {
                    Logger.LogError(ex.Message);
                }

                return "🔗";
        }
    }
}
