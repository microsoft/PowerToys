// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CommandPalette.Extensions.Toolkit.UnitTests.Auth;

[TestClass]
public class OAuthClientRefreshTests
{
    private static OAuthClient CreateClient(string tokenEndpoint) => new()
    {
        ClientId = "test-client",
        AuthorizationEndpoint = "https://example.test/authorize",
        TokenEndpoint = tokenEndpoint,
        Scopes = new[] { "read", "write" },
    };

    [TestMethod]
    public async Task RefreshAsync_ParsesSuccessResponse()
    {
        const string body = """
        {"access_token":"new-access","refresh_token":"new-refresh","token_type":"Bearer","scope":"read write","expires_in":3600}
        """;
        using var server = new MockHttpServer(200, body);
        var client = CreateClient(server.Url);

        var token = await client.RefreshAsync("old-refresh");

        Assert.AreEqual("new-access", token.AccessToken);
        Assert.AreEqual("new-refresh", token.RefreshToken);
        Assert.AreEqual("Bearer", token.TokenType);
        Assert.AreEqual("read write", token.Scope);
        Assert.IsNotNull(token.ExpiresAt);
        Assert.IsFalse(token.IsExpired());

        // The refresh_token grant must be posted with the caller's refresh token.
        Assert.IsNotNull(server.CapturedRequest);
        StringAssert.Contains(server.CapturedRequest!, "grant_type=refresh_token");
        StringAssert.Contains(server.CapturedRequest!, "refresh_token=old-refresh");
        StringAssert.Contains(server.CapturedRequest!, "client_id=test-client");
    }

    [TestMethod]
    public async Task RefreshAsync_HandlesExpiresInAsString()
    {
        const string body = """
        {"access_token":"a","token_type":"Bearer","expires_in":"1200"}
        """;
        using var server = new MockHttpServer(200, body);
        var client = CreateClient(server.Url);

        var token = await client.RefreshAsync("r");

        Assert.IsNotNull(token.ExpiresAt);
        Assert.IsFalse(token.IsExpired());
    }

    [TestMethod]
    public async Task RefreshAsync_ProviderErrorBody_ThrowsOAuthException()
    {
        const string body = """
        {"error":"invalid_grant","error_description":"The refresh token is expired."}
        """;
        using var server = new MockHttpServer(400, body);
        var client = CreateClient(server.Url);

        var ex = await Assert.ThrowsExceptionAsync<OAuthException>(() => client.RefreshAsync("r"));

        StringAssert.Contains(ex.Message, "expired");
    }

    [TestMethod]
    public async Task RefreshAsync_MissingAccessToken_ThrowsOAuthException()
    {
        const string body = """
        {"token_type":"Bearer"}
        """;
        using var server = new MockHttpServer(200, body);
        var client = CreateClient(server.Url);

        await Assert.ThrowsExceptionAsync<OAuthException>(() => client.RefreshAsync("r"));
    }

    [TestMethod]
    public async Task RefreshAsync_NonJsonResponse_ThrowsOAuthException()
    {
        using var server = new MockHttpServer(200, "this is not json");
        var client = CreateClient(server.Url);

        await Assert.ThrowsExceptionAsync<OAuthException>(() => client.RefreshAsync("r"));
    }
}
