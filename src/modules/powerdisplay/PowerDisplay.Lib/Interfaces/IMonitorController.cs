// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerDisplay.Common.Interfaces
{
    /// <summary>
    /// Aggregate monitor controller contract that all callers reference. It is the
    /// union of the basic (<see cref="IBasicMonitorController"/>) and DDC/CI
    /// (<see cref="IDdcController"/>) surfaces. Concrete controllers always implement
    /// this interface; the basic methods are required, while the DDC methods carry
    /// default "unsupported" implementations on <see cref="IDdcController"/> that
    /// non-DDC controllers (e.g. WMI) simply inherit.
    /// </summary>
    public interface IMonitorController : IBasicMonitorController, IDdcController
    {
    }
}
