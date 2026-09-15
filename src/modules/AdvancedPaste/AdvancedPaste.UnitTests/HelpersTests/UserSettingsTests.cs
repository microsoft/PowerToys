// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using System.Linq;
using System.Threading;

using AdvancedPaste.Models;
using AdvancedPaste.Settings;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace AdvancedPaste.UnitTests.HelpersTests;

[TestClass]
public sealed class UserSettingsTests
{
    private static readonly PasteFormats[] TextCaseFormats =
    [
        PasteFormats.LowerCase,
        PasteFormats.UpperCase,
        PasteFormats.TitleCase,
        PasteFormats.SentenceCase,
        PasteFormats.ToggleCase,
        PasteFormats.CamelCase,
        PasteFormats.PascalCase,
        PasteFormats.SnakeCase,
        PasteFormats.ScreamingSnakeCase,
        PasteFormats.KebabCase,
    ];

    [DataTestMethod]
    [DataRow(PasteFormats.LowerCase)]
    [DataRow(PasteFormats.UpperCase)]
    [DataRow(PasteFormats.TitleCase)]
    [DataRow(PasteFormats.SentenceCase)]
    [DataRow(PasteFormats.ToggleCase)]
    [DataRow(PasteFormats.CamelCase)]
    [DataRow(PasteFormats.PascalCase)]
    [DataRow(PasteFormats.SnakeCase)]
    [DataRow(PasteFormats.ScreamingSnakeCase)]
    [DataRow(PasteFormats.KebabCase)]
    public void TextCaseFormatRequiresParentAndTargetLeaf(PasteFormats format)
    {
        Assert.IsFalse(LoadAdditionalActions(format, parentShown: false, leafShown: true, siblingShown: false).Contains(format));
        Assert.IsFalse(LoadAdditionalActions(format, parentShown: true, leafShown: false, siblingShown: false).Contains(format));
        Assert.IsTrue(LoadAdditionalActions(format, parentShown: true, leafShown: true, siblingShown: false).Contains(format));
        Assert.IsTrue(LoadAdditionalActions(format, parentShown: true, leafShown: true, siblingShown: true).Contains(format));
    }

    [TestMethod]
    public void DisabledTextCaseParentSuppressesEveryLeaf()
    {
        var actions = LoadAdditionalActions(PasteFormats.LowerCase, parentShown: false, leafShown: true, siblingShown: true);

        CollectionAssert.AreEqual(Array.Empty<PasteFormats>(), actions.Intersect(TextCaseFormats).ToArray());
    }

    [TestMethod]
    public void EnabledTextCaseParentAndLeavesExposeEveryLeaf()
    {
        var actions = LoadAdditionalActions(PasteFormats.LowerCase, parentShown: true, leafShown: true, siblingShown: true);

        CollectionAssert.AreEqual(TextCaseFormats, actions.Where(TextCaseFormats.Contains).ToArray());
    }

    private static PasteFormats[] LoadAdditionalActions(PasteFormats target, bool parentShown, bool leafShown, bool siblingShown)
    {
        var textCase = new AdvancedPasteTextCaseAction { IsShown = parentShown };
        foreach (var action in textCase.SubActions.Cast<AdvancedPasteAdditionalAction>())
        {
            action.IsShown = siblingShown;
        }

        GetLeaf(textCase, target).IsShown = leafShown;

        var settings = new AdvancedPasteSettings();
        settings.Properties.AdditionalActions = new AdvancedPasteAdditionalActions { TextCase = textCase };

        var fileSystem = new WatchableMockFileSystem();
        settings.Save(new SettingsUtils(fileSystem));

        var originalContext = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(originalContext ?? new SynchronizationContext());
        try
        {
            using var userSettings = new UserSettings(fileSystem);
            return userSettings.AdditionalActions.ToArray();
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(originalContext);
        }
    }

    private static AdvancedPasteAdditionalAction GetLeaf(AdvancedPasteTextCaseAction textCase, PasteFormats format) => format switch
    {
        PasteFormats.LowerCase => textCase.LowerCase,
        PasteFormats.UpperCase => textCase.UpperCase,
        PasteFormats.TitleCase => textCase.TitleCase,
        PasteFormats.SentenceCase => textCase.SentenceCase,
        PasteFormats.ToggleCase => textCase.ToggleCase,
        PasteFormats.CamelCase => textCase.CamelCase,
        PasteFormats.PascalCase => textCase.PascalCase,
        PasteFormats.SnakeCase => textCase.SnakeCase,
        PasteFormats.ScreamingSnakeCase => textCase.ScreamingSnakeCase,
        PasteFormats.KebabCase => textCase.KebabCase,
        _ => throw new ArgumentOutOfRangeException(nameof(format)),
    };

    private sealed class WatchableMockFileSystem : MockFileSystem
    {
        public override IFileSystemWatcherFactory FileSystemWatcher { get; }

        public WatchableMockFileSystem()
        {
            var watcherFactory = new Mock<IFileSystemWatcherFactory>();
            watcherFactory.Setup(factory => factory.New()).Returns(new Mock<IFileSystemWatcher>().Object);
            FileSystemWatcher = watcherFactory.Object;
        }
    }
}
