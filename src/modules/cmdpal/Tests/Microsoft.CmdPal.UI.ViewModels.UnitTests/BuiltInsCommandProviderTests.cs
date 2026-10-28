// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.Common.Services;
using Microsoft.CmdPal.UI.ViewModels.BuiltinCommands;
using Microsoft.CmdPal.UI.ViewModels.MainPage;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.CmdPal.UI.ViewModels.Properties;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public sealed class BuiltInsCommandProviderTests
{
    [TestMethod]
    public void TopLevelCommands_IncludeFormerFallbacksWithStableIdentityAndTitles()
    {
        using var provider = CreateProvider();

        var items = provider.TopLevelCommands();

        Assert.AreEqual(6, items.Length);
        Assert.IsTrue(provider.FallbackCommands() is not { Length: > 0 });
        foreach (var item in items)
        {
            Assert.IsFalse(item is IFallbackCommandItem);
            Assert.IsFalse(item.Command is IFallbackHandler);
            Assert.IsFalse(string.IsNullOrWhiteSpace(item.Title));
        }

        var quit = items.Single(item => item.Command?.Id == "com.microsoft.cmdpal.quit");
        Assert.AreEqual(Resources.builtin_quit_title, quit.Title);
        Assert.AreEqual(string.Empty, quit.Subtitle);

        var reload = items.Single(item => item.Command?.Id == "com.microsoft.cmdpal.reload");
        Assert.AreEqual(Resources.builtin_reload_title, reload.Title);
        Assert.AreEqual(string.Empty, reload.Subtitle);

        var log = items.Single(item => item.Command?.Id == "com.microsoft.cmdpal.log");
        Assert.AreEqual(Resources.builtin_log_folder_command_name, log.Title);
        Assert.AreEqual(1, log.MoreCommands.Length);
    }

    [TestMethod]
    [DataRow("com.microsoft.cmdpal.quit", "EXIT")]
    [DataRow("com.microsoft.cmdpal.quit", "palette")]
    [DataRow("com.microsoft.cmdpal.reload", "RELOAD")]
    [DataRow("com.microsoft.cmdpal.reload", "extensions")]
    [DataRow("com.microsoft.cmdpal.log", "LOG")]
    [DataRow("com.microsoft.cmdpal.log", "folder")]
    public void TopLevelCommands_MatchNormalSearchAndRejectUnrelatedQueries(string commandId, string query)
    {
        using var provider = CreateProvider();
        var commandItem = provider.TopLevelCommands().Single(item => item.Command?.Id == commandId);
        var item = new ListItem(commandItem.Command!) { Title = commandItem.Title, Subtitle = commandItem.Subtitle };
        var matcher = ScoringTestCatalog.CreateMatcher();
        var history = new RecentCommandsManager();

        Assert.IsTrue(MainListPage.ScoreTopLevelItem(matcher.PrecomputeQuery(query), item, history, matcher) > 0);
        Assert.AreEqual(0, MainListPage.ScoreTopLevelItem(matcher.PrecomputeQuery("zzzzzz"), item, history, matcher));
    }

    [TestMethod]
    public void TopLevelCommands_QuitAndReloadPreserveActions()
    {
        using var provider = CreateProvider();
        var items = provider.TopLevelCommands();
        var messages = new List<string>();
        var recipient = new object();
        WeakReferenceMessenger.Default.Register<QuitMessage>(recipient, (_, _) => messages.Add("quit"));
        WeakReferenceMessenger.Default.Register<ClearSearchMessage>(recipient, (_, _) => messages.Add("clear"));
        WeakReferenceMessenger.Default.Register<ReloadCommandsMessage>(recipient, (_, _) => messages.Add("reload"));

        try
        {
            var quit = (InvokableCommand)items.Single(item => item.Command?.Id == "com.microsoft.cmdpal.quit").Command!;
            Assert.AreEqual(CommandResultKind.KeepOpen, quit.Invoke().Kind);
            Assert.AreEqual(1, messages.Count);
            Assert.AreEqual("quit", messages[0]);

            messages.Clear();
            var reload = (InvokableCommand)items.Single(item => item.Command?.Id == "com.microsoft.cmdpal.reload").Command!;
            Assert.AreEqual(CommandResultKind.GoHome, reload.Invoke().Kind);
            Assert.AreEqual(2, messages.Count);
            Assert.AreEqual("clear", messages[0]);
            Assert.AreEqual("reload", messages[1]);
        }
        finally
        {
            WeakReferenceMessenger.Default.UnregisterAll(recipient);
        }
    }

    private static BuiltInsCommandProvider CreateProvider() => new(
        Mock.Of<IRootPageAccessor>(),
        Mock.Of<IApplicationInfoService>(info => info.LogDirectory == @"C:\Logs"));
}
