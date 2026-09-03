// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CmdPal.JsonRpc;
using Microsoft.CmdPal.UI.ViewModels.Models;
using Microsoft.CommandPalette.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public class JSExtensionWrapperTests
{
    private static JSExtensionWrapper CreateWrapper(string? version = "1.2.3") => new(
        new JSExtensionManifest
        {
            Name = "test-ext",
            DisplayName = "Test Extension",
            Version = version,
            Publisher = "unit-test",
            Main = "index.js",
            EntryPointPath = Path.Combine(Path.GetTempPath(), "index.js"),
        },
        Path.Combine(Path.GetTempPath(), "test-ext"));

    [TestMethod]
    public void NewWrapper_IsHealthy_WithZeroCrashes()
    {
        var wrapper = CreateWrapper();
        Assert.IsTrue(wrapper.IsHealthy);
        Assert.AreEqual(0, wrapper.ConsecutiveCrashCount);
        Assert.IsFalse(wrapper.IsRunning());
    }

    [TestMethod]
    public void RecordUnexpectedExit_WithinThreshold_StaysHealthy()
    {
        var wrapper = CreateWrapper();

        for (var i = 1; i <= 3; i++)
        {
            Assert.AreEqual(i, wrapper.RecordUnexpectedExit());
        }

        Assert.IsTrue(wrapper.IsHealthy, "Three or fewer crashes should remain healthy.");
        Assert.AreEqual(3, wrapper.ConsecutiveCrashCount);
    }

    [TestMethod]
    public void RecordUnexpectedExit_AboveThreshold_BecomesUnhealthy()
    {
        var wrapper = CreateWrapper();

        for (var i = 0; i < 4; i++)
        {
            wrapper.RecordUnexpectedExit();
        }

        Assert.IsFalse(wrapper.IsHealthy, "More than three crashes should mark the extension unhealthy.");
        Assert.AreEqual(4, wrapper.ConsecutiveCrashCount);
    }

    [TestMethod]
    public void ResetCrashCount_RestoresHealthAndCount()
    {
        var wrapper = CreateWrapper();

        for (var i = 0; i < 5; i++)
        {
            wrapper.RecordUnexpectedExit();
        }

        Assert.IsFalse(wrapper.IsHealthy);

        wrapper.ResetCrashCount();

        Assert.IsTrue(wrapper.IsHealthy);
        Assert.AreEqual(0, wrapper.ConsecutiveCrashCount);
    }

    [TestMethod]
    public void Identity_DerivesFromManifest()
    {
        var wrapper = CreateWrapper();

        Assert.AreEqual("js!test-ext", wrapper.ExtensionUniqueId);
        Assert.AreEqual("js!test-ext", wrapper.PackageFamilyName);
        Assert.AreEqual("Test Extension", wrapper.ExtensionDisplayName);
        Assert.AreEqual("unit-test", wrapper.Publisher);
        Assert.IsTrue(wrapper.HasProviderType(ProviderType.Commands));
    }

    [TestMethod]
    public void Version_ParsesManifestVersion()
    {
        var wrapper = CreateWrapper("4.5.6");
        var version = wrapper.Version;

        Assert.AreEqual(4, version.Major);
        Assert.AreEqual(5, version.Minor);
        Assert.AreEqual(6, version.Build);
    }

    [TestMethod]
    public void Version_MissingVersion_DefaultsToOneZeroZero()
    {
        var wrapper = CreateWrapper(version: null);
        var version = wrapper.Version;

        Assert.AreEqual(1, version.Major);
        Assert.AreEqual(0, version.Minor);
        Assert.AreEqual(0, version.Build);
    }

    [TestMethod]
    public void GetExtensionObject_IsNull_ForJsExtensions()
    {
        var wrapper = CreateWrapper();
        Assert.IsNull(wrapper.GetExtensionObject());
    }

    [TestMethod]
    public void CreateCommandProviderProxy_UsesResolvedRelativeIcon()
    {
        var extensionDirectory = Path.Combine(Path.GetTempPath(), $"JSExtensionWrapperTests_{Guid.NewGuid():N}");
        var iconDirectory = Directory.CreateDirectory(Path.Combine(extensionDirectory, "assets"));
        var iconPath = Path.Combine(iconDirectory.FullName, "icon.png");
        File.WriteAllText(iconPath, "icon");

        try
        {
            using var wrapper = CreateWrapperWithIcon(extensionDirectory, @"assets\icon.png");
            using var input = new MemoryStream();
            using var output = new MemoryStream();
            using var connection = new JsonRpcConnection(input, output);
            using var provider = wrapper.CreateCommandProviderProxy(connection);

            Assert.AreEqual(Path.GetFullPath(iconPath), provider.Icon.Light.Icon);
        }
        finally
        {
            Directory.Delete(extensionDirectory, recursive: true);
        }
    }

    [TestMethod]
    public void CreateCommandProviderProxy_DoesNotUseRawRootedIcon()
    {
        var extensionDirectory = Path.Combine(Path.GetTempPath(), $"JSExtensionWrapperTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(extensionDirectory);
        var iconPath = Path.Combine(extensionDirectory, "icon.png");
        File.WriteAllText(iconPath, "icon");

        try
        {
            using var wrapper = CreateWrapperWithIcon(extensionDirectory, iconPath);
            using var input = new MemoryStream();
            using var output = new MemoryStream();
            using var connection = new JsonRpcConnection(input, output);
            using var provider = wrapper.CreateCommandProviderProxy(connection);

            Assert.AreEqual(string.Empty, provider.Icon.Light.Icon);
        }
        finally
        {
            Directory.Delete(extensionDirectory, recursive: true);
        }
    }

    private static JSExtensionWrapper CreateWrapperWithIcon(string extensionDirectory, string icon)
    {
        return new JSExtensionWrapper(
            new JSExtensionManifest
            {
                Name = "test-ext",
                DisplayName = "Test Extension",
                Version = "1.2.3",
                Publisher = "unit-test",
                Main = "index.js",
                EntryPointPath = Path.Combine(extensionDirectory, "index.js"),
                Icon = icon,
            },
            extensionDirectory);
    }

    [TestMethod]
    public async Task StartExtensionAsync_StopsBeforeLaunch_WhenCanceled()
    {
        var wrapper = CreateWrapper();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => wrapper.StartExtensionAsync(cancellation.Token));

        Assert.IsFalse(wrapper.IsRunning());
    }
}
