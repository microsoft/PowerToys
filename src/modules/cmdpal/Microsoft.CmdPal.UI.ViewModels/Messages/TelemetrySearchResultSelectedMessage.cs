// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.ViewModels.MainPage;

namespace Microsoft.CmdPal.UI.ViewModels.Messages;

/// <summary>
/// Telemetry message sent when the user selects (invokes) a main-page search result.
/// Carries only non-identifying aggregates - never the selected item's title/id or the raw
/// query text. <see cref="QueryLength"/> is the query's character count, <see cref="SelectedIndex"/>
/// is the zero-based rank of the selected result, and <see cref="SelectedTier"/> is its ranker tier.
/// </summary>
public record TelemetrySearchResultSelectedMessage(int QueryLength, int SelectedIndex, RankTier SelectedTier);
