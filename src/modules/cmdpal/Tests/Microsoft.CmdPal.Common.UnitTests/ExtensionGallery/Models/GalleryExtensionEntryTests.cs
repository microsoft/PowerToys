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
