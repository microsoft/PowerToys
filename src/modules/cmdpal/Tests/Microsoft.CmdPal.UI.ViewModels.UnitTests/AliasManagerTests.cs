// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CmdPal.Common;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public class AliasManagerTests
{
    [TestMethod]
    public void Constructor_PopulatesDefaultAliasesWithBuiltInCommandIds()
    {
        var (settingsService, getSettings) = CreateSettingsService(new SettingsModel());
        var expectedAliases = new Dictionary<string, (string CommandId, bool IsDirect)>
        {
            [":"] = (BuiltInCommandIds.Registry, true),
            ["$"] = (BuiltInCommandIds.WindowsSettings, true),
            ["="] = (BuiltInCommandIds.Calculator, true),
            [">"] = (BuiltInCommandIds.Run, true),
            ["<"] = (BuiltInCommandIds.WindowWalker, true),
            ["??"] = (BuiltInCommandIds.WebSearch, true),
            ["file "] = (BuiltInCommandIds.FileSearch, false),
            [")"] = (BuiltInCommandIds.TimeDate, true),
        };

        _ = new AliasManager(null!, settingsService.Object);

        var aliases = getSettings().Aliases;
        Assert.AreEqual(expectedAliases.Count, aliases.Count);
        Assert.AreEqual(expectedAliases.Count, expectedAliases.Values.Select(alias => alias.CommandId).Distinct().Count());
        foreach (var expectedAlias in expectedAliases)
        {
            Assert.IsTrue(aliases.TryGetValue(expectedAlias.Key, out var alias));
            Assert.AreEqual(expectedAlias.Value.CommandId, alias.CommandId);
            Assert.AreEqual(expectedAlias.Value.IsDirect, alias.IsDirect);
        }
    }

    [TestMethod]
    public void Constructor_MigratesAliasesForRenamedRunCommand()
    {
        var aliases = ImmutableDictionary<string, CommandAlias>.Empty
            .Add(">", new CommandAlias(">", "com.microsoft.cmdpal.shell", true))
            .Add("run ", new CommandAlias("run", "com.microsoft.cmdpal.shell"))
            .Add("=", new CommandAlias("=", BuiltInCommandIds.Calculator, true));
        var (settingsService, getSettings) = CreateSettingsService(new SettingsModel { Aliases = aliases });

        _ = new AliasManager(null!, settingsService.Object);

        var migratedAliases = getSettings().Aliases;
        Assert.AreEqual(BuiltInCommandIds.Run, migratedAliases[">"].CommandId);
        Assert.IsTrue(migratedAliases[">"].IsDirect);
        Assert.AreEqual(BuiltInCommandIds.Run, migratedAliases["run "].CommandId);
        Assert.IsFalse(migratedAliases["run "].IsDirect);
        Assert.AreEqual(BuiltInCommandIds.Calculator, migratedAliases["="].CommandId);
        Assert.IsFalse(migratedAliases.Values.Any(alias => alias.CommandId == "com.microsoft.cmdpal.shell"));
    }

    [TestMethod]
    public void Constructor_RemovesDeprecatedAliasWhenRunAlreadyHasAlias()
    {
        var aliases = ImmutableDictionary<string, CommandAlias>.Empty
            .Add(">", new CommandAlias(">", "com.microsoft.cmdpal.shell", true))
            .Add("run ", new CommandAlias("run", BuiltInCommandIds.Run));
        var (settingsService, getSettings) = CreateSettingsService(new SettingsModel { Aliases = aliases });

        _ = new AliasManager(null!, settingsService.Object);

        var migratedAliases = getSettings().Aliases;
        Assert.IsFalse(migratedAliases.ContainsKey(">"));
        Assert.AreEqual(BuiltInCommandIds.Run, migratedAliases["run "].CommandId);
        Assert.IsFalse(migratedAliases["run "].IsDirect);
        Assert.IsFalse(migratedAliases.Values.Any(alias => alias.CommandId == "com.microsoft.cmdpal.shell"));
    }

    private static (Mock<ISettingsService> Service, Func<SettingsModel> GetSettings) CreateSettingsService(SettingsModel initialSettings)
    {
        var settings = initialSettings;
        var settingsService = new Mock<ISettingsService>();
        settingsService.SetupGet(service => service.Settings).Returns(() => settings);
        settingsService
            .Setup(service => service.UpdateSettings(It.IsAny<Func<SettingsModel, SettingsModel>>(), It.IsAny<bool>()))
            .Callback<Func<SettingsModel, SettingsModel>, bool>((transform, _) => settings = transform(settings));

        return (settingsService, () => settings);
    }
}
