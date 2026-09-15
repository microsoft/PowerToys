// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerToys.TryRun.Core;

public sealed class PolicyNetworkPort
{
    public string Protocol { get; set; } = "Tcp";

    public ushort? Port { get; set; }

    public ushort? EndPort { get; set; }
}
