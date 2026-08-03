// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using ShortcutGuide.Models;

namespace ShortcutGuide.Helpers
{
    public static class ShortcutSearchMatcher
    {
        public static bool Matches(ShortcutEntry shortcut, string? query)
        {
            string searchText = query?.Trim() ?? string.Empty;
            if (searchText.Length == 0)
            {
                return true;
            }

            if (Contains(shortcut.Name, searchText) || Contains(shortcut.Description, searchText))
            {
                return true;
            }

            foreach (var description in shortcut.Shortcut ?? [])
            {
                foreach (string label in GetSearchLabels(description))
                {
                    if (Contains(label, searchText))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static IEnumerable<string> GetSearchLabels(ShortcutDescription description)
        {
            if (description.Win)
            {
                yield return "Win Windows";
            }

            if (description.Ctrl)
            {
                yield return "Ctrl Control";
            }

            if (description.Alt)
            {
                yield return "Alt";
            }

            if (description.Shift)
            {
                yield return "Shift";
            }

            foreach (string key in description.Keys ?? [])
            {
                yield return GetKeySearchLabel(key);
            }
        }

        private static string GetKeySearchLabel(string key)
        {
            if (int.TryParse(key, out int keyCode))
            {
                return keyCode switch
                {
                    37 => "Left Left Arrow",
                    38 => "Up Up Arrow",
                    39 => "Right Right Arrow",
                    40 => "Down Down Arrow",
                    _ => Microsoft.PowerToys.Settings.UI.Library.Utilities.Helper.GetKeyName((uint)keyCode),
                };
            }

            return key switch
            {
                "Up" or "<Up>" => "Up Up Arrow",
                "Down" or "<Down>" => "Down Down Arrow",
                "Left" or "<Left>" => "Left Left Arrow",
                "Right" or "<Right>" => "Right Right Arrow",
                "Back" or "<Backspace>" => "Back Backspace",
                "<TASKBAR1-9>" => "Num",
                "<ArrowUD>" => "Up Down Arrow",
                "<ArrowLR>" => "Left Right Arrow",
                "<Arrow>" => "Left Right Up Down Arrow",
                "<Enter>" => "Enter",
                "<LessThan>" => "<",
                "<GreaterThan>" => ">",
                "<Escape>" => "Esc Escape",
                string value when value.StartsWith('<') && value.EndsWith('>') => value.Trim('<', '>'),
                _ => key,
            };
        }

        private static bool Contains(string? value, string searchText)
        {
            return value?.Contains(searchText, StringComparison.OrdinalIgnoreCase) == true;
        }
    }
}
