// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Common.Services;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Microsoft.CmdPal.UI.UnitTests;

[TestClass]
public class PowerToysRootPageServiceTests
{
    [TestMethod]
    [DataRow(false, true)]
    [DataRow(true, true)]
    [DataRow(false, false)]
    [DataRow(true, false)]
    public void OnPerformCommand_RequestsForegroundGrantForEveryCommand(bool topLevel, bool grantSucceeds)
    {
        var grantRequests = new List<bool>();
        var host = new CommandPaletteHost(CreateExtension(grantRequests, grantSucceeds));
        var service = CreateService();

        service.OnPerformCommand(null, topLevel, host);
        service.OnPerformCommand(null, topLevel, host);
        service.OnPerformCommand(null, topLevel, host);

        bool[] expectedGrantRequests = [true, false, false];
        CollectionAssert.AreEqual(expectedGrantRequests, grantRequests);
    }

    [TestMethod]
    public void OnPerformCommand_RequestsForegroundGrantForTheCommandOwner()
    {
        var firstGrantRequests = new List<bool>();
        var secondGrantRequests = new List<bool>();
        var firstHost = new CommandPaletteHost(CreateExtension(firstGrantRequests));
        var secondHost = new CommandPaletteHost(CreateExtension(secondGrantRequests));
        var service = CreateService();

        service.OnPerformCommand(null, false, firstHost);
        service.OnPerformCommand(null, false, firstHost);
        service.OnPerformCommand(null, false, secondHost);
        service.OnPerformCommand(null, false, secondHost);
        service.OnPerformCommand(null, false, firstHost);

        bool[] expectedFirstGrantRequests = [true, false, true];
        bool[] expectedSecondGrantRequests = [true, false];
        CollectionAssert.AreEqual(expectedFirstGrantRequests, firstGrantRequests);
        CollectionAssert.AreEqual(expectedSecondGrantRequests, secondGrantRequests);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void GoHome_AndBuiltInCommandsDoNotGrantForegroundToPreviousExtension(bool builtInCommand)
    {
        var grantRequests = new List<bool>();
        var host = new CommandPaletteHost(CreateExtension(grantRequests));
        var service = CreateService();

        service.GoHome();
        service.OnPerformCommand(null, true, CommandPaletteHost.Instance);
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

        bool[] expectedBeforeReturning = [true];
        CollectionAssert.AreEqual(expectedBeforeReturning, grantRequests);

        service.OnPerformCommand(null, false, host);

        bool[] expectedAfterReturning = [true, true];
        CollectionAssert.AreEqual(expectedAfterReturning, grantRequests);
    }

    private static IExtensionWrapper CreateExtension(List<bool> grantRequests, bool grantSucceeds = true)
    {
        var extension = new Mock<IExtensionWrapper>(MockBehavior.Strict);
        extension.Setup(static e => e.TryAllowSetForeground(It.IsAny<bool>()))
            .Callback<bool>(grantRequests.Add)
            .Returns(grantSucceeds);
        return extension.Object;
    }

    private static PowerToysRootPageService CreateService() => new(null!, null!, null!, null!, null!);
}
