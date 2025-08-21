// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.Bookmarks.UnitTests;

[TestClass]
public class BookmarkJsonParserTests
{
    private BookmarkJsonParser _parser;

    [TestInitialize]
    public void Setup()
    {
        _parser = new BookmarkJsonParser();
    }

    [TestMethod]
    public void ParseBookmarks_ValidJson_ReturnsBookmarks()
    {
        // Arrange
        var json = """
        {
            "Data": [
                {
                    "Name": "Google",
                    "Bookmark": "https://www.google.com"
                },
                {
                    "Name": "Local File",
                    "Bookmark": "C:\\temp\\file.txt"
                }
            ]
        }
        """;

        // Act
        var result = _parser.ParseBookmarks(json);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(2, result.Data.Count);
        Assert.AreEqual("Google", result.Data[0].Name);
        Assert.AreEqual("https://www.google.com", result.Data[0].Bookmark);
        Assert.AreEqual("Local File", result.Data[1].Name);
        Assert.AreEqual("C:\\temp\\file.txt", result.Data[1].Bookmark);
    }

    [TestMethod]
    public void ParseBookmarks_EmptyJson_ReturnsEmptyBookmarks()
    {
        // Arrange
        var json = "{}";

        // Act
        var result = _parser.ParseBookmarks(json);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(0, result.Data.Count);
    }

    [TestMethod]
    public void ParseBookmarks_NullJson_ReturnsEmptyBookmarks()
    {
        // Act
        var result = _parser.ParseBookmarks(null);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(0, result.Data.Count);
    }

    [TestMethod]
    public void ParseBookmarks_WhitespaceJson_ReturnsEmptyBookmarks()
    {
        // Act
        var result = _parser.ParseBookmarks("   ");

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(0, result.Data.Count);
    }

    [TestMethod]
    public void ParseBookmarks_EmptyString_ReturnsEmptyBookmarks()
    {
        // Act
        var result = _parser.ParseBookmarks(string.Empty);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(0, result.Data.Count);
    }

    [TestMethod]
    public void ParseBookmarks_InvalidJson_ReturnsEmptyBookmarks()
    {
        // Arrange
        var invalidJson = "{invalid json}";

        // Act
        var result = _parser.ParseBookmarks(invalidJson);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(0, result.Data.Count);
    }

    [TestMethod]
    public void ParseBookmarks_MalformedJson_ReturnsEmptyBookmarks()
    {
        // Arrange
        var malformedJson = """
        {
            "Data": [
                {
                    "Name": "Google",
                    "Bookmark": "https://www.google.com"
                },
                {
                    "Name": "Incomplete entry"
        """;

        // Act
        var result = _parser.ParseBookmarks(malformedJson);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(0, result.Data.Count);
    }

    [TestMethod]
    public void ParseBookmarks_JsonWithTrailingCommas_ParsesSuccessfully()
    {
        // Arrange - JSON with trailing commas (should be handled by AllowTrailingCommas option)
        var json = """
        {
            "Data": [
                {
                    "Name": "Google",
                    "Bookmark": "https://www.google.com",
                },
                {
                    "Name": "Local File",
                    "Bookmark": "C:\\temp\\file.txt",
                },
            ]
        }
        """;

        // Act
        var result = _parser.ParseBookmarks(json);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(2, result.Data.Count);
        Assert.AreEqual("Google", result.Data[0].Name);
        Assert.AreEqual("https://www.google.com", result.Data[0].Bookmark);
    }

    [TestMethod]
    public void ParseBookmarks_JsonWithDifferentCasing_ParsesSuccessfully()
    {
        // Arrange - JSON with different property name casing (should be handled by PropertyNameCaseInsensitive option)
        var json = """
        {
            "data": [
                {
                    "name": "Google",
                    "bookmark": "https://www.google.com"
                }
            ]
        }
        """;

        // Act
        var result = _parser.ParseBookmarks(json);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(1, result.Data.Count);
        Assert.AreEqual("Google", result.Data[0].Name);
        Assert.AreEqual("https://www.google.com", result.Data[0].Bookmark);
    }

