// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.CmdPal.UI.ViewModels.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public class JSExtensionManifestTests
{
    [TestMethod]
    public async Task LoadFromFileAsync_ReturnsManifest_WhenCmdPalSectionExists()
    {
        var packageJson = """
            {
              "name": "contoso.hello",
              "version": "1.0.0",
              "main": "dist/index.js",
              "cmdpal": {
                "displayName": "Contoso Hello",
                "publisher": "Contoso"
              }
            }
            """;

        var path = WriteTempPackageJson(packageJson);
        try
        {
            var manifest = await JSExtensionManifest.LoadFromFileAsync(path);
            Assert.IsNotNull(manifest);
            Assert.AreEqual("contoso.hello", manifest.Name);
            Assert.AreEqual("dist/index.js", manifest.Main);
            Assert.AreEqual("Contoso Hello", manifest.DisplayName);
        }
        finally
        {
            TryDeleteDirectory(Path.GetDirectoryName(path));
        }
    }

    [TestMethod]
    public async Task LoadFromFileAsync_UsesCmdPalMain_WhenSpecified()
    {
        var packageJson = """
            {
              "name": "contoso.override",
              "version": "1.0.0",
              "main": "dist/default.js",
              "cmdpal": {
                "main": "dist/cmdpal.js"
              }
            }
            """;

        var path = WriteTempPackageJson(packageJson);
        try
        {
            var manifest = await JSExtensionManifest.LoadFromFileAsync(path);
            Assert.IsNotNull(manifest);
            Assert.AreEqual("dist/cmdpal.js", manifest.Main);
        }
        finally
        {
            TryDeleteDirectory(Path.GetDirectoryName(path));
        }
    }

    [TestMethod]
    public async Task LoadFromFileAsync_ReturnsNull_WhenCmdPalSectionMissing()
    {
        var packageJson = """
            {
              "name": "contoso.invalid",
              "version": "1.0.0",
              "main": "dist/index.js"
            }
            """;

        var path = WriteTempPackageJson(packageJson);
        try
        {
            var manifest = await JSExtensionManifest.LoadFromFileAsync(path);
            Assert.IsNull(manifest);
        }
        finally
        {
            TryDeleteDirectory(Path.GetDirectoryName(path));
        }
    }

    [TestMethod]
    public async Task LoadFromFileAsync_ReturnsNull_WhenNameMissing()
    {
        var packageJson = """
            {
              "version": "1.0.0",
              "main": "dist/index.js",
              "cmdpal": {
                "displayName": "Missing Name"
              }
            }
            """;

        var path = WriteTempPackageJson(packageJson);
        try
        {
            var manifest = await JSExtensionManifest.LoadFromFileAsync(path);
            Assert.IsNull(manifest);
        }
        finally
        {
            TryDeleteDirectory(Path.GetDirectoryName(path));
        }
    }

    private static string WriteTempPackageJson(string content)
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), "cmdpal-js-manifest-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        var path = Path.Combine(tempDirectory, "package.json");
        File.WriteAllText(path, content);
        return path;
    }

    private static void TryDeleteDirectory(string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            return;
        }

        try
        {
            Directory.Delete(directory, true);
        }
        catch
        {
        }
    }
}
