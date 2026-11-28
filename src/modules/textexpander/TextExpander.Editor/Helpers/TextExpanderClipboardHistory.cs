// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.Security;
using Microsoft.Win32;

namespace Microsoft.PowerToys.TextExpander.Editor.Helpers
{
    /// <summary>
    /// Whether Windows clipboard history is on, for snippets that pick a copy from it when they run, as {{clipboard:pick}}
    /// does: with it off, nothing goes in there, and the editor says so.
    /// </summary>
    internal static class TextExpanderClipboardHistory
    {
        /// <summary>The page of Windows Settings with clipboard history's switch.</summary>
        public const string SettingsUri = "ms-settings:clipboard";

        private const string PolicyKey = @"SOFTWARE\Policies\Microsoft\Windows\System";

        /// <summary>
        /// Gets whether clipboard history is on, and whether a policy turned it off, which the user can't change. On when it
        /// can't be told, so the editor never warns by mistake.
        /// </summary>
        public static (bool On, bool TurnedOffByPolicy) Read()
        {
            bool turnedOffByPolicy = TurnedOffIn(Registry.LocalMachine) || TurnedOffIn(Registry.CurrentUser);
            bool on;
            try
            {
                on = global::Windows.ApplicationModel.DataTransfer.Clipboard.IsHistoryEnabled();
            }
            catch (Exception ex) when (ex is COMException or UnauthorizedAccessException or InvalidOperationException)
            {
                on = !turnedOffByPolicy;
            }

            return (on && !turnedOffByPolicy, turnedOffByPolicy);
        }

        /// <summary>
        /// Gets what the editor says where a snippet picks a copy from clipboard history while it's off, or null when it's on.
        /// </summary>
        public static string Note(bool on, bool turnedOffByPolicy, Func<string, string> resource)
        {
            ArgumentNullException.ThrowIfNull(resource);
            return on ? null : resource(turnedOffByPolicy ? "TextExpander_ClipboardHistory_OffByPolicy" : "TextExpander_ClipboardHistory_Off");
        }

        private static bool TurnedOffIn(RegistryKey hive)
        {
            try
            {
                using RegistryKey key = hive.OpenSubKey(PolicyKey);
                return key?.GetValue("AllowClipboardHistory") is int value && value == 0;
            }
            catch (Exception ex) when (ex is SecurityException or UnauthorizedAccessException or System.IO.IOException)
            {
                return false;
            }
        }
    }
}
