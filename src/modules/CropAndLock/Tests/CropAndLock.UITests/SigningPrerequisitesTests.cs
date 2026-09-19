// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CropAndLock.UITests
{
    [TestClass]
    [TestCategory("CropAndLock")]
    public sealed class SigningPrerequisitesTests
    {
        [TestMethod]
        public void MissingLocalSigningIsInconclusive()
        {
            foreach (var signature in MissingSignatures())
            {
                Assert.ThrowsException<AssertInconclusiveException>(() =>
                    SigningPrerequisites.RequireSignature(signature, true, false, "Prepare local signing."));
            }
        }

        [TestMethod]
        public void MissingCiSigningFails()
        {
            foreach (var signature in MissingSignatures())
            {
                Assert.ThrowsException<AssertFailedException>(() =>
                    SigningPrerequisites.RequireSignature(signature, true, true, "CI must prepare signing."));
            }
        }

        [TestMethod]
        public void TrustedMicrosoftSigningAllowsTheScenario()
        {
            var signature = new SigningPrerequisites.Signature("Valid", "Verified.", "Microsoft Corporation", true);
            SigningPrerequisites.RequireSignature(signature, true, false, "Settings IPC.");
            SigningPrerequisites.RequireSignature(signature, true, true, "Settings IPC.");
        }

        [TestMethod]
        public void CorruptSignaturesAreNeverSkipped()
        {
            var signature = new SigningPrerequisites.Signature("HashMismatch", "File changed after signing.", "Microsoft Corporation", true);
            Assert.ThrowsException<AssertFailedException>(() =>
                SigningPrerequisites.RequireSignature(signature, false, false, "Corrupt fixture."));
        }

        private static SigningPrerequisites.Signature[] MissingSignatures() =>
        [
            new("NotSigned", "Unsigned fixture.", string.Empty, false),
            new("NotTrusted", "Untrusted signer.", "Microsoft Corporation", false),
            new("UnknownError", "Trust verification unavailable.", "Microsoft Corporation", false),
            new("Valid", "Different publisher.", "Another publisher", true),
            new("Valid", "Only current-user trust.", "Microsoft Corporation", false),
        ];
    }
}
