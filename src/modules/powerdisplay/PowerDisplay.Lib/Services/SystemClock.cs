// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma warning disable SA1649 // File name should match first type name

using System;

namespace PowerDisplay.Common.Services;

/// <summary>
/// Abstraction over <see cref="DateTime.UtcNow"/> so that date-sensitive logic
/// (retention windows, last-seen stamps) can be unit-tested without wall-clock dependence.
/// </summary>
public interface ISystemClock
{
    DateTime UtcNow { get; }
}

/// <summary>
/// Production <see cref="ISystemClock"/> implementation.
/// </summary>
public sealed class SystemClock : ISystemClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
