// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.Common.Messages;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.Windows.Storage.Pickers;
using Windows.Win32;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Microsoft.CmdPal.UI.Controls.AdaptiveCards;

internal static class AdaptiveFilePicker
{
    public static async Task<string?> PickFileAsync(
        FrameworkElement owner,
        IEnumerable<string> fileTypeFilter,
        string commitButtonText)
    {
        var picker = new FileOpenPicker(GetOwnerWindowId(owner))
        {
            CommitButtonText = commitButtonText,
        };

        var filterWasAdded = false;
        foreach (var filter in fileTypeFilter.Select(NormalizeFileTypeFilter).Where(static filter => filter is not null))
        {
            picker.FileTypeFilter.Add(filter!);
            filterWasAdded = true;
        }

        if (!filterWasAdded)
        {
            picker.FileTypeFilter.Add("*");
        }

        return (await picker.PickSingleFileAsync())?.Path;
    }

    public static async Task<string?> PickFolderAsync(FrameworkElement owner, string commitButtonText)
    {
        var picker = new FolderPicker(GetOwnerWindowId(owner))
        {
            CommitButtonText = commitButtonText,
        };
        return (await picker.PickSingleFolderAsync())?.Path;
    }

    /// <summary>
    /// Resolves the window that should own the picker dialog. Command Palette has more than one
    /// window — the palette and the settings window — and a settings form renders in either, so
    /// the owner has to be resolved per invocation rather than assumed to be the palette.
    /// </summary>
    private static WindowId GetOwnerWindowId(FrameworkElement owner)
    {
        // Preferred, but null in Command Palette's windows today.
        var appWindowId = owner.XamlRoot?.ContentIslandEnvironment?.AppWindowId;
        if (appWindowId is not null && appWindowId.Value.Value != 0)
        {
            return appWindowId.Value;
        }

        // Both windows live on this thread, so the active one is the window the user just clicked
        // in. GA_ROOTOWNER maps a menu flyout's popup window back to the window that owns it.
        var activeWindow = PInvoke.GetActiveWindow();
        if (activeWindow != IntPtr.Zero)
        {
            var ownerWindow = PInvoke.GetAncestor(activeWindow, GET_ANCESTOR_FLAGS.GA_ROOTOWNER);
            return Win32Interop.GetWindowIdFromWindow(ownerWindow != IntPtr.Zero ? ownerWindow : activeWindow);
        }

        // Last resort: the palette window answers this, which is right when it is the only window.
        var message = new GetHwndMessage();
        WeakReferenceMessenger.Default.Send(message);
        return message.Hwnd != 0
            ? Win32Interop.GetWindowIdFromWindow((nint)message.Hwnd)
            : throw new InvalidOperationException("Could not resolve a window to own the picker.");
    }

    private static string? NormalizeFileTypeFilter(string filter)
    {
        var normalized = filter.Trim();
        if (string.IsNullOrEmpty(normalized))
        {
            return null;
        }

        if (normalized is "*" or "*.*")
        {
            return "*";
        }

        if (normalized.StartsWith("*.", StringComparison.Ordinal))
        {
            normalized = normalized[1..];
        }

        return normalized.StartsWith('.')
            ? normalized
            : $".{normalized}";
    }
}
