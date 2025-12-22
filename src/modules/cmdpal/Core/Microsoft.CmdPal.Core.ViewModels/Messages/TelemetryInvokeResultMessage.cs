// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.Core.ViewModels.Messages;

/// <summary>
/// Telemetry message sent when command invocation completes with a result.
/// </summary>
public record TelemetryInvokeResultMessage(Microsoft.CommandPalette.Extensions.CommandResultKind Kind);
