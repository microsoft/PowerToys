// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text.Json;
using Microsoft.CmdPal.UI.ViewModels.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

/// <summary>
/// Verifies that the author-specified frozen value from the initialize handshake
/// metadata flows through the proxy (cp-B). When no metadata is supplied the wire
/// default is frozen; when the handshake provides a value it wins.
/// </summary>
[TestClass]
public class JSCommandProviderProxyFrozenTests
{
    private static JsonElement Parse(string json)
    {
        // The returned element must outlive this call, so parse into a document whose
        // lifetime is tied to the element via Clone rather than a disposed document.
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    [TestMethod]
    public void Frozen_DefaultsToTrue_WhenNoMetadata()
    {
        using var fake = new JSFakeExtension();
        var provider = new JSCommandProviderProxy(fake.Connection, new JSExtensionManifest { Name = "ext" });

        Assert.IsTrue(provider.Frozen);

        provider.Dispose();
    }

    [TestMethod]
    public void Frozen_HonorsCtorMetadata_False()
    {
        using var fake = new JSFakeExtension();
        var provider = new JSCommandProviderProxy(
            fake.Connection,
            new JSExtensionManifest { Name = "ext" },
            Parse("""{ "frozen": false }"""));

        Assert.IsFalse(provider.Frozen);

        provider.Dispose();
    }

    [TestMethod]
    public void Frozen_HonorsHandshakeMetadata_False()
    {
        using var fake = new JSFakeExtension();
        var provider = new JSCommandProviderProxy(fake.Connection, new JSExtensionManifest { Name = "ext" });

        provider.SetProviderMetadata(Parse("""{ "Frozen": false }"""));

        Assert.IsFalse(provider.Frozen);

        provider.Dispose();
    }

    [TestMethod]
    public void Frozen_HonorsHandshakeMetadata_True()
    {
        using var fake = new JSFakeExtension();
        var provider = new JSCommandProviderProxy(fake.Connection, new JSExtensionManifest { Name = "ext" });

        provider.SetProviderMetadata(Parse("""{ "frozen": true }"""));

        Assert.IsTrue(provider.Frozen);

        provider.Dispose();
    }
}
