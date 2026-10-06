// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.IO;
using System.Linq;
using Microsoft.CmdPal.Common.Helpers;
using Microsoft.CmdPal.Common.Services;
using Microsoft.CmdPal.Ext.Run;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Microsoft.CmdPal.Ext.Shell.UnitTests;

[TestClass]
public class RunItemShortcutTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void RunItem_RequestsAdministratorShortcut(bool commandLine)
    {
        var path = Path.Combine(Environment.SystemDirectory, "cmd.exe");
        var item = commandLine
            ? (ListItem)new RunCommandLineItem(path, "cmd /c echo test", null)
            : new RunExeItem("cmd", "/c echo test", path, null);

        Assert.HasCount(1, item.MoreCommands.OfType<CommandContextItem>().Where(command => command.RequestedShortcut == WellKnownKeyChords.RunAsAdministrator));
        Assert.AreEqual(WellKnownKeyChords.RunAsAdministrator, ((CommandContextItem)item.MoreCommands[0]).RequestedShortcut);
    }

    [TestMethod]
    public void HistoryItem_RequestsAdministratorShortcut()
    {
        var result = new ParseCommandlineResult
        {
            Result = 0,
            FilePath = Path.Combine(Environment.SystemDirectory, "cmd.exe"),
            Arguments = "/c echo test",
        };

        var item = RunDialogHelpers.CreateListItemForCommandResult(result);

        Assert.IsNotNull(item);
        Assert.AreEqual(WellKnownKeyChords.RunAsAdministrator, ((CommandContextItem)item.MoreCommands[0]).RequestedShortcut);
    }

    [TestMethod]
    public void FallbackItem_RequestsAdministratorShortcut()
    {
        var historyService = new Mock<IRunHistoryService>();
        historyService.Setup(service => service.ParseCommandline("cmd /c echo test", string.Empty)).Returns(new ParseCommandlineResult
        {
            Result = 0,
            FilePath = Path.Combine(Environment.SystemDirectory, "cmd.exe"),
            Arguments = "/c echo test",
        });
        using var item = new FallbackExecuteItem(historyService.Object, Mock.Of<ITelemetryService>());

        item.UpdateQuery("cmd /c echo test");

        Assert.AreEqual(WellKnownKeyChords.RunAsAdministrator, ((CommandContextItem)item.MoreCommands[0]).RequestedShortcut);
    }
}
