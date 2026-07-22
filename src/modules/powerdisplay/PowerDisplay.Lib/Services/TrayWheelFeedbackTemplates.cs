// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerDisplay.Common.Services;

/// <summary>
/// Localized templates used to format tray wheel feedback.
/// </summary>
public sealed record TrayWheelFeedbackTemplates(
    string? PrimaryFormat,
    string? PrimaryPluralFormat,
    string? AllFormat,
    string? PercentageFormat,
    string? RangeFormat,
    string? ListSeparator);
