// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using FormsClipboard = System.Windows.Forms.Clipboard;

namespace Microsoft.PowerToys.UITest.Next;

/// <summary>
/// Clipboard helpers that always execute on an STA thread (<see cref="FormsClipboard"/>
/// requires it). Tolerant — every method swallows clipboard errors and returns a default,
/// so callers can use them in test <c>finally</c> blocks without worrying about masking
/// the real failure.
/// </summary>
public static class ClipboardHelper
{
    /// <summary>Return the current clipboard text, or <see cref="string.Empty"/> if none / on error.</summary>
    public static string GetText() => RunSTA(() => FormsClipboard.ContainsText() ? FormsClipboard.GetText() : string.Empty) ?? string.Empty;

    /// <summary>Clear the clipboard. Returns true on success, false on error.</summary>
    public static bool Clear() => RunSTA(() => { FormsClipboard.Clear(); return true; });

    /// <summary>Set the clipboard text. Returns true on success, false on error.</summary>
    public static bool SetText(string value) => RunSTA(() => { FormsClipboard.SetText(value); return true; });

    /// <summary>
    /// Poll the clipboard up to <paramref name="timeoutMS"/> for the first non-empty text
    /// different from <paramref name="ignoredValue"/>. Returns <see cref="string.Empty"/> on
    /// timeout. Use when you've just cleared the clipboard and are waiting for an external
    /// app (e.g. ColorPicker on click) to write into it.
    /// </summary>
    public static string WaitForText(string ignoredValue = "", int timeoutMS = 3_000, int pollIntervalMS = 100)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromMilliseconds(timeoutMS);
        while (DateTime.UtcNow < deadline)
        {
            var text = GetText();
            if (!string.IsNullOrEmpty(text) && text != ignoredValue)
            {
                return text;
            }

            Thread.Sleep(pollIntervalMS);
        }

        return string.Empty;
    }

    private static T? RunSTA<T>(Func<T> body)
    {
        T? result = default;
        try
        {
            var thread = new Thread(() =>
            {
                try
                {
                    result = body();
                }
                catch
                {
                    // Best effort — clipboard can throw under contention (OpenClipboard failures).
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join(TimeSpan.FromSeconds(5));
        }
        catch
        {
        }

        return result;
    }
}
