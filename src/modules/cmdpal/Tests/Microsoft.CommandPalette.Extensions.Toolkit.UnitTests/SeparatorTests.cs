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
}
