// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
namespace PowerDisplay.Contracts;

/// <summary>
/// Shared selector shape for the read commands that target a single monitor and optionally a single
/// setting (<c>get</c>, <c>capabilities</c>). Exactly one of <see cref="MonitorNumber"/> /
/// <see cref="MonitorId"/> identifies the monitor; <see cref="SettingFilter"/> optionally narrows
/// the result to one setting. Concrete subclasses keep the envelope's payload slots distinct types.
/// </summary>
public abstract class MonitorSelectorRequest
{
    public int? MonitorNumber { get; set; }

    public string? MonitorId { get; set; }

    /// <summary>
    /// Optional filter restricting the result to a single setting (e.g. a discrete setting's VCP
    /// code for <c>capabilities</c>). Null = no filter.
    /// </summary>
    public string? SettingFilter { get; set; }
}