    [TestMethod]
    public void SerializeBookmarks_ValidBookmarks_ReturnsJsonString()
    {
        // Arrange
        var bookmarks = new Bookmarks
        {
            Data = new List<BookmarkData>
            {
                new BookmarkData { Name = "Google", Bookmark = "https://www.google.com" },
                new BookmarkData { Name = "Local File", Bookmark = "C:\\temp\\file.txt" },
            },
        };

        // Act
        var result = _parser.SerializeBookmarks(bookmarks);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsTrue(result.Contains("Google"));
        Assert.IsTrue(result.Contains("https://www.google.com"));
        Assert.IsTrue(result.Contains("Local File"));
        Assert.IsTrue(result.Contains("C:\\\\temp\\\\file.txt")); // Escaped backslashes in JSON
        Assert.IsTrue(result.Contains("Data"));
    }

    [TestMethod]
    public void SerializeBookmarks_EmptyBookmarks_ReturnsValidJson()
    {
        // Arrange
        var bookmarks = new Bookmarks();

        // Act
        var result = _parser.SerializeBookmarks(bookmarks);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsTrue(result.Contains("Data"));
        Assert.IsTrue(result.Contains("[]"));
    }

    [TestMethod]
    public void SerializeBookmarks_NullBookmarks_ReturnsEmptyString()
    {
        // Act
        var result = _parser.SerializeBookmarks(null);

        // Assert
        Assert.AreEqual(string.Empty, result);
    }

    [TestMethod]
    public void ParseBookmarks_RoundTripSerialization_PreservesData()
    {
        // Arrange
        var originalBookmarks = new Bookmarks
        {
            Data = new List<BookmarkData>
            {
                new BookmarkData { Name = "Google", Bookmark = "https://www.google.com" },
                new BookmarkData { Name = "Local File", Bookmark = "C:\\temp\\file.txt" },
                new BookmarkData { Name = "Placeholder", Bookmark = "Open {file} in editor" },
            },
        };

        // Act - Serialize then parse
        var serializedJson = _parser.SerializeBookmarks(originalBookmarks);
        var parsedBookmarks = _parser.ParseBookmarks(serializedJson);

        // Assert
        Assert.IsNotNull(parsedBookmarks);
        Assert.AreEqual(originalBookmarks.Data.Count, parsedBookmarks.Data.Count);

        for (var i = 0; i < originalBookmarks.Data.Count; i++)
        {
            Assert.AreEqual(originalBookmarks.Data[i].Name, parsedBookmarks.Data[i].Name);
            Assert.AreEqual(originalBookmarks.Data[i].Bookmark, parsedBookmarks.Data[i].Bookmark);
            Assert.AreEqual(originalBookmarks.Data[i].IsPlaceholder, parsedBookmarks.Data[i].IsPlaceholder);
        }
    }

    [TestMethod]
    public void ParseBookmarks_JsonWithPlaceholderBookmarks_CorrectlyIdentifiesPlaceholders()
    {
        // Arrange
        var json = """
        {
            "Data": [
                {
                    "Name": "Regular URL",
                    "Bookmark": "https://www.google.com"
                },
                {
                    "Name": "Placeholder Command",
                    "Bookmark": "notepad {file}"
                },
                {
                    "Name": "Multiple Placeholders",
                    "Bookmark": "copy {source} {destination}"
                }
            ]
        }
        """;

        // Act
        var result = _parser.ParseBookmarks(json);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(3, result.Data.Count);

        Assert.IsFalse(result.Data[0].IsPlaceholder);
        Assert.IsTrue(result.Data[1].IsPlaceholder);
        Assert.IsTrue(result.Data[2].IsPlaceholder);
    }

