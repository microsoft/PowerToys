// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading.Tasks;
using Microsoft.CmdPal.UI.Utilities;

namespace Microsoft.CmdPal.Common.UnitTests.Helpers;

[TestClass]
public class TaskbarMetricsTests
{
    [TestMethod]
    public async Task DisposeAllowsAnAcceptedMeasurementToFinish()
    {
        await using var metrics = new TaskbarMetrics();
        var measurement = metrics.UpdateAsync();

        metrics.Dispose();

        await measurement.WaitAsync(TimeSpan.FromSeconds(30));
        await metrics.DisposeAsync();
        Assert.ThrowsExactly<ObjectDisposedException>(() => metrics.UpdateAsync());
    }

    [TestMethod]
    public async Task DisposeWithoutMeasurementIsIdempotent()
    {
        var metrics = new TaskbarMetrics();
        metrics.Dispose();
        await metrics.DisposeAsync();
        await metrics.DisposeAsync();
        Assert.ThrowsExactly<ObjectDisposedException>(() => metrics.UpdateAsync());
    }
}
