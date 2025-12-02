// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.Core.ViewModels.Messages;

/// <summary>
/// Message sent when an error occurs during command execution.
/// Used to track session error count for telemetry.
/// </summary>
public record ErrorOccurredMessage();
