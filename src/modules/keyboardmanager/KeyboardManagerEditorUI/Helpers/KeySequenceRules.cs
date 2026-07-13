// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using KeyboardManagerEditorUI.Interop;

namespace KeyboardManagerEditorUI.Helpers
{
    internal static class KeySequenceRules
    {
        public static bool CanAppend(IReadOnlyList<KeyType> keys, bool allowChords)
        {
            if (keys.Count == 0)
            {
                return true;
            }

            int actionCount = keys.Count(key => key == KeyType.Action);
            if (actionCount == 0)
            {
                return keys.Count <= EditorConstants.MaxShortcutModifiers;
            }

            if (actionCount == EditorConstants.MaxShortcutActions)
            {
                int actionIndex = FindFirstAction(keys);
                return allowChords && actionIndex > 0 && actionIndex == keys.Count - 1;
            }

            return false;
        }

        public static KeySequenceUpdate EvaluateSelection(
            IReadOnlyList<KeyType> currentKeys,
            int changedIndex,
            KeyType selectedKey,
            bool allowChords)
        {
            if (changedIndex < 0 || changedIndex > currentKeys.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(changedIndex));
            }

            var candidate = currentKeys.ToList();
            if (changedIndex == candidate.Count)
            {
                candidate.Add(selectedKey);
            }
            else
            {
                candidate[changedIndex] = selectedKey;
            }

            int resultCount = candidate.Count;

            if (selectedKey == KeyType.Action)
            {
                if (changedIndex == 0 && candidate.Count > 1)
                {
                    return KeySequenceUpdate.Invalid(KeySequenceError.ShortcutStartWithModifier);
                }

                int priorActionIndex = FindFirstAction(candidate, changedIndex);
                if (priorActionIndex >= 0)
                {
                    if (!allowChords)
                    {
                        return KeySequenceUpdate.Invalid(KeySequenceError.ChordsDisabled);
                    }

                    if (priorActionIndex == 0)
                    {
                        return KeySequenceUpdate.Invalid(KeySequenceError.ShortcutStartWithModifier);
                    }

                    if (changedIndex != priorActionIndex + 1)
                    {
                        return KeySequenceUpdate.Invalid(KeySequenceError.ModifierAfterAction);
                    }

                    resultCount = changedIndex + 1;
                }
                else
                {
                    resultCount = changedIndex + 1;

                    // Preserve an existing second action when replacing the first action in a chord.
                    if (allowChords &&
                        changedIndex > 0 &&
                        candidate.Count > changedIndex + 1 &&
                        candidate[changedIndex + 1] == KeyType.Action)
                    {
                        resultCount++;
                    }
                }
            }
            else
            {
                if (candidate.Take(changedIndex).Any(key => key == KeyType.Action))
                {
                    return KeySequenceUpdate.Invalid(KeySequenceError.ModifierAfterAction);
                }

                for (int i = 0; i < candidate.Count; i++)
                {
                    if (i != changedIndex && candidate[i] == selectedKey)
                    {
                        return KeySequenceUpdate.Invalid(KeySequenceError.RepeatedModifier);
                    }
                }
            }

            if (candidate.Count > resultCount)
            {
                candidate.RemoveRange(resultCount, candidate.Count - resultCount);
            }

            KeySequenceError validationError = Validate(candidate, allowChords);
            return validationError == KeySequenceError.None
                ? KeySequenceUpdate.Valid(resultCount)
                : KeySequenceUpdate.Invalid(validationError);
        }

        public static int GetKeyCountWithoutChord(IReadOnlyList<KeyType> keys)
        {
            int firstActionIndex = FindFirstAction(keys);
            if (firstActionIndex >= 0 &&
                keys.Skip(firstActionIndex + 1).Any(key => key == KeyType.Action))
            {
                return firstActionIndex + 1;
            }

            return keys.Count;
        }

        private static KeySequenceError Validate(IReadOnlyList<KeyType> keys, bool allowChords)
        {
            var modifiers = keys.Where(key => key != KeyType.Action).ToList();
            if (modifiers.Count > EditorConstants.MaxShortcutModifiers)
            {
                return KeySequenceError.MaxShortcutSize;
            }

            if (modifiers.Distinct().Count() != modifiers.Count)
            {
                return KeySequenceError.RepeatedModifier;
            }

            var actionIndices = keys
                .Select((key, index) => (key, index))
                .Where(item => item.key == KeyType.Action)
                .Select(item => item.index)
                .ToList();

            if (actionIndices.Count == 0)
            {
                return keys.Count <= EditorConstants.MaxShortcutModifiers
                    ? KeySequenceError.None
                    : KeySequenceError.MaxShortcutSize;
            }

            if (keys.Count > 1 && actionIndices[0] == 0)
            {
                return KeySequenceError.ShortcutStartWithModifier;
            }

            if (keys.Skip(actionIndices[0]).Any(key => key != KeyType.Action))
            {
                return KeySequenceError.ModifierAfterAction;
            }

            if (actionIndices.Count > EditorConstants.MaxChordActions)
            {
                return KeySequenceError.MaxShortcutSize;
            }

            if (actionIndices.Count > EditorConstants.MaxShortcutActions && !allowChords)
            {
                return KeySequenceError.ChordsDisabled;
            }

            int maxSize = actionIndices.Count == EditorConstants.MaxChordActions
                ? EditorConstants.MaxChordSize
                : EditorConstants.MaxShortcutSize;

            return keys.Count <= maxSize
                ? KeySequenceError.None
                : KeySequenceError.MaxShortcutSize;
        }

        private static int FindFirstAction(IReadOnlyList<KeyType> keys, int count)
        {
            for (int i = 0; i < count; i++)
            {
                if (keys[i] == KeyType.Action)
                {
                    return i;
                }
            }

            return -1;
        }

        private static int FindFirstAction(IReadOnlyList<KeyType> keys) => FindFirstAction(keys, keys.Count);
    }
}
