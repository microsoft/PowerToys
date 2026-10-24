// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.TextExpander;

/// <summary>
/// What the {{prompt:Label}} window shows and returns, apart from the window itself so it can be
/// tested. The words come from the engine's resources (<see cref="EngineText"/>).
/// </summary>
internal static class PromptText
{
    /// <summary>Gets the window's title, beside Text Expander's icon.</summary>
    public static string Title => EngineText.Get("Prompt_Title", "Text Expander");

    /// <summary>Gets the box's label when the snippet's question has none, as in {{prompt:}}. Settings' preview uses the same words.</summary>
    public static string UnnamedLabel => EngineText.Get("Prompt_UnnamedLabel", "Your answer");

    public static string OkText => EngineText.Get("Prompt_Ok", "OK");

    public static string CancelText => EngineText.Get("Prompt_Cancel", "Cancel");

    public static string RequiredError => EngineText.Get("Prompt_RequiredError", "Enter an answer to continue.");

    /// <summary>Gets the label shown above the box: the snippet's own words, or <see cref="UnnamedLabel"/>.</summary>
    public static string Label(string? label) => string.IsNullOrWhiteSpace(label) ? UnnamedLabel : label.Trim();

    /// <summary>
    /// Gets what the snippet receives: after OK, the typed text, even when nothing was typed; after
    /// Cancel, Esc or the close button, null, which leaves that spot blank and goes on to the next.
    /// </summary>
    public static string? Result(bool accepted, string? typed) => accepted ? typed ?? string.Empty : null;

    public static bool HasRequiredAnswer(PromptRequest request, string? typed)
        => !request.Required || !string.IsNullOrWhiteSpace(typed);

    /// <summary>
    /// Reads the answer out of the box, then empties the box, even if reading fails, so the answer
    /// doesn't stay in the window after it closes.
    /// </summary>
    public static string Take(Func<string?> read, Action clear)
    {
        try
        {
            return read() ?? string.Empty;
        }
        finally
        {
            clear();
        }
    }
}
