// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Common.Services.Sanitizer;
using Microsoft.CmdPal.Common.Services.Sanitizer.Abstraction;
using Microsoft.CmdPal.Common.UnitTests.TestUtils;

namespace Microsoft.CmdPal.Common.UnitTests.Services.Sanitizer;

[TestClass]
public class ConnectionStringRuleProviderTests
{
    [TestMethod]
    public void GetRules_ShouldReturnExpectedRules()
    {
        // Arrange
        var provider = new ConnectionStringRuleProvider();

        // Act
        var rules = provider.GetRules();

        // Assert
        var ruleList = new List<SanitizationRule>(rules);
        Assert.AreEqual(1, ruleList.Count);
        Assert.AreEqual("Connection string parameters", ruleList[0].Description);
    }

    [DataTestMethod]
    [DataRow("Server=localhost;Database=mydb;User ID=admin;Password=secret123", "Server=[REDACTED];Database=[REDACTED];User ID=[REDACTED];Password=[REDACTED]")]
    [DataRow("Data Source=server.example.com;Initial Catalog=testdb;Uid=user;Pwd=pass", "Data Source=[REDACTED];Initial Catalog=[REDACTED];Uid=[REDACTED];Pwd=[REDACTED]")]
    [DataRow("Server=localhost;Password=my_secret", "Server=[REDACTED];Password=[REDACTED]")]
    [DataRow("No connection string here", "No connection string here")]
    public void ConnectionStringRules_ShouldMaskConnectionStringParameters(string input, string expected)
    {
        // Arrange
        var provider = new ConnectionStringRuleProvider();

        // Act
        var result = SanitizerTestHelper.ApplyRules(input, provider.GetRules());

        // Assert
        Assert.AreEqual(expected, result);
    }

    [DataTestMethod]
    [DataRow("Password=\"complexPassword123!\"", "Password=[REDACTED]")]
    [DataRow("Password='myPassword'", "Password=[REDACTED]")]
    [DataRow("Password=unquotedSecret", "Password=[REDACTED]")]
    public void ConnectionStringRules_ShouldHandleQuotedAndUnquotedValues(string input, string expected)
    {
        // Arrange
        var provider = new ConnectionStringRuleProvider();

        // Act
        var result = SanitizerTestHelper.ApplyRules(input, provider.GetRules());

        // Assert
        Assert.AreEqual(expected, result);
    }

    [DataTestMethod]
    [DataRow("SERVER=server1;PASSWORD=pass1", "SERVER=[REDACTED];PASSWORD=[REDACTED]")]
    [DataRow("server=server1;password=pass1", "server=[REDACTED];password=[REDACTED]")]
    [DataRow("Server=server1;Password=pass1", "Server=[REDACTED];Password=[REDACTED]")]
    public void ConnectionStringRules_ShouldBeCaseInsensitive(string input, string expected)
    {
        // Arrange
        var provider = new ConnectionStringRuleProvider();

        // Act
        var result = SanitizerTestHelper.ApplyRules(input, provider.GetRules());

        // Assert
        Assert.AreEqual(expected, result);
    }

    [DataTestMethod]
    [DataRow("User ID=admin;Username=john;Password=secret", "User ID=[REDACTED];Username=[REDACTED];Password=[REDACTED]")]
    [DataRow("Database=mydb;Uid=user1;Pwd=pass1;Server=localhost", "Database=[REDACTED];Uid=[REDACTED];Pwd=[REDACTED];Server=[REDACTED]")]
    public void ConnectionStringRules_ShouldHandleMultipleParameters(string input, string expected)
    {
        // Arrange
        var provider = new ConnectionStringRuleProvider();

        // Act
        var result = SanitizerTestHelper.ApplyRules(input, provider.GetRules());

        // Assert
        Assert.AreEqual(expected, result);
    }

    [DataTestMethod]
    [DataRow("Server = localhost ; Password = secret123", "Server=[REDACTED] ; Password=[REDACTED]")]
    [DataRow("Initial Catalog=db;  User ID=admin;  Password=pass", "Initial Catalog=[REDACTED];  User ID=[REDACTED];  Password=[REDACTED]")]
    public void ConnectionStringRules_ShouldHandleWhitespace(string input, string expected)
    {
        // Arrange
        var provider = new ConnectionStringRuleProvider();

        // Act
        var result = SanitizerTestHelper.ApplyRules(input, provider.GetRules());

        // Assert
        Assert.AreEqual(expected, result);
    }
}
