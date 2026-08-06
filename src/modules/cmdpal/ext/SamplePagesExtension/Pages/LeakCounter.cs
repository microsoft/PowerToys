// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading;

namespace SamplePagesExtension.Pages;

/// <summary>
/// Live and cumulative counts for one category of tracked object.
/// </summary>
/// <remarks>
/// <see cref="Alive"/> is its own counter rather than <c>Created - Released</c>.
/// Deriving it was a bug: <see cref="ResetTotals"/> zeroed the totals while
/// finalizers for earlier objects were still in flight, and their releases then
/// decremented against zero, driving the live count negative.
/// </remarks>
internal sealed class LeakCounter(string name)
{
    private int _alive;
    private int _created;
    private int _released;
    private long _bytesAlive;

    public string Name { get; } = name;

    public int Alive => Volatile.Read(ref _alive);

    public int Created => Volatile.Read(ref _created);

    public int Released => Volatile.Read(ref _released);

    public long BytesAlive => Volatile.Read(ref _bytesAlive);

    public void OnCreated(long bytes = 0)
    {
        Interlocked.Increment(ref _alive);
        Interlocked.Increment(ref _created);

        if (bytes != 0)
        {
            Interlocked.Add(ref _bytesAlive, bytes);
        }
    }

    public void OnReleased(long bytes = 0)
    {
        Interlocked.Decrement(ref _alive);
        Interlocked.Increment(ref _released);

        if (bytes != 0)
        {
            Interlocked.Add(ref _bytesAlive, -bytes);
        }
    }

    /// <summary>
    /// Clears the cumulative tallies only. The live count is deliberately left
    /// alone - objects created before the reset can still be in flight.
    /// </summary>
    public void ResetTotals()
    {
        Interlocked.Exchange(ref _created, 0);
        Interlocked.Exchange(ref _released, 0);
    }

    public override string ToString() => $"{Name}: {Alive:N0} alive of {Created:N0}";
}
