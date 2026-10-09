// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using Windows.Win32;

namespace Microsoft.CmdPal.Ext.WebSearch.Helpers.Browser.Providers;

/// <summary>
/// Base class for providers that determine the default browser via application associations.
/// </summary>
internal abstract class AssociationProviderBase : IDefaultBrowserProvider
{
    protected abstract AssociatedApp? FindAssociation();

    public BrowserInfo GetDefaultBrowserInfo()
    {
        var appAssociation = FindAssociation();
        if (appAssociation is null)
        {
            throw new ArgumentNullException(nameof(appAssociation), "Could not determine default browser application.");
        }

        var commandPattern = appAssociation.Command;
        var appAndArgs = SplitAppAndArgs(commandPattern);

        if (string.IsNullOrEmpty(appAndArgs.Path))
        {
            throw new ArgumentOutOfRangeException(nameof(appAndArgs.Path), "Default browser program path could not be determined.");
        }

        // Packaged applications could be an URI. Example: shell:AppsFolder\Microsoft.MicrosoftEdge.Stable_8wekyb3d8bbwe!App
        if (!Path.Exists(appAndArgs.Path) && !Uri.TryCreate(appAndArgs.Path, UriKind.Absolute, out _))
        {
            throw new ArgumentException($"Command validation failed: {commandPattern}", nameof(commandPattern));
        }

        return new BrowserInfo
        {
            Path = appAndArgs.Path,
            Name = appAssociation.FriendlyName ?? Path.GetFileNameWithoutExtension(appAndArgs.Path),
            ArgumentsPattern = appAndArgs.Arguments,
        };
    }

    private static (string? Path, string? Arguments) SplitAppAndArgs(string? commandPattern)
    {
        if (string.IsNullOrEmpty(commandPattern))
        {
            throw new ArgumentOutOfRangeException(nameof(commandPattern), "Default browser program command is not specified.");
        }

        commandPattern = GetIndirectString(commandPattern);

        // HACK: for firefox installed through Microsoft store
        // When installed through Microsoft Firefox the commandPattern does not have
        // quotes for the path. As the Program Files does have a space
        // the extracted path would be invalid, here we add the quotes to fix it
        const string FirefoxExecutableName = "firefox.exe";
        if (commandPattern.Contains(FirefoxExecutableName) && commandPattern.Contains(@"\WindowsApps\") &&
            !commandPattern.StartsWith('\"'))
        {
            var pathEndIndex = commandPattern.IndexOf(FirefoxExecutableName, StringComparison.Ordinal) +
                               FirefoxExecutableName.Length;
            commandPattern = commandPattern.Insert(pathEndIndex, "\"");
            commandPattern = commandPattern.Insert(0, "\"");
        }

        if (commandPattern.StartsWith('\"'))
        {
            var endQuoteIndex = commandPattern.IndexOf('\"', 1);
            if (endQuoteIndex != -1)
            {
                return (commandPattern[1..endQuoteIndex], commandPattern[(endQuoteIndex + 1)..].Trim());
            }
        }
        else
        {
            var spaceIndex = commandPattern.IndexOf(' ');
            if (spaceIndex != -1)
            {
                return (commandPattern[..spaceIndex], commandPattern[(spaceIndex + 1)..].Trim());
            }
        }

        return (null, null);
    }

    protected static string GetIndirectString(string str)
    {
        if (string.IsNullOrEmpty(str) || str[0] != '@')
        {
            return str;
        }

        const int initialCapacity = 128;
        const int maxCapacity = 8192; // Reasonable upper limit
        int hresult;

        unsafe
        {
            // Try with stack allocation first for common cases
            var stackBuffer = stackalloc char[initialCapacity];

            fixed (char* pszSource = str)
            {
                hresult = PInvoke.SHLoadIndirectString(
                    pszSource,
                    stackBuffer,
                    initialCapacity,
                    null);

                // S_OK (0) means success
                if (hresult == 0)
                {
                    return new string(stackBuffer);
                }

                // STRSAFE_E_INSUFFICIENT_BUFFER (0x8007007A) means buffer too small
                // Try with progressively larger heap buffers
                if (unchecked((uint)hresult) == 0x8007007A)
                {
                    for (var capacity = initialCapacity * 2; capacity <= maxCapacity; capacity *= 2)
                    {
                        var heapBuffer = new char[capacity];
                        fixed (char* pBuffer = heapBuffer)
                        {
                            hresult = PInvoke.SHLoadIndirectString(
                                pszSource,
                                pBuffer,
                                (uint)capacity,
                                null);

                            if (hresult == 0)
                            {
                                return new string(pBuffer);
                            }

                            if (unchecked((uint)hresult) != 0x8007007A)
                            {
                                break; // Different error, stop retrying
                            }
                        }
                    }
                }
            }
        }

        throw new InvalidOperationException(
            $"Could not load indirect string. HRESULT: 0x{unchecked((uint)hresult):X8}");
    }
}
