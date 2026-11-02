// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.TextExpander
{
    /// <summary>
    /// Works out whether typing a trigger can ever reach it, using the runtime's longest-suffix matcher and
    /// word-boundary rules.
    /// </summary>
    internal static class TextExpanderTriggerReach
    {
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
        /// <paramref name="trigger"/> it starts, or -1. The triggers in <paramref name="added"/>, if any, count as
        /// <paramref name="triggers"/> too. Allocates nothing unless it finds one.
        /// </summary>
        public static (string Trigger, char Next)? FindBlocker(string trigger, TextExpanderTriggerSet triggers, bool wordMode, out int start, TriggerIndex added = null)
        {
            for (int typed = 1; typed < trigger.Length; typed++)
            {
                // With the word wait, only a space, punctuation other than '_', or a symbol lets anything fire.
                if (wordMode && !WordBoundaryRules.IsTerminator(trigger[typed]))
                {
                    continue;
                }

                string fired = triggers.Index.MatchLongestSuffix(trigger.AsSpan(0, typed));
                if (added?.MatchLongestSuffix(trigger.AsSpan(0, typed)) is { } longer && longer.Length > (fired?.Length ?? 0))
                {
                    fired = longer;
                }

                if (fired is not null)
                {
                    start = typed - fired.Length;
                    if (!wordMode || WordBoundaryRules.StartsAtWordBoundary(trigger.AsSpan(0, start), fired))
                    {
                        return (trigger.Substring(start, fired.Length), trigger[typed]);
                    }
                }
            }

            start = -1;
            return null;
        }
    }
}
