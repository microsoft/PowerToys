// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.Text.Json;
using Microsoft.CmdPal.Common.ExtensionGallery.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public class GalleryManifestLocalizationTests
{
    [TestMethod]
    public void DeserializesLegacyLocalizedFields_UsingCurrentUiCultureFallback()
    {
        const string json = """
            {
              "id": "localized-extension",
              "title": { "en": "English title", "cs-cz": "Cesky nazev" },
              "description": { "en": "English description", "cs": "Cesky popis" },
              "author": { "name": "Author" },
              "installSources": [{ "type": "url", "uri": "https://example.com" }]
            }
            """;

        var previousCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = new CultureInfo("cs-CZ");
            var entry = JsonSerializer.Deserialize(json, GallerySerializationContext.Default.GalleryExtensionEntry);

            Assert.IsNotNull(entry);
            Assert.AreEqual("Cesky nazev", entry.Title);
            Assert.AreEqual("Cesky popis", entry.Description);
        }
        finally
        {
            CultureInfo.CurrentUICulture = previousCulture;
        }
    }

    [TestMethod]
    public void SerializesLocalizedFields_AsStringValues()
    {
        var entry = new GalleryExtensionEntry
        {
            Id = "string-extension",
            Title = "Simple title",
            Description = "Simple description",
            Author = new GalleryAuthor { Name = "Author" },
            InstallSources =
            [
                new GalleryInstallSource { Type = "url", Uri = "https://example.com" },
            ],
        };

        var json = JsonSerializer.Serialize(entry, GallerySerializationContext.Default.GalleryExtensionEntry);
        StringAssert.Contains(json, "\"Title\":\"Simple title\"");
        StringAssert.Contains(json, "\"Description\":\"Simple description\"");
        Assert.IsFalse(json.Contains("\"Title\":{"));
        Assert.IsFalse(json.Contains("\"Description\":{"));
    }
}
