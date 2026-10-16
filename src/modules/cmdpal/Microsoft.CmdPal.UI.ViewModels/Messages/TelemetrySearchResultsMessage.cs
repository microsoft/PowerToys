// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.ViewModels.Messages;

/// <summary>
/// Telemetry message sent when a main-page search query settles.
/// Carries only non-identifying aggregates - never the raw query text. <see cref="QueryLength"/>
/// is the query's character count.
/// </summary>
public record TelemetrySearchResultsMessage(int QueryLength, int ResultCount, bool NoResults, ulong LatencyMs);