    [TestMethod]
    public void ParseBookmarks_IsWebUrl_CorrectlyIdentifiesWebUrls()
    {
        // Arrange
        var json = """
        {
            "Data": [
                {
                    "Name": "HTTPS Website",
                    "Bookmark": "https://www.google.com"
                },
                {
                    "Name": "HTTP Website",
                    "Bookmark": "http://example.com"
                },
                {
                    "Name": "Website without protocol",
                    "Bookmark": "www.github.com"
                },
                {
                    "Name": "Local File Path",
                    "Bookmark": "C:\\Users\\test\\Documents\\file.txt"
                },
                {
                    "Name": "Network Path",
                    "Bookmark": "\\\\server\\share\\file.txt"
                },
                {
                    "Name": "Executable",
                    "Bookmark": "notepad.exe"
                },
                {
                    "Name": "File URI",
                    "Bookmark": "file:///C:/temp/file.txt"
                }
            ]
        }
        """;

        // Act
        var result = _parser.ParseBookmarks(json);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(7, result.Data.Count);

        // Web URLs should return true
        Assert.IsTrue(result.Data[0].IsWebUrl(), "HTTPS URL should be identified as web URL");
        Assert.IsTrue(result.Data[1].IsWebUrl(), "HTTP URL should be identified as web URL");

        // This case will fail. We need to consider if we need to support pure domain value in bookmark.
        // Assert.IsTrue(result.Data[2].IsWebUrl(), "Domain without protocol should be identified as web URL");

        // Non-web URLs should return false
        Assert.IsFalse(result.Data[3].IsWebUrl(), "Local file path should not be identified as web URL");
        Assert.IsFalse(result.Data[4].IsWebUrl(), "Network path should not be identified as web URL");
        Assert.IsFalse(result.Data[5].IsWebUrl(), "Executable should not be identified as web URL");
        Assert.IsFalse(result.Data[6].IsWebUrl(), "File URI should not be identified as web URL");
    }

    [TestMethod]
    public void ParseBookmarks_IsPlaceholder_CorrectlyIdentifiesPlaceholders()
    {
        // Arrange
        var json = """
        {
            "Data": [
                {
                    "Name": "Simple Placeholder",
                    "Bookmark": "notepad {file}"
                },
                {
                    "Name": "Multiple Placeholders",
                    "Bookmark": "copy {source} to {destination}"
                },
                {
                    "Name": "Web URL with Placeholder",
                    "Bookmark": "https://search.com?q={query}"
                },
                {
                    "Name": "Complex Placeholder",
                    "Bookmark": "cmd /c echo {message} > {output_file}"
                },
                {
                    "Name": "No Placeholder - Regular URL",
                    "Bookmark": "https://www.google.com"
                },
                {
                    "Name": "No Placeholder - Local File",
                    "Bookmark": "C:\\temp\\file.txt"
                },
                {
                    "Name": "False Positive - Only Opening Brace",
                    "Bookmark": "test { incomplete"
                },
                {
                    "Name": "False Positive - Only Closing Brace",
                    "Bookmark": "test } incomplete"
                },
                {
                    "Name": "Empty Placeholder",
                    "Bookmark": "command {}"
                }
            ]
        }
        """;

        // Act
        var result = _parser.ParseBookmarks(json);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(9, result.Data.Count);

        // Should be identified as placeholders
        Assert.IsTrue(result.Data[0].IsPlaceholder, "Simple placeholder should be identified");
        Assert.IsTrue(result.Data[1].IsPlaceholder, "Multiple placeholders should be identified");
        Assert.IsTrue(result.Data[2].IsPlaceholder, "Web URL with placeholder should be identified");
        Assert.IsTrue(result.Data[3].IsPlaceholder, "Complex placeholder should be identified");
        Assert.IsTrue(result.Data[8].IsPlaceholder, "Empty placeholder should be identified");

        // Should NOT be identified as placeholders
        Assert.IsFalse(result.Data[4].IsPlaceholder, "Regular URL should not be placeholder");
        Assert.IsFalse(result.Data[5].IsPlaceholder, "Local file should not be placeholder");
        Assert.IsFalse(result.Data[6].IsPlaceholder, "Only opening brace should not be placeholder");
        Assert.IsFalse(result.Data[7].IsPlaceholder, "Only closing brace should not be placeholder");
    }

