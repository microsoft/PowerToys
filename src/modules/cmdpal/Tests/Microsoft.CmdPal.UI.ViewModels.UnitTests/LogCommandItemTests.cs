// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using Microsoft.CmdPal.Common.Services;
using Microsoft.CmdPal.UI.ViewModels.BuiltinCommands;
using Microsoft.CmdPal.UI.ViewModels.Properties;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public sealed class LogCommandItemTests
{
    [TestMethod]
    public void LogCommand_HasPermanentTitleAndPreservesIdentityAndContextCommand()
    {
        var applicationInfo = Mock.Of<IApplicationInfoService>(info => info.LogDirectory == @"C:\Logs");
        var item = new LogCommandItem(applicationInfo);

        Assert.IsInstanceOfType<OpenFileCommand>(item.Command);
        Assert.AreEqual("com.microsoft.cmdpal.log", item.Command.Id);
        Assert.AreEqual(Resources.builtin_log_folder_command_name, item.Title);
        Assert.AreEqual(item.Title, item.Command.Name);
        Assert.AreEqual(1, item.MoreCommands.Length);
        var openLogFileItem = item.MoreCommands[0] as CommandContextItem;
        Assert.IsNotNull(openLogFileItem);
        Assert.IsInstanceOfType<InvokableCommand>(openLogFileItem.Command);
        Assert.AreEqual(Resources.builtin_log_file_command_name, openLogFileItem.Command.Name);
    }

    [TestMethod]
    public void OpenCurrentLogFile_ResolvesPathAtInvocationAndIgnoresUnavailableFiles()
    {
        var applicationInfo = new Mock<IApplicationInfoService>();
        applicationInfo.SetupGet(info => info.LogDirectory).Returns(@"C:\Logs");
        applicationInfo.SetupSequence(info => info.LogFile)
            .Returns((string?)null)
            .Returns(string.Empty)
            .Returns(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "missing.log"));

        var item = new LogCommandItem(applicationInfo.Object);
        var openLogFileItem = item.MoreCommands[0] as CommandContextItem;
        Assert.IsNotNull(openLogFileItem);
        var command = openLogFileItem.Command as InvokableCommand;
        Assert.IsNotNull(command);
        applicationInfo.VerifyGet(info => info.LogFile, Times.Never);

        command.Invoke();
        command.Invoke();
        command.Invoke();

        applicationInfo.VerifyGet(info => info.LogFile, Times.Exactly(3));
    }
}
