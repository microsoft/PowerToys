// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Common.Services.Sanitizer;
using Microsoft.CmdPal.Common.Services.Sanitizer.Abstraction;
using Microsoft.CmdPal.Common.UnitTests.TestUtils;

namespace Microsoft.CmdPal.Common.UnitTests.Services.Sanitizer;

[TestClass]
public class SecretKeyValueRulesProviderTests
{
    [TestMethod]
    public void GetRules_ShouldReturnExpectedRules()
    {
        // Arrange
        var provider = new SecretKeyValueRulesProvider();

        // Act
        var rules = provider.GetRules();

        // Assert
        var ruleList = new List<SanitizationRule>(rules);
        Assert.AreEqual(1, ruleList.Count);
        Assert.AreEqual("Sensitive key/value pairs", ruleList[0].Description);
    }

    [DataTestMethod]
    [DataRow("password=secret123", "password= [REDACTED]")]
    [DataRow("passphrase=myPassphrase", "passphrase= [REDACTED]")]
    [DataRow("pwd=test", "pwd= [REDACTED]")]
    [DataRow("passwd=pass1234", "passwd= [REDACTED]")]
    public void SecretKeyValueRules_ShouldMaskPasswordSecrets(string input, string expected)
    {
        // Arrange
        var provider = new SecretKeyValueRulesProvider();

        // Act
        var result = SanitizerTestHelper.ApplyRules(input, provider.GetRules());

        // Assert
        Assert.AreEqual(expected, result);
    }

    [DataTestMethod]
    [DataRow("token=abc123def456", "token= [REDACTED]")]
    [DataRow("access_token=token_value", "access_token= [REDACTED]")]
    [DataRow("refresh-token=refresh_value", "refresh-token= [REDACTED]")]
    [DataRow("id token=id_token_value", "id token= [REDACTED]")]
    [DataRow("bearer token=bearer_value", "bearer token= [REDACTED]")]
    [DataRow("session token=session_value", "session token= [REDACTED]")]
    public void SecretKeyValueRules_ShouldMaskTokens(string input, string expected)
    {
        // Arrange
        var provider = new SecretKeyValueRulesProvider();

        // Act
        var result = SanitizerTestHelper.ApplyRules(input, provider.GetRules());

        // Assert
        Assert.AreEqual(expected, result);
    }

    [DataTestMethod]
    [DataRow("api key=my_api_key", "api key= [REDACTED]")]
    [DataRow("api-key=key123", "api-key= [REDACTED]")]
    [DataRow("api_key=secret_key", "api_key= [REDACTED]")]
    [DataRow("x-api-key=api123", "x-api-key= [REDACTED]")]
    [DataRow("x api key=key456", "x api key= [REDACTED]")]
    [DataRow("client id=client123", "client id= [REDACTED]")]
    [DataRow("client-secret=secret123", "client-secret= [REDACTED]")]
    [DataRow("consumer secret=secret456", "consumer secret= [REDACTED]")]
    public void SecretKeyValueRules_ShouldMaskApiCredentials(string input, string expected)
    {
        // Arrange
        var provider = new SecretKeyValueRulesProvider();

        // Act
        var result = SanitizerTestHelper.ApplyRules(input, provider.GetRules());

        // Assert
        Assert.AreEqual(expected, result);
    }

    [DataTestMethod]
    [DataRow("subscription key=sub_key_123", "subscription key= [REDACTED]")]
    [DataRow("instrumentation key=instr_key", "instrumentation key= [REDACTED]")]
    [DataRow("account key=account123", "account key= [REDACTED]")]
    [DataRow("storage account key=storage_key", "storage account key= [REDACTED]")]
    [DataRow("shared access key=sak123", "shared access key= [REDACTED]")]
    [DataRow("SAS token=sas123", "SAS token= [REDACTED]")]
    public void SecretKeyValueRules_ShouldMaskCloudPlatformKeys(string input, string expected)
    {
        // Arrange
        var provider = new SecretKeyValueRulesProvider();

        // Act
        var result = SanitizerTestHelper.ApplyRules(input, provider.GetRules());

        // Assert
        Assert.AreEqual(expected, result);
    }

    [DataTestMethod]
    [DataRow("connection string=Server=localhost;Pwd=pass", "connection string= [REDACTED]")]
    [DataRow("conn string=conn_value", "conn string= [REDACTED]")]
    [DataRow("storage connection string=connection_value", "storage connection string= [REDACTED]")]
    public void SecretKeyValueRules_ShouldMaskConnectionStrings(string input, string expected)
    {
        // Arrange
        var provider = new SecretKeyValueRulesProvider();

        // Act
        var result = SanitizerTestHelper.ApplyRules(input, provider.GetRules());

        // Assert
        Assert.AreEqual(expected, result);
    }

    [DataTestMethod]
    [DataRow("private key=pk123", "private key= [REDACTED]")]
    [DataRow("certificate password=cert_pass", "certificate password= [REDACTED]")]
    [DataRow("client certificate password=cert123", "client certificate password= [REDACTED]")]
    [DataRow("pfx password=pfx_pass", "pfx password= [REDACTED]")]
    public void SecretKeyValueRules_ShouldMaskCertificateSecrets(string input, string expected)
    {
        // Arrange
        var provider = new SecretKeyValueRulesProvider();

        // Act
        var result = SanitizerTestHelper.ApplyRules(input, provider.GetRules());

        // Assert
        Assert.AreEqual(expected, result);
    }

