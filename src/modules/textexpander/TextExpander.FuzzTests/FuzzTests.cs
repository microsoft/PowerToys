// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.Text;

using Microsoft.PowerToys.TextExpander;

namespace TextExpander.FuzzTests;

/// <summary>
/// OneFuzz targets for the snippets file and snippet text, which users and other apps edit freely. A failed check
/// throws, so OneFuzz reports a disagreement between the editor, the engine and the file the way it reports a crash.
/// </summary>
public static class FuzzTests
{
    private const string AddedTrigger = "fuzz";

    // Regions that write numbers differently: a dot or a comma for decimals, and commas, dots, spaces, apostrophes or
    // India's grouping between groups.
    private static readonly string[] Regions = ["en-US", "de-DE", "fr-FR", "de-CH", "en-IN", "ar-SA"];

    private static readonly string[] ClipboardVariables =
    [
        "{{clipboard:email:all}}",
        "{{clipboard:link:2}}",
        "{{clipboard:phone:last}}",
        "{{clipboard:number:pick}}",
        "{{clipboard:pick:link:pick}}",
    ];

    /// <summary>
    /// Reads the input as the snippets file, as the engine and the editor do. A file that isn't one is refused with
    /// <see cref="SnippetFileFormatException"/> and nothing else. One that is must save and read back the same snippets,
    /// unedited and after re-saving each snippet the editor would accept and adding the input as one, and the engine's
    /// trigger matcher must agree with a linear scan. Text kept without being read that can't be written back, such as
    /// half of an emoji, is refused with <see cref="SnippetFileWriteException"/>, which the editor reports instead of saving.
    /// </summary>
    public static void FuzzSnippetFile(ReadOnlySpan<byte> input)
    {
        SnippetFile file;
        try
        {
            file = SnippetFile.Read(input);
        }
        catch (SnippetFileFormatException)
        {
            return;
        }

        string text = Encoding.UTF8.GetString(input);
        CheckMatching(file.Snippets.Select(entry => entry.Trigger).ToHashSet(StringComparer.Ordinal), text);

        byte[] saved;
        try
        {
            saved = file.ToBytes();
        }
        catch (SnippetFileWriteException)
        {
            return;
        }

        CheckSameSnippets(file, SnippetFile.Read(saved), "An unedited file read back different snippets.");

        SnippetFile edited = file.Clone();
        foreach (SnippetFile.Entry entry in file.Snippets)
        {
            if (SnippetFile.IsValidTrigger(entry.Trigger, out _) && edited.FindDuplicate(entry.Trigger, entry.Id) is null)
            {
                edited.Upsert(entry.Id, entry.Trigger, entry.Replacement);
            }
        }

        if (edited.FindDuplicate(AddedTrigger) is null)
        {
            // Pasting the input in as a new snippet puts any text through the writer.
            edited.Upsert(null, AddedTrigger, text);
        }

        CheckSameSnippets(edited, SnippetFile.Read(edited.ToBytes()), "A saved file read back different snippets.");
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
            _ = Variables.MayAsk(snippet);
            _ = Variables.UsesSensitiveRuntimeInput(snippet);

            // Answer each prompt with its label, paste the snippet itself and pick the last choice, so codes also arrive
            // where they must stay literal.
            _ = Variables.Expand(snippet, label => label, () => snippet, choice => choice.Options.Count > 0 ? choice.Options[^1] : snippet);
        }
    }

    /// <summary>
    /// Reads the input as a copy, as clipboard variables do, for each part in regions with different number separators.
    /// Every match must be text from the copy, each different one once, and every clipboard variable must expand over it.
    /// </summary>
    public static void FuzzClipboardParts(ReadOnlySpan<byte> input)
    {
        string copy = Encoding.UTF8.GetString(input);
        foreach (string name in Regions)
        {
            CultureInfo culture = CultureInfo.GetCultureInfo(name);
            foreach (ClipboardPart part in Enum.GetValues<ClipboardPart>())
            {
                IReadOnlyList<string> found = ClipboardParts.Find(copy, part, culture);
                Check(found.Distinct(StringComparer.Ordinal).Count() == found.Count, "A match was found twice.");
                foreach (string match in found)
                {
                    Check(match.Length > 0 && copy.Contains(match, StringComparison.Ordinal), "A match isn't text from the copy.");
                }
            }
        }

        foreach (string variable in ClipboardVariables)
        {
            Check(ClipboardVariable.TryParse(variable, out ClipboardVariable parsed) && parsed.ToToken() == variable, "A clipboard variable didn't read back the same.");
            _ = Variables.Expand(variable, _ => null, () => copy, choice => choice.Options.Count > 0 ? choice.Options[^1] : copy);
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

    private static void CheckSameSnippets(SnippetFile expected, SnippetFile actual, string message)
        => Check(
            actual.Snippets.Select(entry => (entry.Trigger, entry.Replacement)).SequenceEqual(expected.Snippets.Select(entry => (entry.Trigger, entry.Replacement))),
            message);

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
