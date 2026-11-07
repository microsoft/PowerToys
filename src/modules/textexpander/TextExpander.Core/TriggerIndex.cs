// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

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
/// With case sensitivity off, characters are compared as the replacement map compares triggers,
/// with <see cref="StringComparer.OrdinalIgnoreCase"/>. That comparer cases a surrogate pair as
/// one character (U+1E922 is the lowercase of U+1E900, though neither half has a case), so a pair
/// is one step of the walk.
/// </para>
///
/// <para>
/// Immutable once built, so the hook reads it without locking, and allocation-free to query.
/// </para>
/// </summary>
public sealed class TriggerIndex
{
    private sealed class Node
    {
        // The only child, while there is just one. Most nodes past the first few characters of a
        // trigger have one child, so a dictionary for each would mostly be overhead.
        private int _onlyKey;
        private Node? _onlyChild;

        /// <summary>Children keyed by the next character *leftwards*, once there are two or more.</summary>
        private Dictionary<int, Node>? _children;

        /// <summary>Gets or sets the trigger ending here, when this node completes one.</summary>
        public string? Trigger { get; set; }

        public bool TryGetChild(int key, [NotNullWhen(true)] out Node? child)
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

        public Node GetOrAddChild(int key)
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
                _children = new Dictionary<int, Node>(2) { [_onlyKey] = _onlyChild! };
                _onlyChild = null;
            }

            _children[key] = child;
            return child;
        }
    }

    private readonly Node _root;
    private readonly bool _caseSensitive;

    /// <summary>
    /// With case sensitivity off, the key of every character the triggers use, found with the
    /// replacement map's comparer so that each capitalization of a character finds the same key.
    /// With it on, a character's key is its UTF-16 value.
    /// </summary>
    private readonly Dictionary<string, int>.AlternateLookup<ReadOnlySpan<char>> _keys;

    /// <summary>Gets length of the longest indexed trigger. Zero when the index is empty, which lets
    /// the caller skip the lookup entirely.</summary>
    public int MaxTriggerLength { get; }

    /// <summary>Gets how many triggers are indexed.</summary>
    public int Count { get; }

    public static readonly TriggerIndex Empty = new(new Node(), 0, 0, keys: null);

    private TriggerIndex(Node root, int maxTriggerLength, int count, Dictionary<string, int>? keys)
    {
        _root = root;
        MaxTriggerLength = maxTriggerLength;
        Count = count;
        _caseSensitive = keys is null;
        if (keys is not null)
        {
            _keys = keys.GetAlternateLookup<ReadOnlySpan<char>>();
        }
    }

    public static TriggerIndex Build(IEnumerable<string> triggers, bool caseSensitive = true)
    {
        ArgumentNullException.ThrowIfNull(triggers);

        var root = new Node();
        Dictionary<string, int>? keys = caseSensitive ? null : new(StringComparer.OrdinalIgnoreCase);
        int max = 0;
        int count = 0;

        foreach (string trigger in triggers)
        {
            if (string.IsNullOrEmpty(trigger))
            {
                continue;
            }

            Node node = root;
            for (int end = trigger.Length; end > 0;)
            {
                ReadOnlySpan<char> character = CharacterBefore(trigger, end, caseSensitive);
                end -= character.Length;

                int key = character[0];
                if (keys is not null && !keys.GetAlternateLookup<ReadOnlySpan<char>>().TryGetValue(character, out key))
                {
                    key = keys.Count;
                    keys.Add(character.ToString(), key);
                }

                node = node.GetOrAddChild(key);
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

        return count == 0 && caseSensitive ? Empty : new TriggerIndex(root, max, count, keys);
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

        int stop = Math.Max(0, tail.Length - MaxTriggerLength);
        for (int end = tail.Length; end > stop;)
        {
            ReadOnlySpan<char> character = CharacterBefore(tail, end, _caseSensitive);
            end -= character.Length;

            int key = character[0];
            if ((!_caseSensitive && !_keys.TryGetValue(character, out key)) || !node.TryGetChild(key, out Node? next))
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

    /// <summary>
    /// Gets the character that ends at <paramref name="end"/>: one UTF-16 unit or, when case is
    /// ignored, a whole surrogate pair, because only the pair has a case.
    /// </summary>
    private static ReadOnlySpan<char> CharacterBefore(ReadOnlySpan<char> text, int end, bool caseSensitive)
        => !caseSensitive && end > 1 && char.IsLowSurrogate(text[end - 1]) && char.IsHighSurrogate(text[end - 2])
            ? text.Slice(end - 2, 2)
            : text.Slice(end - 1, 1);
}
