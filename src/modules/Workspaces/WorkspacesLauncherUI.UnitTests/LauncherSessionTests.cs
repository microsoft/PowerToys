// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace WorkspacesLauncherUI.UnitTests
{
    [TestClass]
    public sealed class LauncherSessionTests
    {
        private static readonly string[] PartialSessionArguments = { "--launch-ui" };

        [DataTestMethod]
        [DataRow("--confirmation-only", true)]
        [DataRow("--launch-ui", false)]
        public void ScopedSessionCanonicalizesOperationIdAndPreservesHostMode(string mode, bool confirmationOnly)
        {
            const string id = "{A1111111-B111-C111-D111-E11111111111}";
            Assert.IsTrue(LauncherSession.TryParse(new[] { mode, id, "1234" }, out var session));
            Assert.AreEqual(Guid.Parse(id).ToString("D"), session.OperationId);
            Assert.AreEqual(1234, session.OwnerPid);
            Assert.AreEqual(confirmationOnly, session.ConfirmationOnly);
        }

        [DataTestMethod]
        [DataRow("--unknown", "a1111111-b111-c111-d111-e11111111111", "1234")]
        [DataRow("--launch-ui", "00000000-0000-0000-0000-000000000000", "1234")]
        [DataRow("--launch-ui", "invalid", "1234")]
        [DataRow("--launch-ui", "a1111111-b111-c111-d111-e11111111111", "0")]
        [DataRow("--launch-ui", "a1111111-b111-c111-d111-e11111111111", "-1")]
        [DataRow("--launch-ui", "a1111111-b111-c111-d111-e11111111111", " 1234")]
        public void InvalidScopedSessionFailsClosed(string mode, string id, string owner)
        {
            Assert.IsFalse(LauncherSession.TryParse(new[] { mode, id, owner }, out _));
        }

        [TestMethod]
        public void LegacySessionIsAcceptedButPartialAndExtraArgumentsAreNot()
        {
            Assert.IsTrue(LauncherSession.TryParse(Array.Empty<string>(), out var session));
            Assert.AreEqual(string.Empty, session.OperationId);
            Assert.AreEqual(0, session.OwnerPid);
            Assert.IsFalse(LauncherSession.TryParse(PartialSessionArguments, out _));
            Assert.IsFalse(LauncherSession.TryParse(new[] { "--launch-ui", Guid.NewGuid().ToString(), "1234", "extra" }, out _));
        }
    }
}
