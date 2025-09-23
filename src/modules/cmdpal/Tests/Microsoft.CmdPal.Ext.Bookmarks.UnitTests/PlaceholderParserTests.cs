// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CmdPal.Ext.Bookmarks.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.Bookmarks.UnitTests;

[TestClass]
public class PlaceholderParserTests
{
    private IPlaceholderParser _parser;

    [TestInitialize]
    public void Setup()
    {
        _parser = new PlaceholderParser();
    }

    public static IEnumerable<object[]> ValidPlaceholderTestData =>
    [
        [
            "Hello {name}!",
                true,
                "Hello ",
                new[] { "name" }
        ],
        [
            "User {user_name} has {count} items",
                true,
                "User ",
                new[] { "user_name", "count" }
        ],
        [
            "Order {order-id} for {name} by {name}",
                true,
                "Order ",
                new[] { "order-id", "name" } // unique only
        ],
        [
            "{start} and {end}",
                true,
                string.Empty,
                new[] { "start", "end" }
        ],
        [
            "Number {123} and text {abc}",
                true,
                "Number ",
                new[] { "123", "abc" }
        ]
    ];

    public static IEnumerable<object[]> InvalidPlaceholderTestData =>
    [
        [string.Empty, false, string.Empty, Array.Empty<string>()],
        ["No placeholders here", false, "No placeholders here", Array.Empty<string>()],
        ["GUID: {550e8400-e29b-41d4-a716-446655440000}", false, "GUID: {550e8400-e29b-41d4-a716-446655440000}", Array.Empty<string>()],
        ["Invalid {user.name} placeholder", false, "Invalid {user.name} placeholder", Array.Empty<string>()],
        ["Empty {} placeholder", false, "Empty {} placeholder", Array.Empty<string>()],
        ["Unclosed {placeholder", false, "Unclosed {placeholder", Array.Empty<string>()],
        ["No opening brace placeholder}", false, "No opening brace placeholder}", Array.Empty<string>()],
        ["Invalid chars {user@domain}", false, "Invalid chars {user@domain}", Array.Empty<string>()],
        ["Spaces { name }", false, "Spaces { name }", Array.Empty<string>()]
    ];

    [TestMethod]
    [DynamicData(nameof(ValidPlaceholderTestData))]
    public void ParsePlaceholders_ValidInput_ReturnsExpectedResults(
        string input,
        bool expectedResult,
        string expectedHead,
        string[] expectedPlaceholderNames)
    {
        // Act
        var result = _parser.ParsePlaceholders(input, out var head, out var placeholders);

        // Assert
        Assert.AreEqual(expectedResult, result);
        Assert.AreEqual(expectedHead, head);
        Assert.AreEqual(expectedPlaceholderNames.Length, placeholders.Count);

        var actualNames = placeholders.Select(p => p.Name).ToArray();
        CollectionAssert.AreEquivalent(expectedPlaceholderNames, actualNames);
    }

    [TestMethod]
    [DynamicData(nameof(InvalidPlaceholderTestData))]
    public void ParsePlaceholders_InvalidInput_ReturnsExpectedResults(
        string input,
        bool expectedResult,
        string expectedHead,
        string[] expectedPlaceholderNames)
    {
        // Act
        var result = _parser.ParsePlaceholders(input, out var head, out var placeholders);

        // Assert
        Assert.AreEqual(expectedResult, result);
        Assert.AreEqual(expectedHead, head);
        Assert.AreEqual(expectedPlaceholderNames.Length, placeholders.Count);

        var actualNames = placeholders.Select(p => p.Name).ToArray();
        CollectionAssert.AreEquivalent(expectedPlaceholderNames, actualNames);
    }

    [TestMethod]
    public void ParsePlaceholders_NullInput_ThrowsArgumentNullException()
    {
        Assert.ThrowsException<ArgumentNullException>(() => _parser.ParsePlaceholders(null!, out _, out _));
    }

    [TestMethod]
    public void Placeholder_Equality_WorksCorrectly()
    {
        // Arrange
        var placeholder1 = new PlaceholderInfo("name");
        var placeholder2 = new PlaceholderInfo("name");
        var placeholder3 = new PlaceholderInfo("other");

        // Assert
        Assert.AreEqual(placeholder1, placeholder2);
        Assert.AreNotEqual(placeholder1, placeholder3);
        Assert.AreEqual(placeholder1.GetHashCode(), placeholder2.GetHashCode());
    }

    [TestMethod]
    public void Placeholder_ToString_ReturnsName()
    {
        // Arrange
        var placeholder = new PlaceholderInfo("userName");

        // Assert
        Assert.AreEqual("userName", placeholder.ToString());
    }

    [TestMethod]
    public void Placeholder_Constructor_ThrowsOnNull()
    {
        // Assert
        Assert.ThrowsException<ArgumentNullException>(() => new PlaceholderInfo(null!));
    }
}
