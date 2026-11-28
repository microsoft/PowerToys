// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using Microsoft.Win32;
using WinClipboard = global::Windows.ApplicationModel.DataTransfer.Clipboard;
using WinClipboardHistoryItemsResultStatus = global::Windows.ApplicationModel.DataTransfer.ClipboardHistoryItemsResultStatus;
using WinStandardDataFormats = global::Windows.ApplicationModel.DataTransfer.StandardDataFormats;

namespace Microsoft.PowerToys.TextExpander.UITests;

/// <summary>
/// Windows clipboard history as the test sees it, read by the rules Text Expander uses for {{clipboard:pick}}: text copies,
/// newest first, each different one once, 25 at most. What's read is compared, never logged.
/// </summary>
internal static class ClipboardHistoryState
{
    public static bool IsOn()
    {
        try
        {
            return WinClipboard.IsHistoryEnabled();
        }
        catch (COMException)
        {
            return false;
        }
    }

    /// <summary>Gets whether a policy turned clipboard history off, so the editor offers no link to turn it on.</summary>
    public static bool TurnedOffByPolicy()
    {
        foreach (RegistryKey hive in new[] { Registry.LocalMachine, Registry.CurrentUser })
        {
            using RegistryKey? key = hive.OpenSubKey(@"SOFTWARE\Policies\Microsoft\Windows\System");
            if (key?.GetValue("AllowClipboardHistory") is int value && value == 0)
            {
                return true;
            }
        }

        return false;
    }

    public static IReadOnlyList<string> ReadTexts()
    {
        if (!IsOn())
        {
            return [];
        }

        var result = WinClipboard.GetHistoryItemsAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();
        if (result.Status != WinClipboardHistoryItemsResultStatus.Success)
        {
            return [];
        }

        var texts = new List<string>();
        foreach (var item in result.Items)
        {
            if (texts.Count >= 25)
            {
                break;
            }

            if (item.Content.Contains(WinStandardDataFormats.Text))
            {
                string text = item.Content.GetTextAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();
                if (!string.IsNullOrEmpty(text) && !texts.Contains(text, StringComparer.Ordinal))
                {
                    texts.Add(text);
                }
            }
        }

        return texts;
    }
}
