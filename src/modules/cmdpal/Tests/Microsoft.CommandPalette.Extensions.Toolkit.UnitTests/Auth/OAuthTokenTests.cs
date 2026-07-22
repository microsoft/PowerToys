// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CommandPalette.Extensions.Toolkit.UnitTests.Auth;

[TestClass]
public class OAuthTokenTests
{
    [TestMethod]
    public void IsExpired_NullExpiry_IsNeverExpired()
    {
        var token = new OAuthToken { AccessToken = "a" };

        Assert.IsFalse(token.IsExpired());
    }

    [TestMethod]
    public void IsExpired_FutureExpiryBeyondSkew_IsNotExpired()
    {
        var token = new OAuthToken
        {
            AccessToken = "a",
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(10),
        };

        Assert.IsFalse(token.IsExpired());
    }

    [TestMethod]
    public void IsExpired_PastExpiry_IsExpired()
    {
        var token = new OAuthToken
        {
            AccessToken = "a",
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1),
        };

        Assert.IsTrue(token.IsExpired());
    }

    [TestMethod]
    public void IsExpired_WithinDefaultSkew_IsExpired()
    {
        // 30 seconds out, default skew is 60 seconds -> considered expired.
        var token = new OAuthToken
        {
            AccessToken = "a",
            ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(30),
        };

        Assert.IsTrue(token.IsExpired());
    }

    [TestMethod]
    public void IsExpired_CustomSkew_IsRespected()
    {
        var token = new OAuthToken
        {
            AccessToken = "a",
            ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(30),
        };

        // With a tiny skew, 30s of remaining life is not yet expired.
        Assert.IsFalse(token.IsExpired(TimeSpan.FromSeconds(5)));
    }
}
