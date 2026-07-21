// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CommandPalette.Extensions.Toolkit.UnitTests.Auth;

[TestClass]
public class OAuthClientParametersTests
{
    [TestMethod]
    public void BuildAuthorizationParameters_IncludesPkceAndCoreParams()
    {
        var client = new OAuthClient
        {
            ClientId = "my-client",
            AuthorizationEndpoint = "https://example.test/authorize",
            TokenEndpoint = "https://example.test/token",
            Scopes = new[] { "openid", "profile" },
        };

        var parameters = client.BuildAuthorizationParameters("CHALLENGE");

        Assert.AreEqual("code", parameters["response_type"]);
        Assert.AreEqual("my-client", parameters["client_id"]);
        Assert.AreEqual("CHALLENGE", parameters["code_challenge"]);
        Assert.AreEqual("S256", parameters["code_challenge_method"]);
        Assert.AreEqual("openid profile", parameters["scope"]);

        // The host injects redirect_uri and state; the client must not set them.
        Assert.IsFalse(parameters.ContainsKey("redirect_uri"));
        Assert.IsFalse(parameters.ContainsKey("state"));
    }

    [TestMethod]
    public void BuildAuthorizationParameters_OmitsScopeWhenEmpty()
    {
        var client = new OAuthClient
        {
            ClientId = "c",
            AuthorizationEndpoint = "https://example.test/authorize",
            TokenEndpoint = "https://example.test/token",
        };

        var parameters = client.BuildAuthorizationParameters("X");

        Assert.IsFalse(parameters.ContainsKey("scope"));
    }

    [TestMethod]
    public void BuildAuthorizationParameters_MergesAdditionalParameters()
    {
        var client = new OAuthClient
        {
            ClientId = "c",
            AuthorizationEndpoint = "https://example.test/authorize",
            TokenEndpoint = "https://example.test/token",
            AdditionalAuthorizationParameters = new Dictionary<string, string>
            {
                ["prompt"] = "consent",
                ["audience"] = "api://resource",
            },
        };

        var parameters = client.BuildAuthorizationParameters("X");

        Assert.AreEqual("consent", parameters["prompt"]);
        Assert.AreEqual("api://resource", parameters["audience"]);
    }
}
