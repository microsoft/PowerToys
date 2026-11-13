// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RobocopyUI.Services.AI
{
    /// <summary>
    /// Selects the subset of robocopy switches worth showing the model for a given request.
    /// </summary>
    /// <remarks>
    /// Listing all ~90 switches costs roughly half the prompt, and every token of it is paid on each
    /// generation and each repair attempt. Ultra-small local models also degrade as the candidate set
    /// grows, because near-identical entries such as /XO, /XN and /XC blur together.
    /// <para>
    /// This is a purely lexical retriever - Okapi BM25 over one short document per switch - chosen
    /// because it needs no model, no embeddings and no extra process, so it adds no measurable latency
    /// and stays deterministic. Retrieval only narrows what the prompt shows; the validator still
    /// accepts any switch in the full catalog, so a miss degrades to a repair attempt rather than a
    /// wrong command.
    /// </para>
    /// </remarks>
    internal sealed class SwitchRetriever
    {
        // Standard Okapi BM25 constants. k1 bounds term-frequency saturation and b controls length
        // normalization; the documents here are all short and similar, so the defaults are fine.
        private const double K1 = 1.2;
        private const double B = 0.75;

        /// <summary>
        /// Switches common enough that they are always offered, regardless of the request wording.
        /// </summary>
        /// <remarks>
        /// Retrieval works on the words the user chose, and users routinely describe an intent without
        /// using any word that appears in the matching switch's text ("back up" implies nothing about
        /// subfolders). Pinning the handful that cover the overwhelming majority of real requests keeps
        /// those available while still dropping the long tail.
        /// </remarks>
        private static readonly string[] AlwaysInclude =
        [
            "/E", "/S", "/MIR", "/PURGE", "/MOVE", "/MOV", "/R", "/W",
            "/COPYALL", "/SEC", "/COPY", "/XF", "/XD", "/MT", "/L", "/LOG",
        ];

        /// <summary>
        /// Maps everyday vocabulary onto the documentation wording the switch texts use.
        /// </summary>
        /// <remarks>
        /// Lexical retrieval matches words, not meaning, so the terms users actually type have to be
        /// bridged to the terms the catalog is written in. Without this, "permissions" retrieves
        /// nothing because the descriptions say "security".
        /// </remarks>
        private static readonly Dictionary<string, string[]> Synonyms = new(StringComparer.OrdinalIgnoreCase)
        {
            ["permission"] = ["security", "acl", "ntfs"],
            ["acl"] = ["security", "permission"],
            ["ownership"] = ["owner", "auditing", "security"],
            ["owner"] = ["ownership", "security"],
            ["subfolder"] = ["subdirectory", "directory", "recurse", "tree"],
            ["subdirectory"] = ["subfolder", "recurse", "tree"],
            ["recursive"] = ["subdirectory", "subfolder", "tree"],
            ["recurse"] = ["subdirectory", "subfolder", "tree"],
            ["folder"] = ["directory"],
            ["directory"] = ["folder"],
            ["mirror"] = ["match", "delete", "extra"],
            ["sync"] = ["mirror", "match", "delete"],
            ["synchronize"] = ["mirror", "match", "delete"],
            ["delete"] = ["purge", "extra", "remove"],
            ["remove"] = ["delete", "purge"],
            ["move"] = ["delete", "source"],
            ["skip"] = ["exclude"],
            ["ignore"] = ["exclude", "skip"],
            ["exclude"] = ["skip"],
            ["retry"] = ["retries", "failed"],
            ["wait"] = ["retries", "seconds"],
            ["fast"] = ["thread", "multiple"],
            ["faster"] = ["thread", "multiple"],
            ["speed"] = ["thread", "multiple"],
            ["thread"] = ["multiple", "faster"],
            ["parallel"] = ["thread", "multiple"],
            ["log"] = ["output", "file"],
            ["logging"] = ["log", "output"],
            ["preview"] = ["test", "report", "without", "copying"],
            ["dry"] = ["test", "report", "without", "copying"],
            ["simulate"] = ["test", "report", "without", "copying"],
            ["hidden"] = ["attribute"],
            ["system"] = ["attribute"],
            ["readonly"] = ["attribute"],
            ["archive"] = ["attribute"],
            ["old"] = ["older", "age"],
            ["older"] = ["age", "maxage"],
            ["newer"] = ["age", "minage"],
            ["day"] = ["days", "age"],
            ["large"] = ["larger", "size", "bytes"],
            ["big"] = ["larger", "size", "bytes"],
            ["small"] = ["smaller", "size", "bytes"],
            ["size"] = ["bytes", "larger", "smaller"],
            ["bandwidth"] = ["gap", "packet", "slow", "link"],
            ["throttle"] = ["gap", "packet", "bandwidth", "slow"],
            ["network"] = ["link", "slow", "compression"],
            ["resume"] = ["restartable"],
            ["restart"] = ["restartable"],
            ["interrupt"] = ["restartable"],
            ["encrypted"] = ["efsraw", "raw"],
            ["timestamp"] = ["times", "timestamps"],
            ["attribute"] = ["attributes"],
            ["schedule"] = ["hours", "run"],
            ["hour"] = ["hours", "run"],
            ["monitor"] = ["changes", "again", "detected"],
            ["watch"] = ["monitor", "changes", "detected"],
        };

        /// <summary>
        /// Words that carry no retrieval signal in this domain. "copy", "file" and "folder" appear in
        /// nearly every switch description, so leaving them in would score everything equally.
        /// </summary>
        private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
        {
            "a", "an", "and", "are", "as", "at", "be", "but", "by", "can", "do", "for", "from", "have",
            "how", "i", "if", "in", "into", "is", "it", "me", "my", "no", "not", "of", "on", "only",
            "or", "please", "so", "that", "the", "their", "them", "then", "there", "these", "they",
            "this", "to", "up", "use", "using", "want", "was", "we", "what", "when", "which", "will",
            "with", "would", "you", "your",
            "copy", "copies", "copying", "robocopy", "file", "files", "folder", "folders", "drive",
        };

        private readonly Dictionary<string, Document> _documents;
        private readonly double _averageLength;
        private readonly int _documentCount;

        public SwitchRetriever(IReadOnlyList<RobocopyOptionDescriptor> catalog, IEnumerable<(string Intent, string Switch)> intents)
        {
            ArgumentNullException.ThrowIfNull(catalog);
            ArgumentNullException.ThrowIfNull(intents);

            var text = new Dictionary<string, StringBuilder>(StringComparer.OrdinalIgnoreCase);

            foreach (var option in catalog)
            {
                if (!text.TryGetValue(option.Name, out var builder))
                {
                    builder = new StringBuilder();
                    text[option.Name] = builder;
                }

                builder.Append(' ').Append(option.Name.TrimStart('/'))
                       .Append(' ').Append(option.Description);

                foreach (var value in option.AllowedValues)
                {
                    builder.Append(' ').Append(value.Description);
                }
            }

            // The cheat-sheet intents are phrased the way users describe the goal, which makes them the
            // highest-signal text available for matching a request to a switch.
            foreach (var (intent, switchName) in intents)
            {
                if (text.TryGetValue(switchName, out var builder))
                {
                    builder.Append(' ').Append(intent);
                }
            }

            _documents = text.ToDictionary(
                pair => pair.Key,
                pair => Document.Create(Tokenize(pair.Value.ToString())),
                StringComparer.OrdinalIgnoreCase);

            _documentCount = _documents.Count;
            _averageLength = _documentCount == 0 ? 1 : _documents.Values.Average(document => document.Length);

            _documentFrequency = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var document in _documents.Values)
            {
                foreach (var term in document.Terms.Keys)
                {
                    _documentFrequency[term] = _documentFrequency.TryGetValue(term, out var count) ? count + 1 : 1;
                }
            }
        }

        private readonly Dictionary<string, int> _documentFrequency;

        /// <summary>
        /// Returns the switch names to show for a request, or all of them when the request is too
        /// vague to retrieve against.
        /// </summary>
        /// <param name="requestText">Everything the user has typed for this request.</param>
        /// <param name="topCount">How many retrieved switches to add on top of the pinned ones.</param>
        public HashSet<string> Retrieve(string requestText, int topCount = 22)
        {
            var selected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Escape hatch for the eval harness, so the retrieval-on and retrieval-off numbers can be
            // compared on identical builds. Not set in normal use.
            if (Environment.GetEnvironmentVariable("ROBOCOPY_AI_DISABLE_RETRIEVAL") == "1")
            {
                selected.UnionWith(_documents.Keys);
                return selected;
            }

            foreach (var name in AlwaysInclude)
            {
                if (_documents.ContainsKey(name))
                {
                    selected.Add(name);
                }
            }

            var queryTerms = Expand(Tokenize(requestText));

            if (queryTerms.Count == 0)
            {
                return selected;
            }

            var ranked = _documents
                .Select(pair => (Name: pair.Key, Score: Score(queryTerms, pair.Value)))
                .Where(entry => entry.Score > 0)
                .OrderByDescending(entry => entry.Score)
                .ThenBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
                .Take(topCount);

            foreach (var entry in ranked)
            {
                selected.Add(entry.Name);
            }

            return selected;
        }

        private double Score(List<string> queryTerms, Document document)
        {
            double score = 0;

            foreach (var term in queryTerms)
            {
                if (!document.Terms.TryGetValue(term, out var frequency))
                {
                    continue;
                }

                var documentFrequency = _documentFrequency.TryGetValue(term, out var count) ? count : 0;

                // Standard BM25 inverse document frequency, using the +1 variant so a term present in
                // most documents scores near zero instead of going negative.
                var idf = Math.Log(1 + ((_documentCount - documentFrequency + 0.5) / (documentFrequency + 0.5)));

                var numerator = frequency * (K1 + 1);
                var denominator = frequency + (K1 * (1 - B + (B * document.Length / _averageLength)));

                score += idf * numerator / denominator;
            }

            return score;
        }

        private static List<string> Expand(List<string> terms)
        {
            var expanded = new List<string>(terms);

            foreach (var term in terms)
            {
                if (Synonyms.TryGetValue(term, out var related))
                {
                    expanded.AddRange(related);
                }
            }

            return expanded;
        }

        private static List<string> Tokenize(string text)
        {
            var tokens = new List<string>();

            if (string.IsNullOrWhiteSpace(text))
            {
                return tokens;
            }

            var current = new StringBuilder();

            foreach (var character in text)
            {
                if (char.IsLetterOrDigit(character))
                {
                    current.Append(char.ToLowerInvariant(character));
                    continue;
                }

                Flush(tokens, current);
            }

            Flush(tokens, current);
            return tokens;
        }

        private static void Flush(List<string> tokens, StringBuilder current)
        {
            if (current.Length == 0)
            {
                return;
            }

            var token = current.ToString();
            current.Clear();

            if (token.Length < 2 || StopWords.Contains(token))
            {
                return;
            }

            tokens.Add(Normalize(token));
        }

        /// <summary>
        /// Folds the handful of inflections that matter here onto a common form.
        /// </summary>
        /// <remarks>
        /// A real stemmer would be overkill and would mangle switch names. This only strips the plural
        /// and progressive endings that separate a user's wording from the documentation's, and leaves
        /// short tokens alone so "/S" style fragments and words like "less" survive intact.
        /// </remarks>
        private static string Normalize(string token)
        {
            if (token.Length > 4 && token.EndsWith("ing", StringComparison.Ordinal))
            {
                return token[..^3];
            }

            if (token.Length > 4 && token.EndsWith("ies", StringComparison.Ordinal))
            {
                return token[..^3] + "y";
            }

            if (token.Length > 3 && token.EndsWith("es", StringComparison.Ordinal))
            {
                return token[..^2];
            }

            if (token.Length > 3 && token.EndsWith('s') && !token.EndsWith("ss", StringComparison.Ordinal))
            {
                return token[..^1];
            }

            return token;
        }

        private sealed record Document(Dictionary<string, int> Terms, int Length)
        {
            public static Document Create(List<string> tokens)
            {
                var terms = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

                foreach (var token in tokens)
                {
                    terms[token] = terms.TryGetValue(token, out var count) ? count + 1 : 1;
                }

                return new Document(terms, tokens.Count);
            }
        }
    }
}
