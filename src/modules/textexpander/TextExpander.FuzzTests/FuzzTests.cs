// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text;

using Microsoft.PowerToys.TextExpander;

namespace TextExpander.FuzzTests;

/// <summary>
/// OneFuzz targets for the snippet file and snippet text, which users and other apps edit freely. A failed check
/// throws, so OneFuzz reports a disagreement between the editor, the engine and the file the way it reports a crash.
/// </summary>
public static class FuzzTests
{
    private const string AddedTrigger = "fuzz";

    /// <summary>
    /// Reads the input as the snippet file, as the engine does. Writing it back unedited, re-saving each snippet the
    /// editor would accept and adding one must read back the same snippets, and the engine's trigger matcher must
    /// agree with a linear scan.
    /// </summary>
    public static void FuzzSnippetFile(ReadOnlySpan<byte> input)
    {
        using var reader = new StreamReader(new MemoryStream(input.ToArray()), Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        string text = reader.ReadToEnd();
        string[] lines = ReadLines(text);

        SnippetFile file = SnippetFile.Parse(lines);
        Check(file.ToLines().SequenceEqual(lines), "An unedited file was not written back line for line.");

        CheckMatching(file.Snippets.Select(entry => entry.Trigger).ToHashSet(StringComparer.Ordinal), text);

        SnippetFile edited = file.Clone();
        foreach (SnippetFile.Entry entry in file.Snippets)
        {
            if (SnippetFile.IsValidTrigger(entry.Trigger, out _) && edited.FindDuplicate(entry.Trigger, entry.Id) is null)
            {
                // Changing the text and changing it back makes the entry render afresh instead of keeping its lines.
                edited.Upsert(entry.Id, entry.Trigger, entry.Replacement + " ");
                edited.Upsert(entry.Id, entry.Trigger, entry.Replacement);
            }
        }

        if (edited.FindDuplicate(AddedTrigger) is null)
        {
            // Pasting the input in as a new snippet puts raw line breaks through the editor's renderer.
            edited.Upsert(null, AddedTrigger, SnippetFile.IsValidReplacement(text, out _) ? text : string.Empty);
        }

        // Compared line for line: the editor writes these lines with the file's own line breaks.
        string[] saved = edited.ToLines();
        Check(!saved.Any(line => line.AsSpan().ContainsAny('\r', '\n')), "A saved line holds a line break.");

        SnippetFile reloaded = SnippetFile.Parse(saved);
        Check(
            reloaded.Snippets.Select(entry => (entry.Trigger, entry.Replacement)).SequenceEqual(edited.Snippets.Select(entry => (entry.Trigger, entry.Replacement))),
            "A saved file read back different snippets.");
    }

    /// <summary>
    /// Expands the input as a snippet, and again as a date format and as a prompt label, which random bytes rarely
    /// spell out.
    /// </summary>
    public static void FuzzExpandSnippet(ReadOnlySpan<byte> input)
    {
        string text = Encoding.UTF8.GetString(input);
        foreach (string snippet in new[] { text, "{{date:" + text + "}}", "{{prompt:" + text + "}}" })
        {
            _ = Variables.NeedsPrompt(snippet);
            _ = Variables.UsesSensitiveRuntimeInput(snippet);

            // Answer each prompt with its label and paste the snippet itself, so codes also arrive where they must stay literal.
            _ = Variables.Expand(snippet, label => label, () => snippet);
        }
    }

    private static void CheckMatching(HashSet<string> triggers, string typed)
    {
        TriggerIndex index = TriggerIndex.Build(triggers);
        string? longest = null;
        foreach (string trigger in triggers)
        {
            Check(index.MatchLongestSuffix(trigger) == trigger, "Typing a trigger did not match that trigger.");

            if (typed.EndsWith(trigger, StringComparison.Ordinal) && trigger.Length > (longest?.Length ?? 0))
            {
                longest = trigger;
            }
        }

        Check(index.MatchLongestSuffix(typed) == longest, "The trigger matcher disagreed with a linear scan.");
        _ = TriggerIndex.Build(triggers, caseSensitive: false).MatchLongestSuffix(typed);
    }

    // Splits on CRLF, LF and lone CR, as the engine's StreamReader.ReadLine does.
    private static string[] ReadLines(string text)
    {
        var lines = new List<string>();
        using var reader = new StringReader(text);
        for (string? line = reader.ReadLine(); line is not null; line = reader.ReadLine())
        {
            lines.Add(line);
        }

        return lines.ToArray();
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
