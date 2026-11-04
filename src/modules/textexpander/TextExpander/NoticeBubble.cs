// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.TextExpander;

/// <summary>
/// A small notice at the caret that never takes the focus, such as why a snippet's clipboard spot was left empty. This
/// stands in for the notice bubble until its window is merged in, and shows nothing.
/// </summary>
internal static class NoticeBubble
{
    /// <summary>
    /// Shows a notice at the caret of <paramref name="targetWindow"/>, replacing any notice showing. Thread-safe and
    /// non-blocking; never activates or takes the focus. Not from the hook callback.
    /// </summary>
    public static void Show(IntPtr targetWindow, string heading, string message)
    {
        ArgumentNullException.ThrowIfNull(heading);
        ArgumentNullException.ThrowIfNull(message);
    }

    /// <summary>Cheap and non-blocking, safe from the keyboard and mouse hook callbacks: a showing notice starts to fade.</summary>
    public static void OnUserInput()
    {
    }

    /// <summary>Takes down any notice showing.</summary>
    public static void Dismiss()
    {
    }
}
