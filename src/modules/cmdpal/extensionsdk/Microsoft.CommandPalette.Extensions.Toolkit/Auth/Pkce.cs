// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Security.Cryptography;
using System.Text;

namespace Microsoft.CommandPalette.Extensions.Toolkit;

/// <summary>
/// Helpers for the PKCE (Proof Key for Code Exchange, RFC 7636) extension to the
/// OAuth 2.0 Authorization Code flow. The verifier is generated and kept inside
/// the extension process; only the S256 challenge is ever sent to the browser.
/// </summary>
public static class Pkce
{
    /// <summary>
    /// Generate a fresh PKCE verifier/challenge pair using the S256 method.
    /// </summary>
    /// <returns>
    /// A tuple of (verifier, challenge). Send <c>challenge</c> as
    /// <c>code_challenge</c> (with <c>code_challenge_method=S256</c>) on the
    /// authorization request, and send <c>verifier</c> as <c>code_verifier</c>
    /// when exchanging the authorization code for a token.
    /// </returns>
    public static (string Verifier, string Challenge) Generate()
    {
        // RFC 7636 allows a verifier of 43-128 chars. 32 random bytes -> 43
        // base64url chars, which is the recommended length.
        var bytes = RandomNumberGenerator.GetBytes(32);
        var verifier = Base64UrlEncode(bytes);
        var challenge = ComputeChallenge(verifier);
        return (verifier, challenge);
    }

    /// <summary>
    /// Compute the S256 code challenge for a given verifier:
    /// <c>base64url(SHA256(ASCII(verifier)))</c>.
    /// </summary>
    public static string ComputeChallenge(string verifier)
    {
        ArgumentNullException.ThrowIfNull(verifier);
        var hash = SHA256.HashData(Encoding.ASCII.GetBytes(verifier));
        return Base64UrlEncode(hash);
    }

    /// <summary>
    /// Base64url-encode without padding (RFC 4648 section 5), as required for
    /// PKCE values and other OAuth parameters.
    /// </summary>
    public static string Base64UrlEncode(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}
