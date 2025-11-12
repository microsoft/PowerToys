// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

using Microsoft.CmdPal.Ext.Bookmarks.Persistence;
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
        var bookmarks = new BookmarksData
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
        var bookmarks = new BookmarksData();

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
        var originalBookmarks = new BookmarksData
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
    }

    [TestMethod]
    public void ParseBookmarks_MixedProperties_CorrectlyIdentifiesPlaceholder()
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
    }
}
