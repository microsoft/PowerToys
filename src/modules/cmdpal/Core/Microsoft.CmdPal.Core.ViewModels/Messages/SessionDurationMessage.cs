// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.Core.ViewModels.Messages;

/// <summary>
/// Message containing session telemetry data from Command Palette launch to dismissal.
/// Used to aggregate metrics like duration, commands executed, pages visited, and search activity.
/// </summary>
public record SessionDurationMessage(ulong DurationMs, int CommandsExecuted, int PagesVisited, string DismissalReason, int SearchQueriesCount, int MaxNavigationDepth, int ErrorCount);
