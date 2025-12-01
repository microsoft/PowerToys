// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.Core.ViewModels.Messages;

/// <summary>
/// Message sent when an extension command or page is invoked.
/// Captures extension usage metrics for telemetry tracking.
/// </summary>
public record ExtensionInvokedMessage(string ExtensionId, string CommandId, string CommandName, bool Success, ulong ExecutionTimeMs);
