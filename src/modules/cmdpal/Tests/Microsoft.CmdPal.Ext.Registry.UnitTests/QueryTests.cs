// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;
using Microsoft.CmdPal.Ext.Registry.Helpers;
using Microsoft.CmdPal.Ext.UnitTestBase;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.Registry.UnitTests;

[OSCondition(OperatingSystems.Windows)]
[TestClass]
public class QueryTests : CommandPaletteUnitTestBase
{
    [TestMethod]
    [DataRow("HKLM", "HKEY_LOCAL_MACHINE")]
    [DataRow("HKCU", "HKEY_CURRENT_USER")]
    [DataRow("HKCR", "HKEY_CLASSES_ROOT")]
    [DataRow("HKU", "HKEY_USERS")]
    [DataRow("HKCC", "HKEY_CURRENT_CONFIG")]
    public void TopLevelPageQueryTest(string input, string expectedKeyName)
    {
        var settings = new Settings();
        var page = new RegistryListPage(settings);
        var results = page.Query(input);

        Assert.IsNotNull(results);
        Assert.IsNotEmpty(results, "No items matched the query.");

        var firstItem = results.FirstOrDefault();
        Assert.IsNotNull(firstItem, "No items matched the query.");
        Assert.IsTrue(
            firstItem.Title.Contains(expectedKeyName, System.StringComparison.OrdinalIgnoreCase),
            $"Expected to match '{expectedKeyName}' but got '{firstItem.Title}'");
    }

    [TestMethod]
    public void EmptyQueryTest()
    {
        var settings = new Settings();
        var page = new RegistryListPage(settings);
        var results = page.Query(string.Empty);

        Assert.IsNotNull(results);

        // Empty query should return all base keys
        Assert.IsGreaterThanOrEqualTo(5, results.Count, "Expected at least 5 base registry keys.");
    }

    [TestMethod]
    public void NullQueryTest()
    {
        var settings = new Settings();
        var page = new RegistryListPage(settings);
        var results = page.Query(null);

        Assert.IsNotNull(results);
        Assert.IsEmpty(results, "Null query should return empty results.");
    }

    [TestMethod]
    public void InvalidBaseKeyTest()
    {
        var settings = new Settings();
        var page = new RegistryListPage(settings);
        var results = page.Query("INVALID_KEY");

        Assert.IsNotNull(results);

        Assert.IsEmpty(results, "Invalid query should return empty results.");
    }
}
