// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.TextExpander;

/// <summary>
/// Asks for a choice a clipboard variable needs when it runs, such as one of several links for {{clipboard:link:pick}},
/// in the choice window. Called on the expansion worker, never the hook's thread.
/// </summary>
internal static class ClipboardChoiceDialog
{
    /// <summary>Returns the pick, an empty text to insert nothing, or null when cancelled, which cancels the whole snippet.</summary>
    /// <param name="target">The window being typed into; the window opens on its screen.</param>
    /// <param name="accepted">Runs on the window's thread as a pick is accepted, before the window goes.</param>
    public static string? Show(ClipboardChoice choice, IntPtr target, Action? accepted = null)
    {
        // Clipboard history isn't read here yet, so a copy from it is nothing.
        if (choice.Kind == ClipboardChoiceKind.Copy || choice.Options.Count == 0)
        {
            accepted?.Invoke();
            return string.Empty;
        }

        if (PromptHost.TryChoose(choice, target, accepted, out string? chosen))
        {
            return chosen;
        }

        // Without the Windows App SDK there's no window to pick in: the first, as without a choice.
        accepted?.Invoke();
        return choice.Options[0];
    }
}
