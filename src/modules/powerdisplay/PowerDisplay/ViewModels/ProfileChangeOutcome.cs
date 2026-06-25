// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using PowerDisplay.Contracts;

namespace PowerDisplay.ViewModels;

/// <summary>
/// Outcome for a single setting within a <see cref="ProfileApplyOutcome"/>. Carries the same
/// fields as <see cref="PowerDisplay.Contracts.CliProfileChange"/> so
/// <c>ProfileDtoProjector.BuildApplyProfileResult</c> can populate every field of the DTO
/// without re-running hardware operations.
/// </summary>
/// <param name="Setting">Canonical setting name (e.g. <c>brightness</c>, <c>color-temperature</c>).</param>
/// <param name="Value">
/// The raw integer value from the profile (percentage for continuous settings;
/// VCP byte for color-temperature). Always populated, regardless of status.
/// </param>
/// <param name="Display">
/// Human-readable applied value (e.g. <c>"50%"</c>, <c>"6500K (0x05)"</c>).
/// <c>null</c> unless <see cref="Status"/> is <c>applied</c>.
/// </param>
/// <param name="Status">
/// One of <see cref="PowerDisplay.Contracts.CliProfileChange.StatusApplied"/>,
/// <see cref="PowerDisplay.Contracts.CliProfileChange.StatusUnsupported"/>,
/// <see cref="PowerDisplay.Contracts.CliProfileChange.StatusOutOfRange"/>,
/// <see cref="PowerDisplay.Contracts.CliProfileChange.StatusHardwareFailure"/>.
/// </param>
/// <param name="Error">
/// Hardware error message from <c>MonitorOperationResult.ErrorMessage</c>.
/// <c>null</c> unless <see cref="Status"/> is <c>hardware-failure</c>.
/// </param>
public readonly record struct ProfileChangeOutcome(
    string Setting,
    int Value,
    string? Display,
    string Status,
    string? Error);
