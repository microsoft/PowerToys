// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
namespace PowerDisplay.Contracts;

/// <summary>
/// Payload for <c>powerdisplay capabilities</c>. See <see cref="MonitorSelectorRequest"/>; the
/// <see cref="MonitorSelectorRequest.SettingFilter"/> restricts the result to a single discrete
/// setting's VCP code (<c>color-temperature</c>, <c>input-source</c>, or <c>power-state</c>).
/// </summary>
public sealed class CapabilitiesRequest : MonitorSelectorRequest
{
}
