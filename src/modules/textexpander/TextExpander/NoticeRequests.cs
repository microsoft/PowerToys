// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.TextExpander;

/// <summary>
/// The notice asked for last. Each Show and Dismiss is numbered in the order it took effect, so the notice's thread
/// acts on the latest, whatever order their messages reach it in, and can tell when one it's preparing was overtaken.
/// Thread-safe and lock-free.
/// </summary>
internal sealed class NoticeRequests
{
    private Ask _latest = new(0, null);

    /// <summary>Gets the latest ask: a notice, or none after a dismiss. Generation 0 is before any.</summary>
    public Ask Latest => Volatile.Read(ref _latest);

    /// <summary>Asks for <paramref name="notice"/>, or for none, after everything asked so far.</summary>
    /// <returns>The ask's generation.</returns>
    public long Set(Notice? notice)
    {
        while (true)
        {
            Ask current = Volatile.Read(ref _latest);
            var next = new Ask(current.Generation + 1, notice);
            if (Interlocked.CompareExchange(ref _latest, next, current) == current)
            {
                return next.Generation;
            }
        }
    }

    /// <summary>Gets a value indicating whether something was asked after <paramref name="generation"/>.</summary>
    public bool Overtaken(long generation) => Latest.Generation != generation;

    /// <summary>A notice for a window.</summary>
    internal sealed record Notice(IntPtr Target, string Heading, string Message);

    /// <summary>A notice asked for, or none, and its place in the order.</summary>
    internal sealed record Ask(long Generation, Notice? Notice);
}