    [DataTestMethod]
    [DataRow("aws access key id=AKIAIOSFODNN7EXAMPLE", "aws access key id= [REDACTED]")]
    [DataRow("aws secret access key=wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY", "aws secret access key= [REDACTED]")]
    [DataRow("aws session token=session_token_value", "aws session token= [REDACTED]")]
    public void SecretKeyValueRules_ShouldMaskAwsKeys(string input, string expected)
    {
        // Arrange
        var provider = new SecretKeyValueRulesProvider();

        // Act
        var result = SanitizerTestHelper.ApplyRules(input, provider.GetRules());

        // Assert
        Assert.AreEqual(expected, result);
    }

    [DataTestMethod]
    [DataRow("password=\"complexPassword123!\"", "password= \"[REDACTED]\"")]
    [DataRow("api-key='secret-key'", "api-key= '[REDACTED]'")]
    [DataRow("token=\"bearer_token_value\"", "token= \"[REDACTED]\"")]
    public void SecretKeyValueRules_ShouldPreserveQuotesAroundRedactedValue(string input, string expected)
    {
        // Arrange
        var provider = new SecretKeyValueRulesProvider();

        // Act
        var result = SanitizerTestHelper.ApplyRules(input, provider.GetRules());

        // Assert
        Assert.AreEqual(expected, result);
    }

    [DataTestMethod]
    [DataRow("PASSWORD=secret", "PASSWORD= [REDACTED]")]
    [DataRow("Api-Key=key123", "Api-Key= [REDACTED]")]
    [DataRow("CLIENT_ID=client123", "CLIENT_ID= [REDACTED]")]
    [DataRow("Pwd=pass123", "Pwd= [REDACTED]")]
    public void SecretKeyValueRules_ShouldBeCaseInsensitive(string input, string expected)
    {
        // Arrange
        var provider = new SecretKeyValueRulesProvider();

        // Act
        var result = SanitizerTestHelper.ApplyRules(input, provider.GetRules());

        // Assert
        Assert.AreEqual(expected, result);
    }

    [DataTestMethod]
    [DataRow("regularKey=regularValue", "regularKey=regularValue")]
    [DataRow("config=myConfig", "config=myConfig")]
    [DataRow("hostname=server.example.com", "hostname=server.example.com")]
    [DataRow("port=8080", "port=8080")]
    public void SecretKeyValueRules_ShouldNotRedactNonSecretKeyValuePairs(string input, string expected)
    {
        // Arrange
        var provider = new SecretKeyValueRulesProvider();

        // Act
        var result = SanitizerTestHelper.ApplyRules(input, provider.GetRules());

        // Assert
        Assert.AreEqual(expected, result);
    }

    [DataTestMethod]
    [DataRow("password:secret123", "password: [REDACTED]")]
    [DataRow("api key:api_key_value", "api key: [REDACTED]")]
    [DataRow("client_secret:secret_value", "client_secret: [REDACTED]")]
    public void SecretKeyValueRules_ShouldSupportColonSeparator(string input, string expected)
    {
        // Arrange
        var provider = new SecretKeyValueRulesProvider();

        // Act
        var result = SanitizerTestHelper.ApplyRules(input, provider.GetRules());

        // Assert
        Assert.AreEqual(expected, result);
    }

    [DataTestMethod]
    [DataRow("password = secret123", "password= [REDACTED]")]
    [DataRow("api key  =  api_key_value", "api key= [REDACTED]")]
    [DataRow("token : token_value", "token: [REDACTED]")]
    public void SecretKeyValueRules_ShouldHandleWhitespace(string input, string expected)
    {
        // Arrange
        var provider = new SecretKeyValueRulesProvider();

        // Act
        var result = SanitizerTestHelper.ApplyRules(input, provider.GetRules());

        // Assert
        Assert.AreEqual(expected, result);
    }

    [DataTestMethod]
    [DataRow("password=secret API_KEY=key config=myConfig", "password= [REDACTED] API_KEY= [REDACTED] config=myConfig")]
    [DataRow("client_id=id123 name=admin pwd=pass123", "client_id= [REDACTED] name=admin pwd= [REDACTED]")]
    public void SecretKeyValueRules_ShouldHandleMultipleKeyValuePairsInSingleString(string input, string expected)
    {
        // Arrange
        var provider = new SecretKeyValueRulesProvider();

        // Act
        var result = SanitizerTestHelper.ApplyRules(input, provider.GetRules());

        // Assert
        Assert.AreEqual(expected, result);
    }

    [DataTestMethod]
    [DataRow("cosmos db key=cosmos_key", "cosmos db key= [REDACTED]")]
    [DataRow("service principal secret=sp_secret", "service principal secret= [REDACTED]")]
    [DataRow("shared access signature=sas_signature", "shared access signature= [REDACTED]")]
    public void SecretKeyValueRules_ShouldMaskServiceSpecificSecrets(string input, string expected)
    {
        // Arrange
        var provider = new SecretKeyValueRulesProvider();

        // Act
        var result = SanitizerTestHelper.ApplyRules(input, provider.GetRules());

        // Assert
        Assert.AreEqual(expected, result);
    }
}
