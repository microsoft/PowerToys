// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Common.Services.Sanitizer;
using Microsoft.CmdPal.Common.Services.Sanitizer.Abstraction;
using Microsoft.CmdPal.Common.UnitTests.TestUtils;

namespace Microsoft.CmdPal.Common.UnitTests.Services.Sanitizer;

[TestClass]
public class PiiRuleProviderTests
{
    [TestMethod]
    public void GetRules_ShouldReturnExpectedRules()
    {
        // Arrange
        var provider = new PiiRuleProvider();

        // Act
        var rules = provider.GetRules();

        // Assert
        var ruleList = new List<SanitizationRule>(rules);
        Assert.AreEqual(4, ruleList.Count);
        Assert.AreEqual("Email addresses", ruleList[0].Description);
        Assert.AreEqual("Social Security Numbers", ruleList[1].Description);
        Assert.AreEqual("Credit card numbers", ruleList[2].Description);
        Assert.AreEqual("Phone numbers", ruleList[3].Description);
    }

    [DataTestMethod]
    [DataRow("Contact me at john.doe@contoso.com", "Contact me at [EMAIL_REDACTED]")]
    [DataRow("Contact me at a_b-c%2@foo-bar.example.co.uk", "Contact me at [EMAIL_REDACTED]")]
    [DataRow("My email is john@sub-domain.contoso.com.", "My email is [EMAIL_REDACTED].")]
    [DataRow("Two: a@b.com and c@d.org", "Two: [EMAIL_REDACTED] and [EMAIL_REDACTED]")]
    [DataRow("No email here", "No email here")]
    public void EmailRules_ShouldMaskEmailAddresses(string input, string expected)
    {
        // Arrange
        var provider = new PiiRuleProvider();

        // Act
        var result = SanitizerTestHelper.ApplyRules(input, provider.GetRules());

        // Assert
        Assert.AreEqual(expected, result);
    }

    [DataTestMethod]
    [DataRow("Call me at 123-456-7890", "Call me at [PHONE_REDACTED]")]
    [DataRow("My number is (123) 456-7890.", "My number is [PHONE_REDACTED].")]
    [DataRow("Office: +1 123 456 7890", "Office: [PHONE_REDACTED]")]
    [DataRow("Two numbers: 123-456-7890 and +420 777123456", "Two numbers: [PHONE_REDACTED] and [PHONE_REDACTED]")]
    [DataRow("Czech phone +420 777 123 456", "Czech phone [PHONE_REDACTED]")]
    [DataRow("Slovak phone +421 777 12 34 56", "Slovak phone [PHONE_REDACTED]")]
    [DataRow("Version 1.2.3.4", "Version 1.2.3.4")]
    [DataRow("OS version: Microsoft Windows 10.0.26220", "OS version: Microsoft Windows 10.0.26220")]
    [DataRow("No phone number here", "No phone number here")]
    public void PhoneRules_ShouldMaskPhoneNumbers(string input, string expected)
    {
        // Arrange
        var provider = new PiiRuleProvider();

        // Act
        var result = SanitizerTestHelper.ApplyRules(input, provider.GetRules());

        // Assert
        Assert.AreEqual(expected, result);
    }

    [DataTestMethod]
    [DataRow("My SSN is 123-45-6789", "My SSN is [SSN_REDACTED]")]
    [DataRow("No SSN here", "No SSN here")]
    public void SsnRules_ShouldMaskSsn(string input, string expected)
    {
        // Arrange
        var provider = new PiiRuleProvider();

        // Act
        var result = SanitizerTestHelper.ApplyRules(input, provider.GetRules());

        // Assert
        Assert.AreEqual(expected, result);
    }

    [DataTestMethod]
    [DataRow("My credit card number is 1234-5678-9012-3456", "My credit card number is [CARD_REDACTED]")]
    [DataRow("My credit card number is 1234567890123456", "My credit card number is [CARD_REDACTED]")]
    [DataRow("No credit card here", "No credit card here")]
    public void CreditCardRules_ShouldMaskCreditCardNumbers(string input, string expected)
    {
        // Arrange
        var provider = new PiiRuleProvider();

        // Act
        var result = SanitizerTestHelper.ApplyRules(input, provider.GetRules());

        // Assert
        Assert.AreEqual(expected, result);
    }

    [DataTestMethod]
    [DataRow("Error code: 0x80070005", "Error code: 0x80070005")]
    [DataRow("Error code: -2147467262", "Error code: -2147467262")]
    [DataRow("GUID: 123e4567-e89b-12d3-a456-426614174000", "GUID: 123e4567-e89b-12d3-a456-426614174000")]
    [DataRow("Timestamp: 2023-10-05T14:32:10Z", "Timestamp: 2023-10-05T14:32:10Z")]
    [DataRow("Version: 1.2.3", "Version: 1.2.3")]
    [DataRow("Version: 1.2.3.4", "Version: 1.2.3.4")]
    [DataRow("Version: 0.2.3.4", "Version: 0.2.3.4")]
    [DataRow("Version: 10.0.22631.3448", "Version: 10.0.22631.3448")]
    [DataRow("MAC: 00:1A:2B:3C:4D:5E", "MAC: 00:1A:2B:3C:4D:5E")]
    [DataRow("Date: 2023-10-05", "Date: 2023-10-05")]
    [DataRow("Date: 05/10/2023", "Date: 05/10/2023")]
    public void PiiRuleProvider_ShouldNotOverRedact(string input, string expected)
    {
        // Arrange
        var provider = new PiiRuleProvider();

        // Act
        var result = SanitizerTestHelper.ApplyRules(input, provider.GetRules());

        // Assert
        Assert.AreEqual(expected, result);
    }
}
