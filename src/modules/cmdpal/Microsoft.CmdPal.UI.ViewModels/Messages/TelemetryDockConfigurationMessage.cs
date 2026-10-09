// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.ViewModels.Messages;

/// <summary>
/// Telemetry message sent when the dock is initialized.
/// Captures the dock configuration for telemetry tracking.
/// </summary>
public record TelemetryDockConfigurationMessage(bool IsDockEnabled, string DockSide, string StartBands, string CenterBands, string EndBands);
