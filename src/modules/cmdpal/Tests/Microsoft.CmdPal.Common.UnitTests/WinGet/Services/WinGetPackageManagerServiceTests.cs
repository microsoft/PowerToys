// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading.Tasks;
using Microsoft.CmdPal.Common.WinGet.Services;

namespace Microsoft.CmdPal.Common.UnitTests.WinGet.Services;

[Microsoft.VisualStudio.TestTools.UnitTesting.OSCondition(Microsoft.VisualStudio.TestTools.UnitTesting.OperatingSystems.Windows)]
[TestClass]
public class WinGetPackageManagerServiceTests
{
    [TestMethod]
    public async Task SearchPackagesAsync_ReturnsUnavailableResult_WhenFactoryIsUnavailable()
    {
        var service = new WinGetPackageManagerService(() => null);

        var result = await service.SearchPackagesAsync("PowerToys", cancellationToken: TestContext.CancellationToken);

        Assert.IsFalse(service.State.IsAvailable);
        Assert.IsTrue(result.IsUnavailable);
        Assert.IsFalse(result.IsSuccess);
        Assert.IsNull(result.Value);
        Assert.AreEqual(service.State.Message, result.ErrorMessage);
    }

    [TestMethod]
    public async Task RefreshCatalogsAsync_ReturnsFalse_WhenFactoryIsUnavailable()
    {
        var service = new WinGetPackageManagerService(() => null);

        var refreshed = await service.RefreshCatalogsAsync(TestContext.CancellationToken);

        Assert.IsFalse(refreshed);
        Assert.IsFalse(service.State.IsAvailable);
    }

    public TestContext TestContext { get; set; } = null!;
}
