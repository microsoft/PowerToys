// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using Microsoft.CmdPal.Common.Services;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CommandPalette.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Microsoft.CmdPal.UI.UnitTests;

[TestClass]
public class PowerToysRootPageServiceTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void OnPerformCommand_LooksUpSameExtensionForEveryCommand(bool topLevel)
    {
        var extension = new Mock<IExtensionWrapper>(MockBehavior.Strict);
        extension.Setup(static e => e.GetExtensionObject()).Returns((IExtension?)null);
        var host = new CommandPaletteHost(extension.Object);
        var service = CreateService();

        service.OnPerformCommand(null, topLevel, host);
        service.OnPerformCommand(null, topLevel, host);

        extension.Verify(static e => e.GetExtensionObject(), Times.Exactly(2));
    }

    [TestMethod]
    public void OnPerformCommand_RetriesAfterExtensionLookupFails()
    {
        var extension = new Mock<IExtensionWrapper>(MockBehavior.Strict);
        extension.SetupSequence(static e => e.GetExtensionObject())
            .Throws(Marshal.GetExceptionForHR(unchecked((int)0x80010108))!)
            .Returns((IExtension?)null);
        var host = new CommandPaletteHost(extension.Object);
        var service = CreateService();

        service.OnPerformCommand(null, false, host);
        service.OnPerformCommand(null, false, host);

        extension.Verify(static e => e.GetExtensionObject(), Times.Exactly(2));
    }

    [TestMethod]
    public void OnPerformCommand_RetriesAfterForegroundGrantPreparationFails()
    {
        var extension = new Mock<IExtensionWrapper>(MockBehavior.Strict);
        extension.Setup(static e => e.GetExtensionObject()).Returns(Mock.Of<IExtension>());
        var host = new CommandPaletteHost(extension.Object);
        var service = CreateService();

        service.OnPerformCommand(null, false, host);
        service.OnPerformCommand(null, false, host);

        extension.Verify(static e => e.GetExtensionObject(), Times.Exactly(2));
    }

    [TestMethod]
    public void GoHome_AndBuiltInCommandsDoNotLookUpPreviousExtension()
    {
        var extension = new Mock<IExtensionWrapper>(MockBehavior.Strict);
        extension.Setup(static e => e.GetExtensionObject()).Returns((IExtension?)null);
        var host = new CommandPaletteHost(extension.Object);
        var service = CreateService();

        service.OnPerformCommand(null, false, host);
        service.GoHome();
        service.GoHome();
        service.OnPerformCommand(null, true, CommandPaletteHost.Instance);

        extension.Verify(static e => e.GetExtensionObject(), Times.Once);

        service.OnPerformCommand(null, false, host);

        extension.Verify(static e => e.GetExtensionObject(), Times.Exactly(2));
    }

    private static PowerToysRootPageService CreateService() => new(null!, null!, null!, null!, null!);
}
