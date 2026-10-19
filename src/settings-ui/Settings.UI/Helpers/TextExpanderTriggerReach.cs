// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.PowerToys.Settings.UI.Helpers
{
    /// <summary>
    /// Works out whether typing a trigger can ever reach it, by the engine's own rules.
    ///
    /// <para>
    /// The engine fires the longest trigger that ends at the caret. Normally that happens as soon
    /// as its last character is typed. With Wait for the whole word, it happens when a space,
    /// punctuation or symbol follows, and a trigger that starts with a letter or digit must also
    /// start a word. So another trigger that is complete partway through typing this one fires
    /// first, and this one never expands. The Settings project cannot reference the engine, so
    /// these rules are transcribed from <c>KeyboardHook</c> and <c>WordBoundary</c>; a change to
    /// one has to be made to the other.
    /// </para>
    /// </summary>
    internal static class TextExpanderTriggerReach
    {
        /// <summary>Same as the engine's <c>WordBoundary.IsWordCharacter</c>.</summary>
        public static bool IsWordCharacter(char c) => char.IsLetterOrDigit(c) || c == '_';

        /// <summary>Same as the engine's <c>WordBoundary.IsTerminator</c>.</summary>
        public static bool IsTerminator(char c) => char.IsWhiteSpace(c) || char.IsPunctuation(c) || char.IsSymbol(c);

        /// <summary>Same as the engine's <c>WordBoundary.StartsAtWordBoundary</c>.</summary>
        public static bool StartsAtWordBoundary(ReadOnlySpan<char> textBefore, ReadOnlySpan<char> trigger)
            => trigger.Length == 0 || !IsWordCharacter(trigger[0]) || textBefore.Length == 0 || !IsWordCharacter(textBefore[^1]);

        /// <summary>
        /// Finds the trigger that fires first while <paramref name="trigger"/> is being typed, and
        /// the character whose typing fires it, or null when <paramref name="trigger"/> can be
        /// reached.
        /// </summary>
        public static (string Trigger, char Next)? FindBlocker(string trigger, TextExpanderTriggerSet triggers, bool wordMode)
            => FindBlocker(trigger, triggers, wordMode, out _);

        /// <summary>
        /// Finds the trigger that fires first while <paramref name="trigger"/> is being typed, as
        /// <see cref="FindBlocker(string, TextExpanderTriggerSet, bool)"/> does, and where in
        /// <paramref name="trigger"/> it starts, or -1. Allocates nothing unless it finds one.
        /// </summary>
        public static (string Trigger, char Next)? FindBlocker(string trigger, TextExpanderTriggerSet triggers, bool wordMode, out int start)
        {
            int[] lengths = triggers.LengthsDescending;
            for (int typed = 1; typed < trigger.Length; typed++)
            {
                // With the word wait, only a space, punctuation or symbol lets anything fire.
                if (wordMode && !IsTerminator(trigger[typed]))
                {
                    continue;
                }

                // The engine considers only the longest trigger ending here; if that one doesn't
                // start a word, nothing fires at this point.
                foreach (int length in lengths)
                {
                    if (length > typed)
                    {
                        continue;
                    }

                    start = typed - length;
                    ReadOnlySpan<char> fired = trigger.AsSpan(start, length);
                    if (!triggers.Contains(fired))
                    {
                        continue;
                    }

                    if (!wordMode || StartsAtWordBoundary(trigger.AsSpan(0, start), fired))
                    {
                        return (fired.ToString(), trigger[typed]);
                    }

                    break;
                }
            }

            start = -1;
            return null;
        }
    }
}
