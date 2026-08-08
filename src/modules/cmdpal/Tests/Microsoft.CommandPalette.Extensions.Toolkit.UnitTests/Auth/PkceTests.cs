// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CommandPalette.Extensions.Toolkit.UnitTests.Auth;

[TestClass]
public class PkceTests
{
    // RFC 7636 Appendix B known-answer vector.
    private const string RfcVerifier = "dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk";
    private const string RfcChallenge = "E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM";

    [TestMethod]
    public void ComputeChallenge_MatchesRfc7636Vector()
    {
        Assert.AreEqual(RfcChallenge, Pkce.ComputeChallenge(RfcVerifier));
    }

    [TestMethod]
    public void Generate_ProducesVerifierWithMatchingChallenge()
    {
        var (verifier, challenge) = Pkce.Generate();

        Assert.AreEqual(Pkce.ComputeChallenge(verifier), challenge);
    }

    [TestMethod]
    public void Generate_VerifierIsUrlSafeAndUnpadded()
    {
        var (verifier, challenge) = Pkce.Generate();

        // Base64url alphabet only, no padding.
        Assert.IsTrue(Regex.IsMatch(verifier, "^[A-Za-z0-9_-]+$"), verifier);
        Assert.IsTrue(Regex.IsMatch(challenge, "^[A-Za-z0-9_-]+$"), challenge);

        // 32 random bytes -> 43 base64url chars; SHA-256 -> 43 chars.
        Assert.AreEqual(43, verifier.Length);
        Assert.AreEqual(43, challenge.Length);
    }

    [TestMethod]
    public void Generate_ProducesUniqueValues()
    {
        var (v1, _) = Pkce.Generate();
        var (v2, _) = Pkce.Generate();

        Assert.AreNotEqual(v1, v2);
    }

    [TestMethod]
    public void Base64UrlEncode_StripsPaddingAndReplacesChars()
    {
        // 0xFB 0xFF -> base64 "+/8=" -> base64url "-_8".
        var encoded = Pkce.Base64UrlEncode(new byte[] { 0xFB, 0xFF });

        Assert.AreEqual("-_8", encoded);
    }
}
