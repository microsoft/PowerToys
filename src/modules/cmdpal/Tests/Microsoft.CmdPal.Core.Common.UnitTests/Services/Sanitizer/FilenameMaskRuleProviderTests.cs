// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Common.UnitTests.TestUtils;
using Microsoft.CmdPal.Core.Common.Services.Sanitizer;
using Microsoft.CmdPal.Core.Common.Services.Sanitizer.Abstraction;

namespace Microsoft.CmdPal.Common.UnitTests.Services.Sanitizer;

[TestClass]
public class FilenameMaskRuleProviderTests
{
    [TestMethod]
    public void GetRules_ShouldReturnExpectedRules()
    {
        // Arrange
        var provider = new FilenameMaskRuleProvider();

        // Act
        var rules = provider.GetRules();

        // Assert
        var ruleList = new List<SanitizationRule>(rules);
        Assert.AreEqual(1, ruleList.Count);
        Assert.AreEqual("Mask filename in any path", ruleList[0].Description);
    }

    [DataTestMethod]
    [DataRow(@"C:\Users\Alice\Documents\secret.txt", @"C:\Users\Alice\Documents\se****.txt")]
    [DataRow(@"logs\error-report.log", @"logs\er**********.log")]
    [DataRow(@"/var/logs/trace.json", @"/var/logs/tr***.json")]
    public void FilenameRules_ShouldMaskFileNamesInPaths(string input, string expected)
    {
        // Arrange
        var provider = new FilenameMaskRuleProvider();

        // Act
        var result = SanitizerTestHelper.ApplyRules(input, provider.GetRules());

        // Assert
        Assert.AreEqual(expected, result);
    }

    [DataTestMethod]
    [DataRow("C:\\Users\\Alice\\Documents\\", "C:\\Users\\Alice\\Documents\\")]
    [DataRow(@"C:\Users\Alice\PowerToys\CmdPal\Logs\1.2.3.4", @"C:\Users\Alice\PowerToys\CmdPal\Logs\1.2.3.4")]
    [DataRow(@"C:\Users\Alice\appsettings.json", @"C:\Users\Alice\appsettings.json")]
    [DataRow(@"C:\Users\Alice\.env", @"C:\Users\Alice\.env")]
    [DataRow(@"logs\readme", @"logs\readme")]
    public void FilenameRules_ShouldNotMaskNonSensitivePatterns(string input, string expected)
    {
        // Arrange
        var provider = new FilenameMaskRuleProvider();

        // Act
        var result = SanitizerTestHelper.ApplyRules(input, provider.GetRules());

        // Assert
        Assert.AreEqual(expected, result);
    }
}
