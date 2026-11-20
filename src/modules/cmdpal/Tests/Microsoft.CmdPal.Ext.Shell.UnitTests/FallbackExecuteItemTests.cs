// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.CmdPal.Common.Helpers;
using Microsoft.CmdPal.Common.Services;
using Microsoft.CmdPal.Ext.Run;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Microsoft.CmdPal.Ext.Shell.UnitTests;

[TestClass]
public class FallbackExecuteItemTests
{
    [TestMethod]
    public void UpdateQuery_ShowsTheShellIconOfTheResolvedFile()
    {
        using var fallback = CreateFallback();

        fallback.UpdateQuery("cmd /c");

        Assert.IsTrue(ShellItemIconProtocol.TryParse(fallback.Icon?.Light?.Icon, out var itemPath, out _));
        Assert.AreEqual(ExePath("cmd"), itemPath);
    }

    [TestMethod]
    public void UpdateQuery_ShowsTheRunIconForAnUnresolvedCommandLine()
    {
        using var fallback = CreateFallback(result: unchecked((int)0x80070002));

        fallback.UpdateQuery("missing /c");

        Assert.AreSame(Icons.RunV2Icon, fallback.Icon);
    }

    [TestMethod]
    public void UpdateQuery_ShowsTheRunIconForAMissingCommand()
    {
        const string CommandLine = "cmdpal-missing-command /c";
        var history = new Mock<IRunHistoryService>();
        history
            .Setup(service => service.ParseCommandline(It.IsAny<string>(), It.IsAny<string>()))
            .Returns((string commandLine, string workingDirectory) => RunHistory.ParseCommandline(commandLine, workingDirectory));
        using var fallback = new FallbackExecuteItem(history.Object, Mock.Of<ITelemetryService>());

        fallback.UpdateQuery(CommandLine);

        // The real parser reports success even though no file has that name.
        Assert.IsTrue(RunHistory.ParseCommandline(CommandLine, string.Empty).Success);
        Assert.AreSame(Icons.RunV2Icon, fallback.Icon);
    }

