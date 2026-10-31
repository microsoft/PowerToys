// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace KeyboardManagerEditorUI.Helpers
{
    /// <summary>
    /// Builds the names screen readers announce for mapping rows and their per-row controls.
    /// Without these, list items fall back to the CLR type name and row controls are unnamed.
    /// </summary>
    public static class MappingAccessibility
    {
        public const string KeyRemapFormatKey = "Mapping_A11y_KeyRemapFormat";
        public const string DisabledFormatKey = "Mapping_A11y_DisabledFormat";
        public const string TextFormatKey = "Mapping_A11y_TextFormat";
        public const string ProgramFormatKey = "Mapping_A11y_ProgramFormat";
        public const string UrlFormatKey = "Mapping_A11y_UrlFormat";
        public const string AloneSuffixFormatKey = "Mapping_A11y_AloneSuffixFormat";
        public const string AppSuffixFormatKey = "Mapping_A11y_AppSuffixFormat";
        public const string EnableToggleFormatKey = "Mapping_A11y_EnableToggleFormat";
        public const string MoreOptionsFormatKey = "Mapping_A11y_MoreOptionsFormat";
        public const string SavedFormatKey = "Mapping_A11y_SavedFormat";
        public const string DeletedFormatKey = "Mapping_A11y_DeletedFormat";
        public const string DeletedCountFormatKey = "Mapping_A11y_DeletedCountFormat";
        public const string FilterCountFormatKey = "Mapping_A11y_FilterCountFormat";
        public const string FilterNoneKey = "Mapping_A11y_FilterNone";
        public const string FilterOneKey = "Mapping_A11y_FilterOne";
        public const string TriggerKeysFormatKey = "Mapping_A11y_TriggerKeysFormat";
        public const string ActionKeysFormatKey = "Mapping_A11y_ActionKeysFormat";
        public const string MessageFormatKey = "Mapping_A11y_MessageFormat";

        public static string JoinKeys(IEnumerable<string>? keys)
        {
            return keys == null
                ? string.Empty
                : string.Join(" + ", keys.Where(k => !string.IsNullOrWhiteSpace(k)));
        }

        public static string Describe(
            Func<string, string> getString,
            string actionFormatKey,
            IEnumerable<string>? trigger,
            string? target,
            bool isAlone,
            string? appName)
        {
            ArgumentNullException.ThrowIfNull(getString);

            string name = Format(getString(actionFormatKey), JoinKeys(trigger), target ?? string.Empty);

            if (isAlone)
            {
                name = Format(getString(AloneSuffixFormatKey), name);
            }

            if (!string.IsNullOrWhiteSpace(appName))
            {
                name = Format(getString(AppSuffixFormatKey), name, appName);
            }

            return name;
        }

        public static string ForRemapping(Remapping remapping, Func<string, string> getString)
        {
            ArgumentNullException.ThrowIfNull(remapping);
            bool isDisabled = remapping.RemappedKeys.Count == 0;
            return Describe(
                getString,
                isDisabled ? DisabledFormatKey : KeyRemapFormatKey,
                remapping.Shortcut,
                JoinKeys(remapping.RemappedKeys),
                remapping.IsAlone,
                remapping.AppName);
        }

        public static string ForText(TextMapping mapping, Func<string, string> getString)
        {
            ArgumentNullException.ThrowIfNull(mapping);
            return Describe(getString, TextFormatKey, mapping.Shortcut, mapping.Text, false, mapping.AppName);
        }

        public static string ForProgram(ProgramShortcut shortcut, Func<string, string> getString)
        {
            ArgumentNullException.ThrowIfNull(shortcut);
            return Describe(getString, ProgramFormatKey, shortcut.Shortcut, shortcut.AppToRun, false, shortcut.AppName);
        }

        public static string ForUrl(URLShortcut shortcut, Func<string, string> getString)
        {
            ArgumentNullException.ThrowIfNull(shortcut);
            return Describe(getString, UrlFormatKey, shortcut.Shortcut, shortcut.URL, false, shortcut.AppName);
        }

        public static string EnableToggleName(string rowName, Func<string, string> getString)
        {
            ArgumentNullException.ThrowIfNull(getString);
            return Format(getString(EnableToggleFormatKey), rowName);
        }

        public static string MoreOptionsName(string rowName, Func<string, string> getString)
        {
            ArgumentNullException.ThrowIfNull(getString);
            return Format(getString(MoreOptionsFormatKey), rowName);
        }

        public static string SavedAnnouncement(string rowName, Func<string, string> getString)
        {
            ArgumentNullException.ThrowIfNull(getString);
            return Format(getString(SavedFormatKey), rowName);
        }

        public static string DeletedAnnouncement(string rowName, Func<string, string> getString)
        {
            ArgumentNullException.ThrowIfNull(getString);
            return Format(getString(DeletedFormatKey), rowName);
        }

        public static string DeletedCountAnnouncement(int count, Func<string, string> getString)
        {
            ArgumentNullException.ThrowIfNull(getString);
            return Format(getString(DeletedCountFormatKey), count);
        }

        public static string FilterResultAnnouncement(int visibleCount, Func<string, string> getString)
        {
            ArgumentNullException.ThrowIfNull(getString);
            return visibleCount switch
            {
                0 => Format(getString(FilterNoneKey)),
                1 => Format(getString(FilterOneKey)),
                _ => Format(getString(FilterCountFormatKey), visibleCount),
            };
        }

        /// <summary>
        /// Text announced while recording keys; empty when nothing is held so screen readers stay quiet.
        /// </summary>
        public static string RecordedKeysAnnouncement(bool isTrigger, IEnumerable<string>? keys, Func<string, string> getString)
        {
            ArgumentNullException.ThrowIfNull(getString);
            string joined = JoinKeys(keys);
            return joined.Length == 0
                ? string.Empty
                : Format(getString(isTrigger ? TriggerKeysFormatKey : ActionKeysFormatKey), joined);
        }

        public static string MessageAnnouncement(string? title, string? message, Func<string, string> getString)
        {
            ArgumentNullException.ThrowIfNull(getString);
            if (string.IsNullOrWhiteSpace(title))
            {
                return message ?? string.Empty;
            }

            return string.IsNullOrWhiteSpace(message) ? title : Format(getString(MessageFormatKey), title, message);
        }

        private static string Format(string format, params object[] args)
        {
            // A missing resource must never leave a control unnamed; fall back to the raw values.
            if (string.IsNullOrEmpty(format))
            {
                return string.Join(", ", args.Select(a => a?.ToString()).Where(a => !string.IsNullOrEmpty(a)));
            }

            return string.Format(CultureInfo.CurrentCulture, format, args);
        }
    }
}
