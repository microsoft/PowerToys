// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.CmdPal.Ext.TimeDate;

internal sealed record CustomClock
{
    internal const string CurrentTimeZoneId = "current";

    public Guid Id { get; init; } = Guid.NewGuid();

    public string Title { get; init; } = string.Empty;

    // Windows time zone ID, for example "Pacific Standard Time".
    public string TimeZoneId { get; init; } = CurrentTimeZoneId;

    public string TitleFormat { get; init; } = "t";

    public string SubtitleFormat { get; init; } = "REL";

    public string CopyFormat { get; init; } = string.Empty;
}
