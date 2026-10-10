// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.CmdPal.Ext.TimeDate.Helpers;

/// <summary>
/// The result of a date calculation: either a single point in time, or the span between two dates.
/// </summary>
internal sealed class DateCalculationResult
{
    internal DateCalculationResult(DateTime timestamp)
    {
        Timestamp = timestamp;
    }

    internal DateCalculationResult(DateTime from, DateTime to)
    {
        From = from;
        To = to;
    }

    /// <summary>
    /// Gets the calculated date for "date ± duration" input.
    /// </summary>
    internal DateTime? Timestamp { get; }

    /// <summary>
    /// Gets the date that is subtracted in a "to - from" difference.
    /// </summary>
    internal DateTime? From { get; }

    /// <summary>
    /// Gets the date that is subtracted from in a "to - from" difference.
    /// </summary>
    internal DateTime? To { get; }

    internal bool IsDifference => From is not null && To is not null;
}
