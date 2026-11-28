// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using Microsoft.CmdPal.Common.ExtensionGallery.Models;

namespace Microsoft.CmdPal.Common.UnitTests.ExtensionGallery.Models;

[TestClass]
public class GalleryExtensionEntryTests
{
    private static readonly string[] ExpectedScreenshotUrls =
    [
        "https://example.com/screenshots/1.png",
        "https://example.com/screenshots/2.png",
    ];

    [DataTestMethod]
    [DataRow("{}")]
    [DataRow("{\"ScreenshotUrls\":[],\"InstallSources\":[],\"Tags\":[]}")]
    public void Deserialize_DefaultCollectionsRemainAttachedWhenEdited(string json)
    {
        var context = GallerySerializationContext.Default;
        var entry = JsonSerializer.Deserialize(json, context.GalleryExtensionEntry);

        Assert.IsNotNull(entry);
        Assert.IsNotNull(entry.ScreenshotUrls);
        Assert.IsNotNull(entry.InstallSources);
        Assert.IsNotNull(entry.Tags);
        Assert.AreEqual(0, entry.ScreenshotUrls.Count);
        Assert.AreEqual(0, entry.InstallSources.Count);
        Assert.AreEqual(0, entry.Tags.Count);
        entry.ScreenshotUrls.Add("https://example.com/screenshot.png");
        entry.InstallSources.Add(new GalleryInstallSource { Type = "url", Uri = "https://example.com/install" });
        entry.Tags.Add("productivity");

        var saved = JsonSerializer.Serialize(entry, context.GalleryExtensionEntry);
        var reloaded = JsonSerializer.Deserialize(saved, context.GalleryExtensionEntry);

        Assert.IsNotNull(reloaded);
        Assert.AreEqual("https://example.com/screenshot.png", reloaded.ScreenshotUrls[0]);
        Assert.AreEqual("https://example.com/install", reloaded.InstallSources[0].Uri);
        Assert.AreEqual("productivity", reloaded.Tags[0]);
    }

    [DataTestMethod]
    [DataRow("{}")]
    [DataRow("{\"Extensions\":[]}")]
    public void Deserialize_IndexDefaultRemainsAttachedWhenEdited(string json)
    {
        var context = GallerySerializationContext.Default;
        var index = JsonSerializer.Deserialize(json, context.GalleryRemoteIndex);

        Assert.IsNotNull(index);
        Assert.IsNotNull(index.Extensions);
        Assert.AreEqual(0, index.Extensions.Count);
        index.Extensions.Add(new GalleryExtensionEntry { Id = "added-extension" });

        var saved = JsonSerializer.Serialize(index, context.GalleryRemoteIndex);
        var reloaded = JsonSerializer.Deserialize(saved, context.GalleryRemoteIndex);

        Assert.IsNotNull(reloaded);
        Assert.AreEqual("added-extension", reloaded.Extensions[0].Id);
    }

    [TestMethod]
    public void Deserialize_ExplicitNullCollectionsKeepTheirExistingMeaning()
    {
        var context = GallerySerializationContext.Default;
        var entry = JsonSerializer.Deserialize("{\"ScreenshotUrls\":null,\"InstallSources\":null,\"Tags\":null}", context.GalleryExtensionEntry);
        var index = JsonSerializer.Deserialize("{\"Extensions\":null}", context.GalleryRemoteIndex);

        Assert.IsNotNull(entry);
        Assert.IsNotNull(index);
        Assert.IsNull(entry.ScreenshotUrls);
        Assert.IsNull(entry.InstallSources);
        Assert.IsNull(entry.Tags);
        Assert.IsNull(index.Extensions);
    }

    [TestMethod]
    public void Deserialize_UsesPlainStringManifestFields()
    {
        const string json = """
            {
              "id": "sample-extension",
              "title": "Sample title",
              "description": "Sample description",
              "readme": "README.md",
              "screenshotUrls": [
                "https://example.com/screenshots/1.png",
                "https://example.com/screenshots/2.png"
              ],
              "author": { "name": "Author" },
              "installSources": [{ "type": "url", "uri": "https://example.com" }]
            }
            """;

        var entry = JsonSerializer.Deserialize(json, GallerySerializationContext.Default.GalleryExtensionEntry);

        Assert.IsNotNull(entry);
        Assert.AreEqual("Sample title", entry.Title);
        Assert.AreEqual("Sample description", entry.Description);
        Assert.AreEqual("README.md", entry.Readme);
        CollectionAssert.AreEqual(ExpectedScreenshotUrls, entry.ScreenshotUrls);
    }

    [TestMethod]
    public void Deserialize_RejectsLegacyLocalizedObjectFields()
    {
        const string json = """
            {
              "id": "sample-extension",
              "title": { "en": "English title" },
              "description": "Sample description",
              "author": { "name": "Author" },
              "installSources": [{ "type": "url", "uri": "https://example.com" }]
            }
            """;

        Assert.ThrowsException<JsonException>(
            () => JsonSerializer.Deserialize(json, GallerySerializationContext.Default.GalleryExtensionEntry));
    }
}
