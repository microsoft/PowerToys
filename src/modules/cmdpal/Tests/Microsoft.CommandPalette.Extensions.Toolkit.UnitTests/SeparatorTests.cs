// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CommandPalette.Extensions.Toolkit.UnitTests;

[TestClass]
public class SeparatorTests
{
    [TestMethod]
    [DataRow("Renamed")]
    [DataRow("")]
    [DataRow(null)]
    public void Title_RaisesTitleAndSectionNotifications(string? title)
    {
        var separator = new Separator("Original");
        List<string> notifications = [];
        separator.PropChanged += (_, args) => notifications.Add(args.PropertyName);

        separator.Title = title;
        separator.Title = title;

        Assert.AreEqual(title, separator.Section);
        CollectionAssert.AreEqual(new[] { nameof(Separator.Title), nameof(Separator.Section) }, notifications);
    }

    [TestMethod]
    public void SectionCommand_IsStoredInExtendedAttributesWithoutChangingCommand()
    {
        var sectionCommand = new NoOpCommand { Name = "Show more..." };
        var separator = new Separator("Recent", sectionCommand);
        var provider = (IExtendedAttributesProvider)separator;

        Assert.IsNull(separator.Command);
        Assert.AreSame(sectionCommand, separator.SectionCommand);
        Assert.AreSame(sectionCommand, provider.GetProperties()[WellKnownExtensionAttributes.SectionCommand]);
        Assert.AreSame(provider.GetProperties(), provider.GetProperties());
    }

    [TestMethod]
    public void SectionCommand_UpdatesPropertiesBeforeNotifyingAndRemovesItWhenCleared()
    {
        var first = new NoOpCommand { Name = "First" };
        var second = new NoOpCommand { Name = "Second" };
        var separator = new Separator("Recent", first);
        var properties = separator.GetProperties();
        List<object?> valuesAtNotification = [];
        separator.PropChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(Separator.SectionCommand))
            {
                properties.TryGetValue(WellKnownExtensionAttributes.SectionCommand, out var value);
                valuesAtNotification.Add(value);
            }
        };

        separator.SectionCommand = second;
        separator.SectionCommand = second;
        separator.SectionCommand = null;

        CollectionAssert.AreEqual(new object?[] { second, null }, valuesAtNotification);
        Assert.IsFalse(properties.ContainsKey(WellKnownExtensionAttributes.SectionCommand));
        Assert.IsNull(separator.Command);
    }
}
