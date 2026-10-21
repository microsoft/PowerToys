// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.CodeAnalysis;

namespace Microsoft.PowerToys.TextExpander;

/// <summary>
/// Finds the longest trigger that is a suffix of what the user has just typed, in time that
/// depends on the length of the longest trigger rather than on how many triggers there are.
///
/// <para>
/// This runs inside the <c>WH_KEYBOARD_LL</c> callback, on every keystroke, system-wide. The
/// previous implementation walked every trigger longest-first and called <c>EndsWith</c> on each:
/// linear in the number of snippets, and worst at exactly the wrong time, because triggers that
/// share a long prefix make each individual comparison expensive too. Measured at 10,000 such
/// triggers it cost a mean of 8.57 ms per keystroke. Windows silently unhooks a low-level hook
/// whose thread exceeds <c>LowLevelHooksTimeout</c> — 300 ms by default — and an unhooked app
/// keeps running while expanding nothing at all, until it is restarted.
/// </para>
///
/// <para>
/// The structure is a trie built from each trigger's <b>last</b> character backwards, which is the
/// natural shape for a suffix question: walking the typed tail from its end follows one path down
/// the trie and visits at most as many nodes as the longest trigger has characters. Every deeper
/// terminal found on the way is a longer match, so the last one seen is the answer — the
/// longest-wins rule the old scan got from its sort order comes out of the walk for free.
/// </para>
///
/// <para>
/// Immutable once built, so the hook reads it without locking, and allocation-free to query.
/// </para>
/// </summary>
internal sealed class TriggerIndex
{
    private sealed class Node
    {
        // The only child, while there is just one. Most nodes past the first few characters of a
        // trigger have one child, so a dictionary for each would mostly be overhead.
        private char _onlyKey;
        private Node? _onlyChild;

        /// <summary>Children keyed by the next character *leftwards*, once there are two or more.</summary>
        private Dictionary<char, Node>? _children;

        /// <summary>Gets or sets the trigger ending here, when this node completes one.</summary>
        public string? Trigger { get; set; }

        public bool TryGetChild(char key, [NotNullWhen(true)] out Node? child)
        {
            if (_onlyChild is not null)
            {
                child = _onlyKey == key ? _onlyChild : null;
                return child is not null;
            }

            if (_children is not null)
            {
                return _children.TryGetValue(key, out child);
            }

            child = null;
            return false;
        }

        public Node GetOrAddChild(char key)
        {
            if (TryGetChild(key, out Node? existing))
            {
                return existing;
            }

            var child = new Node();
            if (_onlyChild is null && _children is null)
            {
                _onlyKey = key;
                _onlyChild = child;
                return child;
            }

            if (_children is null)
            {
                _children = new Dictionary<char, Node>(2) { [_onlyKey] = _onlyChild! };
                _onlyChild = null;
            }

            _children[key] = child;
            return child;
        }
    }

    private readonly Node _root;
    private readonly bool _caseSensitive;

    /// <summary>Gets length of the longest indexed trigger. Zero when the index is empty, which lets
    /// the caller skip the lookup entirely.</summary>
    public int MaxTriggerLength { get; }

    /// <summary>Gets how many triggers are indexed.</summary>
    public int Count { get; }

    public static readonly TriggerIndex Empty = new(new Node(), 0, 0, caseSensitive: true);

    private TriggerIndex(Node root, int maxTriggerLength, int count, bool caseSensitive)
    {
        _root = root;
        MaxTriggerLength = maxTriggerLength;
        Count = count;
        _caseSensitive = caseSensitive;
    }

    public static TriggerIndex Build(IEnumerable<string> triggers, bool caseSensitive = true)
    {
        ArgumentNullException.ThrowIfNull(triggers);

        var root = new Node();
        int max = 0;
        int count = 0;

        foreach (string trigger in triggers)
        {
            if (string.IsNullOrEmpty(trigger))
            {
                continue;
            }

            Node node = root;
            for (int i = trigger.Length - 1; i >= 0; i--)
            {
                node = node.GetOrAddChild(Normalize(trigger[i], caseSensitive));
            }

            // Two spellings of the same trigger cannot occur — the source is a dictionary keyed by
            // trigger — but a repeated enumerable would otherwise inflate the count.
            if (node.Trigger is null)
            {
                count++;
            }

            node.Trigger = trigger;

            if (trigger.Length > max)
            {
                max = trigger.Length;
            }
        }

        return count == 0 && caseSensitive ? Empty : new TriggerIndex(root, max, count, caseSensitive);
    }

    /// <summary>
    /// The longest indexed trigger that <paramref name="tail"/> ends with, or null when it ends
    /// with none of them. Allocates nothing.
    /// </summary>
    public string? MatchLongestSuffix(ReadOnlySpan<char> tail)
    {
        if (tail.Length == 0 || MaxTriggerLength == 0)
        {
            return null;
        }

        Node node = _root;
        string? best = null;

        int limit = Math.Min(tail.Length, MaxTriggerLength);
        for (int i = 0; i < limit; i++)
        {
            if (!node.TryGetChild(Normalize(tail[tail.Length - 1 - i], _caseSensitive), out Node? next))
            {
                break;
            }

            node = next;

            // Each step leftwards is one character more of trigger matched, so a terminal found
            // later is strictly longer than one found earlier. Keeping the latest gives the
            // longest match without any comparison of lengths.
            if (node.Trigger is not null)
            {
                best = node.Trigger;
            }
        }

        return best;
    }

    private static char Normalize(char value, bool caseSensitive)
        => caseSensitive ? value : char.ToUpperInvariant(value);
}
