// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.PowerToys.Settings.UI.Helpers
{
    /// <summary>
    /// The triggers <see cref="TextExpanderTriggerReach"/> looks for. They are looked up by span,
    /// so checking the text being typed allocates nothing, and by length, so only lengths that
    /// some trigger has are tried.
    /// </summary>
    internal sealed class TextExpanderTriggerSet
    {
        private readonly HashSet<string> _triggers;
        private readonly HashSet<string>.AlternateLookup<ReadOnlySpan<char>> _lookup;
        private readonly Dictionary<int, int> _countByLength = new();
        private int[] _lengths;

        public TextExpanderTriggerSet(IEnumerable<string> triggers = null, bool caseSensitive = true)
        {
            _triggers = new HashSet<string>(caseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase);
            _lookup = _triggers.GetAlternateLookup<ReadOnlySpan<char>>();
            foreach (string trigger in triggers ?? Enumerable.Empty<string>())
            {
                Add(trigger);
            }
        }

        public int Count => _triggers.Count;

        public IEnumerable<string> Triggers => _triggers;

        /// <summary>Gets the lengths the triggers have, longest first.</summary>
        public int[] LengthsDescending => _lengths ??= _countByLength.Keys.OrderByDescending(length => length).ToArray();

        public bool Contains(string trigger) => trigger != null && _triggers.Contains(trigger);

        public bool Contains(ReadOnlySpan<char> trigger) => _lookup.Contains(trigger);

        /// <summary>Adds a trigger. Empty ones are ignored, as the engine ignores them.</summary>
        public bool Add(string trigger)
        {
            if (string.IsNullOrEmpty(trigger) || !_triggers.Add(trigger))
            {
                return false;
            }

            int count = _countByLength.GetValueOrDefault(trigger.Length);
            _countByLength[trigger.Length] = count + 1;
            if (count == 0)
            {
                _lengths = null;
            }

            return true;
        }

        public bool Remove(string trigger)
        {
            if (trigger == null || !_triggers.Remove(trigger))
            {
                return false;
            }

            int count = _countByLength[trigger.Length] - 1;
            if (count == 0)
            {
                _countByLength.Remove(trigger.Length);
                _lengths = null;
            }
            else
            {
                _countByLength[trigger.Length] = count;
            }

            return true;
        }
    }
}
