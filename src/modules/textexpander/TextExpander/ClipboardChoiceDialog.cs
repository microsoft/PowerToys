// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;

namespace Microsoft.PowerToys.TextExpander;

/// <summary>
/// Asks for a choice a clipboard variable needs when it runs, such as one of several links for {{clipboard:link:pick}}, or
/// one of the recent copies in clipboard history for {{clipboard:pick}}, in the choice window. Called on the expansion
/// worker, never the hook's thread.
/// </summary>
internal static class ClipboardChoiceDialog
{
    /// <summary>Returns the pick, an empty text to insert nothing, or null when cancelled, which cancels the whole snippet.</summary>
    /// <param name="target">The window being typed into; the window opens on its screen.</param>
    /// <param name="accepted">Runs on the window's thread as a pick is accepted, before the window goes.</param>
    public static string? Show(ClipboardChoice choice, IntPtr target, Action? accepted = null)
        => Show(choice, target, accepted, ClipboardHistoryReader.ReadTexts);

    /// <param name="readHistory">Reads the text copies in clipboard history, newest first.</param>
    internal static string? Show(ClipboardChoice choice, IntPtr target, Action? accepted, Func<IReadOnlyList<string>> readHistory)
    {
        if (choice.Kind == ClipboardChoiceKind.Copy)
        {
            choice = choice with { Options = CopiesFor(choice.Part, readHistory(), CultureInfo.CurrentCulture) };

            // As with matches, only two or more ask: one goes in, and none, as with history off, inserts nothing.
            if (choice.Options.Count < 2)
            {
                accepted?.Invoke();
                return choice.Options.Count == 1 ? choice.Options[0] : string.Empty;
            }
        }
        else if (choice.Options.Count == 0)
        {
            accepted?.Invoke();
            return string.Empty;
        }

        if (PromptHost.TryChoose(choice, target, accepted, out string? chosen))
        {
            return chosen;
        }

        // Without the Windows App SDK there's no window to pick in: the first, the newest copy, as without a choice.
        accepted?.Invoke();
        return choice.Options[0];
    }

    /// <summary>
    /// Gets the copies a snippet can pick from: for a part, as {{clipboard:pick:link}} takes, only the copies that have
    /// one, so the list holds only what can go in. In their order, newest first.
    /// </summary>
    internal static IReadOnlyList<string> CopiesFor(ClipboardPart part, IReadOnlyList<string> copies, CultureInfo culture)
        => part == ClipboardPart.All ? copies : copies.Where(copy => ClipboardParts.Find(copy, part, culture).Count > 0).ToList();

    /// <summary>Gets whether <paramref name="replacement"/> picks a copy from clipboard history, as {{clipboard:pick}} does.</summary>
    internal static bool PicksCopy(string replacement)
        => replacement.Contains("{{clipboard:pick", StringComparison.Ordinal)
            && SnippetTemplate.Parse(replacement).Any(token => token.Kind == SnippetTokenKind.Clipboard && token.Clipboard.PickCopy);

    /// <summary>Waits for clipboard history read ahead, at most <paramref name="timeout"/>: none if it isn't read by then.</summary>
    internal static IReadOnlyList<string> Await(Task<IReadOnlyList<string>> history, TimeSpan? timeout = null)
    {
        try
        {
            return history.Wait(timeout ?? TimeSpan.FromSeconds(3)) ? history.Result : Array.Empty<string>();
        }
        catch (AggregateException)
        {
            return Array.Empty<string>();
        }
    }
}
