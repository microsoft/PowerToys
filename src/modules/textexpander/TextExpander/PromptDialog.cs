// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.TextExpander;

/// <summary>
/// Asks the user for a {{prompt:Label}} answer: in the Windows 11 prompt window, or in the classic
/// box if the Windows App SDK can't start. Called on the expansion worker, never the hook's thread.
/// </summary>
internal static class PromptDialog
{
    /// <summary>Returns the answer, or null when cancelled, which leaves that spot blank.</summary>
    /// <param name="label">The snippet's question.</param>
    /// <param name="target">The window being typed into; the prompt opens on its screen.</param>
    public static string? Show(PromptRequest request, IntPtr target)
        => PromptHost.TryAsk(request, target, out string? answer) ? answer : ClassicPromptDialog.Show(request);
}
