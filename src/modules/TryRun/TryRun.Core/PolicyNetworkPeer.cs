// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerToys.TryRun.Core;

public sealed class PolicyNetworkPeer
{
    public string Cidr { get; set; } = "0.0.0.0/0";

    public string[] Except { get; set; } = [];
}
