// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Awake.UnitTests;

internal sealed class FakeTimeProvider : TimeProvider
{
    private readonly TimeZoneInfo _localTimeZone;
    private readonly TimeSpan _step;
    private DateTimeOffset _utcNow;

    public FakeTimeProvider(DateTimeOffset initialUtcNow, TimeZoneInfo localTimeZone, TimeSpan? step = null)
    {
        ArgumentNullException.ThrowIfNull(localTimeZone);

        _utcNow = initialUtcNow.ToUniversalTime();
        _localTimeZone = localTimeZone;
        _step = step ?? TimeSpan.Zero;
    }

    public override TimeZoneInfo LocalTimeZone => _localTimeZone;

    public int UtcNowCallCount { get; private set; }

    public override DateTimeOffset GetUtcNow()
    {
        UtcNowCallCount++;
        DateTimeOffset current = _utcNow;
        _utcNow = _utcNow.Add(_step);
        return current;
    }
}
