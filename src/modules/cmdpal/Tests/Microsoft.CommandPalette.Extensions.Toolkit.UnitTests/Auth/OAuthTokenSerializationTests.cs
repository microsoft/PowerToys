// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CommandPalette.Extensions.Toolkit.UnitTests.Auth;

[TestClass]
public class OAuthTokenSerializationTests
{
    [TestMethod]
    public void RoundTrip_PreservesAllFields()
    {
        var expiresAt = DateTimeOffset.UtcNow.AddHours(1);
        var original = new OAuthToken
        {
            AccessToken = "access-123",
            RefreshToken = "refresh-456",
            TokenType = "Bearer",
            Scope = "read write",
            IdToken = "id-789",
            ExpiresAt = expiresAt,
        };

        var json = OAuthTokenSerialization.Serialize(original);
        var restored = OAuthTokenSerialization.Deserialize(json);

        Assert.IsNotNull(restored);
        Assert.AreEqual(original.AccessToken, restored!.AccessToken);
        Assert.AreEqual(original.RefreshToken, restored.RefreshToken);
        Assert.AreEqual(original.TokenType, restored.TokenType);
        Assert.AreEqual(original.Scope, restored.Scope);
        Assert.AreEqual(original.IdToken, restored.IdToken);
        Assert.IsNotNull(restored.ExpiresAt);
        Assert.AreEqual(expiresAt.ToUnixTimeSeconds(), restored.ExpiresAt!.Value.ToUnixTimeSeconds());
    }

    [TestMethod]
    public void RoundTrip_OmitsNullOptionalFields()
    {
        var original = new OAuthToken { AccessToken = "only-access" };

        var json = OAuthTokenSerialization.Serialize(original);
        var restored = OAuthTokenSerialization.Deserialize(json);

        Assert.IsNotNull(restored);
        Assert.AreEqual("only-access", restored!.AccessToken);
        Assert.IsNull(restored.RefreshToken);
        Assert.IsNull(restored.TokenType);
        Assert.IsNull(restored.Scope);
        Assert.IsNull(restored.IdToken);
        Assert.IsNull(restored.ExpiresAt);
    }

    [TestMethod]
    public void Deserialize_InvalidJson_ReturnsNull()
    {
        Assert.IsNull(OAuthTokenSerialization.Deserialize("not json"));
        Assert.IsNull(OAuthTokenSerialization.Deserialize("[1,2,3]"));
    }
}
