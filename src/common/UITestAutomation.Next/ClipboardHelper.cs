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

    private static T? RunSTA<T>(Func<T> body, int maxAttempts = 10, int retryDelayMS = 100)
    {
        T? result = default;
        try
        {
            var thread = new Thread(() =>
            {
                for (var attempt = 1; attempt <= maxAttempts; attempt++)
                {
                    try
                    {
                        result = body();
                        return;
                    }
                    catch when (attempt < maxAttempts)
                    {
                        // The clipboard is a single shared resource: OpenClipboard fails transiently
                        // while another process still holds it open — very common right after an app
                        // writes data (e.g. the Measure Tool committing a measurement on click, which
                        // itself bails silently if OpenClipboard fails). A single-shot attempt surfaces
                        // that as a false empty/failure, so wait a beat and retry instead of giving up.
                        Console.WriteLine($"[clipboard] operation blocked (clipboard locked); retry {attempt}/{maxAttempts}");
                        Thread.Sleep(retryDelayMS);
                    }
                    catch
                    {
                        // Final attempt also failed — leave result at its default (null/false/empty).
                    }
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
