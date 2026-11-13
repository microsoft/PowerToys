// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.TextExpander;

/// <summary>
/// Asks which to insert when a clipboard variable has two or more to choose from, such as two email addresses for
/// {{clipboard:email}}, or recent copies in clipboard history for {{clipboard:pick}}, in the choice window. Called on the
/// expansion worker, never the hook's thread.
/// </summary>
internal static class ClipboardChoiceDialog
{
    /// <summary>Returns the pick, or null when cancelled, which cancels the whole snippet.</summary>
    /// <param name="target">The window being typed into; the window opens on its screen.</param>
    /// <param name="accepted">Runs on the window's thread as a pick is accepted, before the window goes.</param>
    public static string? Show(ClipboardChoice choice, IntPtr target, Action? accepted = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(choice);
        cancellationToken.ThrowIfCancellationRequested();

        // Core asks only with two or more; anything less needs no window.
        if (choice.Options.Count < 2)
        {
            accepted?.Invoke();
            return choice.Options.Count == 1 ? choice.Options[0] : string.Empty;
        }

        if (PromptHost.TryChoose(choice, target, accepted, out string? chosen, cancellationToken))
        {
            return chosen;
        }

        // Without the Windows App SDK there's no window to pick in: the first, the newest from history, as without a choice.
        cancellationToken.ThrowIfCancellationRequested();
        accepted?.Invoke();
        return choice.Options[0];
    }

    /// <summary>Gets whether <paramref name="replacement"/> picks from clipboard history, as {{clipboard:pick}} does.</summary>
    internal static bool PicksCopy(string replacement)
        => replacement.Contains("{{clipboard:pick", StringComparison.Ordinal)
            && SnippetTemplate.Parse(replacement).Any(token => token.Kind == SnippetTokenKind.Clipboard && token.Clipboard.PickCopy);

    /// <summary>Waits for clipboard history read ahead, at most <paramref name="timeout"/>: unread if it isn't read by then.</summary>
    internal static ClipboardHistory Await(Task<ClipboardHistory> history, TimeSpan? timeout = null)
    {
        try
        {
            return history.Wait(timeout ?? TimeSpan.FromSeconds(3)) ? history.Result : ClipboardHistory.NotRead;
        }
        catch (AggregateException)
        {
            return ClipboardHistory.NotRead;
        }
    }
}