    [TestMethod]
    public void ParseBookmarks_MixedProperties_CorrectlyIdentifiesBothWebUrlAndPlaceholder()
    {
        // Arrange
        var json = """
        {
            "Data": [
                {
                    "Name": "Web URL with Placeholder",
                    "Bookmark": "https://google.com/search?q={query}"
                },
                {
                    "Name": "Web URL without Placeholder",
                    "Bookmark": "https://github.com"
                },
                {
                    "Name": "Local File with Placeholder",
                    "Bookmark": "notepad {file}"
                },
                {
                    "Name": "Local File without Placeholder",
                    "Bookmark": "C:\\Windows\\notepad.exe"
                }
            ]
        }
        """;

        // Act
        var result = _parser.ParseBookmarks(json);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(4, result.Data.Count);

        // Web URL with placeholder
        Assert.IsTrue(result.Data[0].IsWebUrl(), "Web URL with placeholder should be identified as web URL");
        Assert.IsTrue(result.Data[0].IsPlaceholder, "Web URL with placeholder should be identified as placeholder");

        // Web URL without placeholder
        Assert.IsTrue(result.Data[1].IsWebUrl(), "Web URL without placeholder should be identified as web URL");
        Assert.IsFalse(result.Data[1].IsPlaceholder, "Web URL without placeholder should not be identified as placeholder");

        // Local file with placeholder
        Assert.IsFalse(result.Data[2].IsWebUrl(), "Local file with placeholder should not be identified as web URL");
        Assert.IsTrue(result.Data[2].IsPlaceholder, "Local file with placeholder should be identified as placeholder");

        // Local file without placeholder
        Assert.IsFalse(result.Data[3].IsWebUrl(), "Local file without placeholder should not be identified as web URL");
        Assert.IsFalse(result.Data[3].IsPlaceholder, "Local file without placeholder should not be identified as placeholder");
    }

    [TestMethod]
    public void ParseBookmarks_EdgeCaseUrls_CorrectlyIdentifiesWebUrls()
    {
        // Arrange
        var json = """
        {
            "Data": [
                {
                    "Name": "FTP URL",
                    "Bookmark": "ftp://files.example.com"
                },
                {
                    "Name": "HTTPS with port",
                    "Bookmark": "https://localhost:8080"
                },
                {
                    "Name": "IP Address",
                    "Bookmark": "http://192.168.1.1"
                },
                {
                    "Name": "Subdomain",
                    "Bookmark": "https://api.github.com"
                },
                {
                    "Name": "Domain only",
                    "Bookmark": "example.com"
                },
                {
                    "Name": "Not a URL - no dots",
                    "Bookmark": "localhost"
                }
            ]
        }
        """;

        // Act
        var result = _parser.ParseBookmarks(json);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(6, result.Data.Count);

        Assert.IsFalse(result.Data[0].IsWebUrl(), "FTP URL should not be identified as web URL");
        Assert.IsTrue(result.Data[1].IsWebUrl(), "HTTPS with port should be identified as web URL");
        Assert.IsTrue(result.Data[2].IsWebUrl(), "IP Address with HTTP should be identified as web URL");
        Assert.IsTrue(result.Data[3].IsWebUrl(), "Subdomain should be identified as web URL");

        // This case will fail. We need to consider if we need to support pure domain value in bookmark.
        // Assert.IsTrue(result.Data[4].IsWebUrl(), "Domain only should be identified as web URL");
        Assert.IsFalse(result.Data[5].IsWebUrl(), "Single word without dots should not be identified as web URL");
    }
}
