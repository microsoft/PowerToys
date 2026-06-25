// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
namespace PowerDisplay.Contracts;

public sealed class CapabilitiesRequest
{
    public int? MonitorNumber { get; set; }

    public string? MonitorId { get; set; }

    /// <summary>
    /// Optional filter restricting the result to a single discrete setting's VCP code
    /// (<c>color-temperature</c>, <c>input-source</c>, or <c>power-state</c>). Null = all codes.
    /// </summary>
    public string? SettingFilter { get; set; }
}