    [TestMethod]
    public void UpdateQuery_ShowsTheRunIconForAnUncheckedQuotedPath()
    {
        // No application has this name, so the system template can't resolve it and parsing takes the
        // quoted branch, wherever the tests run.
        const string QuotedCommandLine = "\"cmdpal-quoted-relative.txt\" /a";
        var history = new Mock<IRunHistoryService>();
        history
            .Setup(service => service.ParseCommandline(It.IsAny<string>(), It.IsAny<string>()))
            .Returns((string commandLine, string workingDirectory) => RunHistory.ParseCommandline(commandLine, workingDirectory));
        using var fallback = new FallbackExecuteItem(history.Object, Mock.Of<ITelemetryService>());

        fallback.UpdateQuery(QuotedCommandLine);

        // Parsing doesn't check a quoted path, so showing its icon would take another file system check
        // before publishing.
        Assert.IsFalse(RunHistory.ParseCommandline(QuotedCommandLine, string.Empty).TargetFound);
        Assert.AreSame(Icons.RunV2Icon, fallback.Icon);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void UpdateQuery_ShowsTheFileIconOnlyForAFoundTarget(bool targetFound)
    {
        var history = new Mock<IRunHistoryService>();
        history
            .Setup(service => service.ParseCommandline(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(new ParseCommandlineResult { FilePath = ExePath("cmd"), Arguments = string.Empty, TargetFound = targetFound });
        using var fallback = new FallbackExecuteItem(history.Object, Mock.Of<ITelemetryService>());

        fallback.UpdateQuery("cmd");

        // Only a target parsing found gets its icon; nothing checks the file system again to find out.
        Assert.AreEqual(targetFound, ShellItemIconProtocol.IsProtocol(fallback.Icon?.Light?.Icon));
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void UpdateQuery_BuildsTheMenuForWhatThePathIs(bool directory)
    {
        var path = directory ? Environment.SystemDirectory : ExePath("cmd");
        var history = new Mock<IRunHistoryService>();
        history
            .Setup(service => service.ParseCommandline(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(new ParseCommandlineResult { FilePath = path, Arguments = string.Empty, TargetFound = true });
        using var fallback = new FallbackExecuteItem(history.Object, Mock.Of<ITelemetryService>());

        fallback.UpdateQuery(path);

        // A folder can't run as administrator, so its menu leaves that out.
        var runsAsAdministrator = fallback.MoreCommands.OfType<CommandContextItem>().Any(item => item.RequestedShortcut == WellKnownKeyChords.RunAsAdministrator);
        Assert.AreEqual(!directory, runsAsAdministrator);
    }

    [TestMethod]
    public void UpdateQuery_PublishesTheParsedCommandWhenTheTargetWasNotFound()
    {
        var path = ExePath("cmdpal-unfound-tool");
        var history = new Mock<IRunHistoryService>();
        history
            .Setup(service => service.ParseCommandline(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(new ParseCommandlineResult { FilePath = path, Arguments = "/check", TargetFound = false });
        using var fallback = new FallbackExecuteItem(history.Object, Mock.Of<ITelemetryService>());

        fallback.UpdateQuery($"{path} /check");

        // Nothing checks the file system between parsing and publishing the command, so Enter runs exactly this.
        var exeItem = ExeItemOf(fallback.Command);
        Assert.AreEqual(path, exeItem.FullExePath);
        Assert.AreEqual($"{path} /check", exeItem.Title);
        Assert.AreSame(Icons.RunV2Icon, fallback.Icon);
    }

    [TestMethod]
    public void UpdateQuery_KeepsTheFileIconWhenOnlyArgumentsChange()
    {
        using var fallback = CreateFallback();
        fallback.UpdateQuery("cmd /c echo one");
        var icon = fallback.Icon;
        var iconChanges = 0;
        fallback.PropChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(fallback.Icon))
            {
                iconChanges++;
            }
        };

        fallback.UpdateQuery("cmd /c echo two");

        // Publishing an equal icon, or the Run placeholder, makes the row reload it and flicker.
        Assert.IsTrue(ShellItemIconProtocol.IsProtocol(icon?.Light?.Icon));
        Assert.AreSame(icon, fallback.Icon);
        Assert.AreEqual(0, iconChanges);
    }

    [TestMethod]
    public void UpdateQuery_ChangesTheIconWhenTheFileChanges()
    {
        using var fallback = CreateFallback();
        fallback.UpdateQuery("cmd /c");
        var icon = fallback.Icon;

        fallback.UpdateQuery("notepad /c");

        Assert.AreNotSame(icon, fallback.Icon);
        Assert.IsTrue(ShellItemIconProtocol.TryParse(fallback.Icon?.Light?.Icon, out var itemPath, out _));
        Assert.AreEqual(ExePath("notepad"), itemPath);
    }

    [TestMethod]
    public void UpdateQuery_KeepsTheArgumentsOfAPublishedCommand()
    {
        using var fallback = CreateFallback();
        fallback.UpdateQuery("cmd /c echo one");
        var invoked = fallback.Command;

        fallback.UpdateQuery("cmd /c echo two");

        // The host runs an invoked command later, so a newer query must not change what it runs.
        Assert.AreEqual($"{ExePath("cmd")} /c echo one", ExeItemOf(invoked).Title);
        Assert.AreEqual($"{ExePath("cmd")} /c echo two", ExeItemOf(fallback.Command).Title);
    }

    private static RunExeItem ExeItemOf(ICommand? command)
    {
        var action = (Action)typeof(AnonymousCommand).GetField("_action", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(command)!;
        return (RunExeItem)action.Target!;
    }

    private static FallbackExecuteItem CreateFallback(int result = 0)
    {
        var history = new Mock<IRunHistoryService>();
        history
            .Setup(service => service.ParseCommandline(It.IsAny<string>(), It.IsAny<string>()))
            .Returns((string commandLine, string _) =>
            {
                var parts = commandLine.Split(' ', 2);
                return new ParseCommandlineResult
                {
                    Result = result,
                    FilePath = ExePath(parts[0]),
                    Arguments = parts.Length > 1 ? parts[1] : string.Empty,
                    TargetFound = result == 0,
                };
            });

        return new FallbackExecuteItem(history.Object, Mock.Of<ITelemetryService>());
    }

    private static string ExePath(string name) => Path.Combine(Environment.SystemDirectory, name + ".exe");
}
