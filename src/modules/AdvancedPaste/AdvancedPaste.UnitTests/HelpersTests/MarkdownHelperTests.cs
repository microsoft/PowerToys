// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Reflection;
using AdvancedPaste.Helpers;
using HtmlAgilityPack;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AdvancedPaste.UnitTests.HelpersTests;

[TestClass]
public sealed class MarkdownHelperTests
{
    /// <summary>
    /// Helper method to invoke the private CleanHtml method for testing.
    /// </summary>
    private static string InvokeCleanHtml(string html)
    {
        var methodInfo = typeof(MarkdownHelper).GetMethod("CleanHtml", BindingFlags.NonPublic | BindingFlags.Static);
        return (string)methodInfo!.Invoke(null, [html])!;
    }

    /// <summary>
    /// Helper method to invoke the private ConvertHtmlToMarkdown method for testing.
    /// </summary>
    private static string InvokeConvertHtmlToMarkdown(string html)
    {
        var methodInfo = typeof(MarkdownHelper).GetMethod("ConvertHtmlToMarkdown", BindingFlags.NonPublic | BindingFlags.Static);
        return (string)methodInfo!.Invoke(null, [html])!;
    }

    [TestMethod]
    public void CleanHtml_GoogleSheetsWrapper_RemovesWrapperPreservesTable()
    {
        // Arrange - Google Sheets HTML with wrapper element
        const string googleSheetsHtml = @"<google-sheets-html-origin>
<style type=""text/css""><!--td {border: 1px solid #cccccc;}--></style>
<table xmlns=""http://www.w3.org/1999/xhtml"" cellspacing=""0"" cellpadding=""0"" dir=""ltr"" border=""1"" data-sheets-root=""1"">
<colgroup><col width=""100""><col width=""100""></colgroup>
<tbody><tr><td>A</td><td>B</td></tr><tr><td>1</td><td>2</td></tr></tbody>
</table>
</google-sheets-html-origin>";

        // Act
        string cleanedHtml = InvokeCleanHtml(googleSheetsHtml);

        // Assert - wrapper and style should be removed, table preserved
        Assert.IsFalse(cleanedHtml.Contains("google-sheets-html-origin"), "Google Sheets wrapper should be removed");
        Assert.IsFalse(cleanedHtml.Contains("<style"), "Style element should be removed");
        Assert.IsFalse(cleanedHtml.Contains("<colgroup"), "Colgroup element should be removed");
        Assert.IsTrue(cleanedHtml.Contains("<table"), "Table element should be preserved");
        Assert.IsTrue(cleanedHtml.Contains("<td>A</td>"), "Table content should be preserved");
    }

    [TestMethod]
    public void CleanHtml_GoogleSheetsHtml_ConvertsToMarkdownTable()
    {
        // Arrange - Google Sheets HTML with wrapper element
        const string googleSheetsHtml = @"<google-sheets-html-origin>
<style type=""text/css""><!--td {border: 1px solid #cccccc;}--></style>
<table xmlns=""http://www.w3.org/1999/xhtml"" cellspacing=""0"" cellpadding=""0"" dir=""ltr"" border=""1"" data-sheets-root=""1"">
<colgroup><col width=""100""><col width=""100""></colgroup>
<tbody><tr><td>A</td><td>B</td></tr><tr><td>1</td><td>2</td></tr></tbody>
</table>
</google-sheets-html-origin>";

        // Act
        string cleanedHtml = InvokeCleanHtml(googleSheetsHtml);
        string markdown = InvokeConvertHtmlToMarkdown(cleanedHtml);

        // Assert - should produce valid Markdown table
        Assert.IsTrue(markdown.Contains("|"), "Markdown should contain table pipes");
        Assert.IsTrue(markdown.Contains("A") && markdown.Contains("B"), "Markdown should contain table content");
        Assert.IsTrue(markdown.Contains("1") && markdown.Contains("2"), "Markdown should contain table data");
        Assert.IsFalse(markdown.Contains("<google-sheets-html-origin>"), "Markdown should not contain HTML wrapper");
    }

    [TestMethod]
    public void CleanHtml_ExcelHtml_ConvertsToMarkdownTable()
    {
        // Arrange - Typical Excel HTML (for regression testing)
        const string excelHtml = @"<table border=""0"" cellpadding=""0"" cellspacing=""0"" width=""192"">
<tbody><tr height=""20""><td height=""20"" width=""64"">Name</td><td width=""64"">Value</td><td width=""64"">Status</td></tr>
<tr height=""20""><td height=""20"">Item1</td><td>100</td><td>Active</td></tr>
<tr height=""20""><td height=""20"">Item2</td><td>200</td><td>Inactive</td></tr>
</tbody></table>";

        // Act
        string cleanedHtml = InvokeCleanHtml(excelHtml);
        string markdown = InvokeConvertHtmlToMarkdown(cleanedHtml);

        // Assert - Excel HTML should still convert correctly
        Assert.IsTrue(markdown.Contains("|"), "Markdown should contain table pipes");
        Assert.IsTrue(markdown.Contains("Name"), "Markdown should contain header content");
        Assert.IsTrue(markdown.Contains("Item1") && markdown.Contains("Item2"), "Markdown should contain row data");
    }

    [TestMethod]
    public void CleanHtml_StyleElement_IsRemoved()
    {
        // Arrange
        const string htmlWithStyle = @"<html><head><style>body { color: red; }</style></head><body><p>Text</p></body></html>";

        // Act
        string cleanedHtml = InvokeCleanHtml(htmlWithStyle);

        // Assert
        Assert.IsFalse(cleanedHtml.Contains("<style"), "Style element should be removed");
        Assert.IsTrue(cleanedHtml.Contains("<p>Text</p>") || cleanedHtml.Contains("Text"), "Content should be preserved");
    }

    [TestMethod]
    public void CleanHtml_ColgroupElement_IsRemoved()
    {
        // Arrange
        const string htmlWithColgroup = @"<table><colgroup><col width=""100""></colgroup><tbody><tr><td>Data</td></tr></tbody></table>";

        // Act
        string cleanedHtml = InvokeCleanHtml(htmlWithColgroup);

        // Assert
        Assert.IsFalse(cleanedHtml.Contains("<colgroup"), "Colgroup element should be removed");
        Assert.IsTrue(cleanedHtml.Contains("<table"), "Table element should be preserved");
        Assert.IsTrue(cleanedHtml.Contains("Data"), "Table content should be preserved");
    }

    [TestMethod]
    public void CleanHtml_ScriptElement_IsRemoved()
    {
        // Arrange
        const string htmlWithScript = @"<html><body><script>alert('test');</script><p>Content</p></body></html>";

        // Act
        string cleanedHtml = InvokeCleanHtml(htmlWithScript);

        // Assert
        Assert.IsFalse(cleanedHtml.Contains("<script"), "Script element should be removed");
        Assert.IsTrue(cleanedHtml.Contains("Content"), "Content should be preserved");
    }

    [TestMethod]
    public void CleanHtml_NestedGoogleSheetsTable_PreservesNestedContent()
    {
        // Arrange - More complex Google Sheets HTML
        const string complexGoogleSheetsHtml = @"<google-sheets-html-origin>
<style type=""text/css"">td {border: 1px solid #ccc;}</style>
<table data-sheets-root=""1"">
<colgroup><col width=""100""><col width=""150""><col width=""100""></colgroup>
<tbody>
<tr><td>Header1</td><td>Header2</td><td>Header3</td></tr>
<tr><td>Row1Col1</td><td>Row1Col2</td><td>Row1Col3</td></tr>
<tr><td>Row2Col1</td><td>Row2Col2</td><td>Row2Col3</td></tr>
</tbody>
</table>
</google-sheets-html-origin>";

        // Act
        string cleanedHtml = InvokeCleanHtml(complexGoogleSheetsHtml);
        string markdown = InvokeConvertHtmlToMarkdown(cleanedHtml);

        // Assert
        Assert.IsFalse(markdown.Contains("<google-sheets-html-origin>"), "Wrapper should be removed from markdown");
        Assert.IsTrue(markdown.Contains("Header1"), "Headers should be preserved");
        Assert.IsTrue(markdown.Contains("Row1Col1"), "Row data should be preserved");
        Assert.IsTrue(markdown.Contains("Row2Col3"), "All cells should be preserved");
    }
}
