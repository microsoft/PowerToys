// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public class NpmJsExtensionInstallerTests
{
    private const string Root = @"C:\Users\sample\AppData\Local\Microsoft\PowerToys\CommandPalette\JSExtensions";

    private static readonly string[] ExpectedUninstallOrder = ["stop", "remove"];

    [TestMethod]
    public async Task InstallAsync_RunsNpm_IntoResolvedTargetDirectory()
    {
        var host = CreateHost();
        var runner = new Mock<INpmCommandRunner>();
        runner.Setup(x => x.IsNpmAvailable()).Returns(true);
        runner
            .Setup(x => x.InstallAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(NpmCommandResult.Ok());

        var installer = new NpmJsExtensionInstaller(host.Object, runner.Object);

        var result = await installer.InstallAsync("sample-ext", "@contoso/sample", "https://registry.example.com", CancellationToken.None);

        var expectedDirectory = Path.Combine(Root, "sample-ext");
        Assert.IsTrue(result.Succeeded);
        runner.Verify(
            x => x.InstallAsync(expectedDirectory, "@contoso/sample", "https://registry.example.com", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task InstallAsync_Fails_WhenNpmNotAvailable()
    {
        var host = CreateHost();
        var runner = new Mock<INpmCommandRunner>();
        runner.Setup(x => x.IsNpmAvailable()).Returns(false);

        var installer = new NpmJsExtensionInstaller(host.Object, runner.Object);

        var result = await installer.InstallAsync("sample-ext", "@contoso/sample", null, CancellationToken.None);

        Assert.IsFalse(result.Succeeded);
        Assert.IsNotNull(result.ErrorMessage);
        runner.Verify(
            x => x.InstallAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task InstallAsync_RemovesDirectory_OnNpmFailure()
    {
        var host = CreateHost();
        var runner = new Mock<INpmCommandRunner>();
        runner.Setup(x => x.IsNpmAvailable()).Returns(true);
        runner
            .Setup(x => x.InstallAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(NpmCommandResult.Fail("boom"));

        var installer = new NpmJsExtensionInstaller(host.Object, runner.Object);

        var result = await installer.InstallAsync("sample-ext", "@contoso/sample", null, CancellationToken.None);

        var expectedDirectory = Path.Combine(Root, "sample-ext");
        Assert.IsFalse(result.Succeeded);
        Assert.AreEqual("boom", result.ErrorMessage);
        runner.Verify(x => x.RemoveDirectory(expectedDirectory), Times.Once);
    }

    [TestMethod]
    public async Task InstallAsync_Fails_ForPathTraversalName()
    {
        var host = CreateHost();
        var runner = new Mock<INpmCommandRunner>();

        var installer = new NpmJsExtensionInstaller(host.Object, runner.Object);

        var result = await installer.InstallAsync("..\\escape", "@contoso/sample", null, CancellationToken.None);

        Assert.IsFalse(result.Succeeded);
        runner.Verify(x => x.IsNpmAvailable(), Times.Never);
    }

    [TestMethod]
    public async Task UninstallAsync_StopsExtension_BeforeRemovingDirectory()
    {
        var order = new List<string>();
        var host = CreateHost();
        host
            .Setup(x => x.StopExtension(It.IsAny<string>()))
            .Callback(() => order.Add("stop"));
        var runner = new Mock<INpmCommandRunner>();
        runner
            .Setup(x => x.RemoveDirectory(It.IsAny<string>()))
            .Callback(() => order.Add("remove"));

        var installer = new NpmJsExtensionInstaller(host.Object, runner.Object);

        var result = await installer.UninstallAsync("sample-ext", CancellationToken.None);

        var expectedDirectory = Path.Combine(Root, "sample-ext");
        Assert.IsTrue(result.Succeeded);
        CollectionAssert.AreEqual(ExpectedUninstallOrder, order);
        host.Verify(x => x.StopExtension(expectedDirectory), Times.Once);
        runner.Verify(x => x.RemoveDirectory(expectedDirectory), Times.Once);
    }

    private static Mock<IJsExtensionHost> CreateHost()
    {
        var host = new Mock<IJsExtensionHost>();
        host.SetupGet(x => x.ExtensionsRootPath).Returns(Root);
        return host;
    }
}
