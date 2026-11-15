// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO.Compression;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Workspaces.UITests
{
    internal static class PackagedFixturePrerequisites
    {
        internal const int NoSignature = unchecked((int)0x800B0100);
        internal const int UntrustedRoot = unchecked((int)0x800B0109);

        internal static void RequireSignature(string path, bool isInPipeline)
        {
            Assert.IsTrue(File.Exists(path), $"The fixture package was not staged: '{path}'. Build Workspaces.UITests.Next first.");
            using var archive = ZipFile.OpenRead(path);

            // Detect the ordinary unsigned developer build cheaply. A present signature is NOT a trust
            // decision: Windows still validates its integrity and certificate chain during deployment.
            if (archive.GetEntry("AppxSignature.p7x") is null)
            {
                throw MissingSigningException(path, NoSignature, isInPipeline);
            }
        }

        internal static bool IsMissingSigning(int errorCode) => errorCode is NoSignature or UntrustedRoot;

        internal static Exception MissingSigningException(string path, int errorCode, bool isInPipeline)
        {
            if (!IsMissingSigning(errorCode))
            {
                throw new ArgumentOutOfRangeException(nameof(errorCode), errorCode, "Not a missing signing prerequisite.");
            }

            var reason = errorCode == NoSignature ? "has no digital signature" : "does not chain to a trusted certificate";
            var message = $"The packaged Workspaces fixture '{path}' {reason} (0x{errorCode:X8}). " +
                "See doc\\devdocs\\modules\\workspaces.md, 'Optional packaged-fixture setup', for signing, trust, and cleanup instructions. " +
                "Rebuilding can replace signed packages; prepare signing again after rebuilding. " +
                "The tests never install certificate trust automatically.";

            if (isInPipeline)
            {
                return new AssertFailedException("CI/VM packaged coverage requires prepared signing. " + message);
            }

            return new AssertInconclusiveException("Packaged scenario skipped locally because signing setup is missing. " + message);
        }
    }
}
