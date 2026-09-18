// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using Microsoft.CmdPal.Common.Services;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CommandPalette.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using WinRT;

namespace Microsoft.CmdPal.UI.UnitTests;

[TestClass]
public class PowerToysRootPageServiceTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void OnPerformCommand_ChecksLivenessOnlyWhenExtensionChanges(bool topLevel)
    {
        var extension = new Mock<IExtensionWrapper>(MockBehavior.Strict);
        extension.Setup(static e => e.GetExtensionObject()).Returns((IExtension?)null);
        extension.Setup(static e => e.GetCachedExtensionObject()).Returns((IExtension?)null);
        var host = new CommandPaletteHost(extension.Object);
        var service = CreateService();

        service.OnPerformCommand(null, topLevel, host);
        service.OnPerformCommand(null, topLevel, host);

        extension.Verify(static e => e.GetExtensionObject(), Times.Once);
        extension.Verify(static e => e.GetCachedExtensionObject(), Times.Once);
        extension.VerifyNoOtherCalls();
    }

    [TestMethod]
    public void OnPerformCommand_RechecksLivenessWhenSwitchingBack()
    {
        var firstExtension = new Mock<IExtensionWrapper>(MockBehavior.Strict);
        firstExtension.Setup(static e => e.GetExtensionObject()).Returns((IExtension?)null);
        firstExtension.Setup(static e => e.GetCachedExtensionObject()).Returns((IExtension?)null);
        var secondExtension = new Mock<IExtensionWrapper>(MockBehavior.Strict);
        secondExtension.Setup(static e => e.GetExtensionObject()).Returns((IExtension?)null);
        secondExtension.Setup(static e => e.GetCachedExtensionObject()).Returns((IExtension?)null);
        var firstHost = new CommandPaletteHost(firstExtension.Object);
        var secondHost = new CommandPaletteHost(secondExtension.Object);
        var service = CreateService();

        service.OnPerformCommand(null, false, firstHost);
        service.OnPerformCommand(null, false, firstHost);
        service.OnPerformCommand(null, false, secondHost);
        service.OnPerformCommand(null, false, secondHost);
        service.OnPerformCommand(null, false, firstHost);

        firstExtension.Verify(static e => e.GetExtensionObject(), Times.Exactly(2));
        firstExtension.Verify(static e => e.GetCachedExtensionObject(), Times.Once);
        firstExtension.VerifyNoOtherCalls();
        secondExtension.Verify(static e => e.GetExtensionObject(), Times.Once);
        secondExtension.Verify(static e => e.GetCachedExtensionObject(), Times.Once);
        secondExtension.VerifyNoOtherCalls();
    }

    [TestMethod]
    public void OnPerformCommand_UsesCachedExtensionAfterLivenessCheckFails()
    {
        var extension = new Mock<IExtensionWrapper>(MockBehavior.Strict);
        extension.Setup(static e => e.GetExtensionObject())
            .Throws(Marshal.GetExceptionForHR(unchecked((int)0x80010108))!);
        extension.Setup(static e => e.GetCachedExtensionObject()).Returns((IExtension?)null);
        var host = new CommandPaletteHost(extension.Object);
        var service = CreateService();

        service.OnPerformCommand(null, false, host);
        service.OnPerformCommand(null, false, host);

        extension.Verify(static e => e.GetExtensionObject(), Times.Once);
        extension.Verify(static e => e.GetCachedExtensionObject(), Times.Once);
        extension.VerifyNoOtherCalls();
    }

    [TestMethod]
    public void OnPerformCommand_RetriesAfterCachedExtensionLookupFails()
    {
        var extension = new Mock<IExtensionWrapper>(MockBehavior.Strict);
        extension.Setup(static e => e.GetExtensionObject()).Returns((IExtension?)null);
        extension.SetupSequence(static e => e.GetCachedExtensionObject())
            .Throws(Marshal.GetExceptionForHR(unchecked((int)0x80010108))!)
            .Returns((IExtension?)null);
        var host = new CommandPaletteHost(extension.Object);
        var service = CreateService();

        service.OnPerformCommand(null, false, host);
        service.OnPerformCommand(null, false, host);
        service.OnPerformCommand(null, false, host);

        extension.Verify(static e => e.GetExtensionObject(), Times.Once);
        extension.Verify(static e => e.GetCachedExtensionObject(), Times.Exactly(2));
        extension.VerifyNoOtherCalls();
    }

    [TestMethod]
    public void OnPerformCommand_RetriesAfterForegroundGrantPreparationFails()
    {
        var extensionObject = new Mock<IExtension>(MockBehavior.Strict);
        var winRtObject = extensionObject.As<IWinRTObject>();
        winRtObject.SetupGet(static e => e.NativeObject)
            .Throws(Marshal.GetExceptionForHR(unchecked((int)0x80010108))!);
        var extension = new Mock<IExtensionWrapper>(MockBehavior.Strict);
        extension.Setup(static e => e.GetExtensionObject()).Returns(extensionObject.Object);
        extension.Setup(static e => e.GetCachedExtensionObject()).Returns(extensionObject.Object);
        var host = new CommandPaletteHost(extension.Object);
        var service = CreateService();

        service.OnPerformCommand(null, false, host);
        service.OnPerformCommand(null, false, host);

        winRtObject.VerifyGet(static e => e.NativeObject, Times.Exactly(2));
        extension.Verify(static e => e.GetExtensionObject(), Times.Once);
        extension.Verify(static e => e.GetCachedExtensionObject(), Times.Once);
        extension.VerifyNoOtherCalls();
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void OnPerformCommand_RechecksLivenessAfterReturningToRoot(bool builtInCommand)
    {
        var extension = new Mock<IExtensionWrapper>(MockBehavior.Strict);
        extension.Setup(static e => e.GetExtensionObject()).Returns((IExtension?)null);
        var host = new CommandPaletteHost(extension.Object);
        var service = CreateService();

        service.OnPerformCommand(null, false, host);
        if (builtInCommand)
        {
            service.OnPerformCommand(null, true, CommandPaletteHost.Instance);
        }
        else
        {
            service.GoHome();
            service.GoHome();
        }

        extension.Verify(static e => e.GetExtensionObject(), Times.Once);
        extension.VerifyNoOtherCalls();

        service.OnPerformCommand(null, false, host);

        extension.Verify(static e => e.GetExtensionObject(), Times.Exactly(2));
        extension.VerifyNoOtherCalls();
    }

    private static PowerToysRootPageService CreateService() => new(null!, null!, null!, null!, null!);
}
