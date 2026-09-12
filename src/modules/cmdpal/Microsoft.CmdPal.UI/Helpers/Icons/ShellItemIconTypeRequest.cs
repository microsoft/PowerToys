// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.Helpers;

internal static class ShellItemIconTypeRequest
{
    private const string StandardPrefix = "|ShellFileType|";
    private const string JumboPrefix = "|JumboShellFileType|";

    public static bool TryCreate(
        ShellItemIconRequest request,
        out ShellItemIconRequest typeRequest)
    {
        typeRequest = default;
        if (request.LocationMode != ShellItemIconLocationMode.ExactItem)
        {
            return false;
        }

        try
        {
            // A path-only request cannot distinguish a dotted directory name from a file
            // without probing the filesystem. Keep that probe off the SourceRequested STA;
            // exact refinement corrects the uncommon provisional mismatch.
            var extension = Path.GetExtension(request.ItemPath.AsSpan());
            if (extension.Length <= 1)
            {
                return false;
            }

            // Shortcut icons are commonly specific to their targets. Avoid flashing the
            // registered generic shortcut icon before the exact Shell lookup completes.
            if (extension.Equals(".lnk", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var prefix = request.Jumbo ? JumboPrefix : StandardPrefix;
            var cacheIdentity = string.Create(
                prefix.Length + extension.Length,
                (prefix, extension: extension.ToString()),
                static (destination, state) =>
                {
                    state.prefix.AsSpan().CopyTo(destination);
                    var extensionDestination = destination[state.prefix.Length..];
                    for (var index = 0; index < state.extension.Length; index++)
                    {
                        extensionDestination[index] = char.ToUpperInvariant(state.extension[index]);
                    }
                });

            typeRequest = new ShellItemIconRequest(
                cacheIdentity,
                request.ItemPath,
                request.Jumbo,
                ShellItemIconLocationMode.FileType);
            return true;
        }
        catch
        {
            // A provisional file-type icon is an optimization only. Malformed paths
            // continue through the exact Shell-item path without changing behavior.
            typeRequest = default;
            return false;
        }
    }
}
