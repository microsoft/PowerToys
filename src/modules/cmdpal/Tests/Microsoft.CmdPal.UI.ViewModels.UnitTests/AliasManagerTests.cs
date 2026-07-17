// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public class AliasManagerTests
{
    [TestMethod]
    public void Constructor_PopulatesRunDefaultAliasWithCurrentCommandId()
    {
        var (settingsService, getSettings) = CreateSettingsService(new SettingsModel());

        _ = new AliasManager(null!, settingsService.Object);

        var runAlias = getSettings().Aliases[">"];
        Assert.AreEqual("com.microsoft.cmdpal.run", runAlias.CommandId);
        Assert.IsTrue(runAlias.IsDirect);
    }

    [TestMethod]
    public void Constructor_MigratesAliasesForRenamedRunCommand()
    {
        var aliases = ImmutableDictionary<string, CommandAlias>.Empty
            .Add(">", new CommandAlias(">", "com.microsoft.cmdpal.shell", true))
            .Add("run ", new CommandAlias("run", "com.microsoft.cmdpal.shell"))
            .Add("=", new CommandAlias("=", "com.microsoft.cmdpal.calculator", true));
        var (settingsService, getSettings) = CreateSettingsService(new SettingsModel { Aliases = aliases });

        _ = new AliasManager(null!, settingsService.Object);

        var migratedAliases = getSettings().Aliases;
        Assert.AreEqual("com.microsoft.cmdpal.run", migratedAliases[">"].CommandId);
        Assert.IsTrue(migratedAliases[">"].IsDirect);
        Assert.AreEqual("com.microsoft.cmdpal.run", migratedAliases["run "].CommandId);
        Assert.IsFalse(migratedAliases["run "].IsDirect);
        Assert.AreEqual("com.microsoft.cmdpal.calculator", migratedAliases["="].CommandId);
        Assert.IsFalse(migratedAliases.Values.Any(alias => alias.CommandId == "com.microsoft.cmdpal.shell"));
    }

    [TestMethod]
    public void Constructor_RemovesDeprecatedAliasWhenRunAlreadyHasAlias()
    {
        var aliases = ImmutableDictionary<string, CommandAlias>.Empty
            .Add(">", new CommandAlias(">", "com.microsoft.cmdpal.shell", true))
            .Add("run ", new CommandAlias("run", "com.microsoft.cmdpal.run"));
        var (settingsService, getSettings) = CreateSettingsService(new SettingsModel { Aliases = aliases });

        _ = new AliasManager(null!, settingsService.Object);

        var migratedAliases = getSettings().Aliases;
        Assert.IsFalse(migratedAliases.ContainsKey(">"));
        Assert.AreEqual("com.microsoft.cmdpal.run", migratedAliases["run "].CommandId);
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
