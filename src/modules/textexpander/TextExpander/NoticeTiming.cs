// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.TextExpander;

/// <summary>When the notice fades. Pure, so tests cover it without a window or a clock.</summary>
internal static class NoticeTiming
{
    /// <summary>How long a notice stays up when nothing happens.</summary>
    public const int LifetimeMs = 4_000;

    /// <summary>
    /// How long a notice is up, at least, before a key or click fades it, so a key typed just as it appears doesn't
    /// flash it away unread.
    /// </summary>
    public const int MinimumVisibleMs = 1_000;

    public const int FadeInMs = 100;

    public const int FadeOutMs = 250;

    public const int FrameMs = 15;

    /// <summary>
    /// How long after a notice shows screen readers are told about it. They connect to the window as it appears, and
    /// what it raises before any has connected to a new window reaches none, so its first notice went unannounced.
    /// Well within <see cref="MinimumVisibleMs"/>, so it's told while the notice is up.
    /// </summary>
    public const int AnnounceDelayMs = 250;

    // Windows' own "Dismiss notifications after this amount of time", unless someone made it longer.
    private const uint DefaultMessageDurationSeconds = 5;

    /// <summary>
    /// Gets how long a notice stays up when nothing happens: about 4 seconds, or as long as Windows keeps its own
    /// notifications up when someone made that longer (Accessibility &gt; Visual effects).
    /// </summary>
    public static int Lifetime(uint messageDurationSeconds)
        => messageDurationSeconds > DefaultMessageDurationSeconds ? (int)Math.Min(messageDurationSeconds, 300) * 1_000 : LifetimeMs;

    /// <summary>Gets how long after a key or click the notice starts to fade: at once, unless it has only just appeared.</summary>
    public static int FadeDelay(long shownAtMs, long nowMs) => (int)Math.Clamp(MinimumVisibleMs - (nowMs - shownAtMs), 0, MinimumVisibleMs);

    /// <summary>Gets the window's opacity <paramref name="elapsedMs"/> into a fade from <paramref name="from"/> to <paramref name="to"/>.</summary>
    public static byte Opacity(long elapsedMs, int durationMs, byte from, byte to)
    {
        if (durationMs <= 0 || elapsedMs >= durationMs)
        {
            return to;
        }

        return elapsedMs <= 0 ? from : (byte)Math.Round(from + ((to - from) * (double)elapsedMs / durationMs));
    }
}
