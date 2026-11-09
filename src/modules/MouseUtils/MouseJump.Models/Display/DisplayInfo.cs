// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;

namespace MouseJump.Models.Display;

public sealed record DisplayInfo
{
    public DisplayInfo(IEnumerable<DeviceInfo> devices)
    {
        this.Devices = (devices ?? throw new ArgumentNullException(nameof(devices))).ToList().AsReadOnly();
    }

    public ReadOnlyCollection<DeviceInfo> Devices
    {
        get;
    }
}
