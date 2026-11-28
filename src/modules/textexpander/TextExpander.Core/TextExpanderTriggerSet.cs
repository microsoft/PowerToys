// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.TextExpander
{
    /// <summary>
    /// The triggers <see cref="TextExpanderTriggerReach"/> looks for, matched with the runtime's <see cref="TriggerIndex"/>.
    /// </summary>
    internal sealed class TextExpanderTriggerSet
    {
        private readonly HashSet<string> _triggers;
        private readonly bool _caseSensitive;
        private TriggerIndex _index;

        public TextExpanderTriggerSet(IEnumerable<string> triggers = null, bool caseSensitive = true)
        {
            _caseSensitive = caseSensitive;
            _triggers = new HashSet<string>(caseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase);
            foreach (string trigger in triggers ?? Enumerable.Empty<string>())
            {
                Add(trigger);
            }
        }

        public IEnumerable<string> Triggers => _triggers;

        public TriggerIndex Index => _index ??= TriggerIndex.Build(_triggers, _caseSensitive);

        public bool Contains(string trigger) => trigger != null && _triggers.Contains(trigger);

        /// <summary>Adds a trigger. Empty ones are ignored, as the engine ignores them.</summary>
        public void Add(string trigger)
        {
            if (!string.IsNullOrEmpty(trigger) && _triggers.Add(trigger))
            {
                _index = null;
            }
        }
    }
}
