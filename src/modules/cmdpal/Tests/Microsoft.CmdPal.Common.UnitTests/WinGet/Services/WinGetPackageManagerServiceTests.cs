// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading.Tasks;
using Microsoft.CmdPal.Common.WinGet.Services;

namespace Microsoft.CmdPal.Common.UnitTests.WinGet.Services;

[TestClass]
public class WinGetPackageManagerServiceTests
{
    [TestMethod]
    public async Task SearchPackagesAsync_ReturnsUnavailableResult_WhenFactoryIsUnavailable()
    {
        var service = new WinGetPackageManagerService(() => null);

        var result = await service.SearchPackagesAsync("PowerToys");

        Assert.IsFalse(service.State.IsAvailable);
        Assert.IsTrue(result.IsUnavailable);
        Assert.IsFalse(result.IsSuccess);
        Assert.IsNull(result.Value);
        Assert.AreEqual(service.State.Message, result.ErrorMessage);
    }

    [TestMethod]
    public async Task SearchCommandPaletteExtensionsAsync_ReturnsUnavailableResult_WhenFactoryIsUnavailable()
    {
        var service = new WinGetPackageManagerService(() => null);

        var result = await service.SearchCommandPaletteExtensionsAsync();

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

        var refreshed = await service.RefreshCatalogsAsync();

        Assert.IsFalse(refreshed);
        Assert.IsFalse(service.State.IsAvailable);
    }
}
