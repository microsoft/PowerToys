// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
namespace PowerDisplay.Contracts;

public sealed class GetRequest
{
    public int? MonitorNumber { get; set; }

    public string? MonitorId { get; set; }

    public string? SettingFilter { get; set; }
}
