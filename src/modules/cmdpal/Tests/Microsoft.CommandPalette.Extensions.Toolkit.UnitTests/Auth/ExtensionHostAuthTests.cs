// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.Foundation;

namespace Microsoft.CommandPalette.Extensions.Toolkit.UnitTests.Auth;

[TestClass]
public class ExtensionHostAuthTests
{
    // A host that implements only IExtensionHost (no IExtensionHost2), i.e. an
    // older Command Palette that predates the auth broker.
    private sealed class LegacyHost : IExtensionHost
    {
        public IAsyncAction ShowStatus(IStatusMessage message, StatusContext context) => null!;

        public IAsyncAction HideStatus(IStatusMessage message) => null!;

        public IAsyncAction LogMessage(ILogMessage message) => null!;
    }

    [TestMethod]
    public void SupportsAuthorization_FalseForLegacyHost()
    {
        ExtensionHost.Initialize(new LegacyHost());

        Assert.IsFalse(ExtensionHost.SupportsAuthorization);
    }

    [TestMethod]
    public async Task RequestAuthorizationAsync_LegacyHost_ThrowsNotSupported()
    {
        ExtensionHost.Initialize(new LegacyHost());

        var request = new AuthorizationRequest
        {
            AuthorizationEndpoint = "https://example.test/authorize",
        };

        await Assert.ThrowsExceptionAsync<System.NotSupportedException>(
            () => ExtensionHost.RequestAuthorizationAsync(request));
    }
}
