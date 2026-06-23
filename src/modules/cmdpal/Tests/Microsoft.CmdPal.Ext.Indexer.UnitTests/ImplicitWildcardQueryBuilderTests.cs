// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Ext.Indexer.Indexer.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.Indexer.UnitTests;

[TestClass]
public class ImplicitWildcardQueryBuilderTests
{
    [DataTestMethod]
    [DataRow("term", null, "((CONTAINS(System.ItemNameDisplay, '\"term\"') OR CONTAINS(System.ItemNameDisplay, '\"term*\"')) OR System.FileName LIKE '%term%')", "System.FileName LIKE '%term%'")]
    [DataRow("term Kind:Folder", "Kind:Folder", "((CONTAINS(System.ItemNameDisplay, '\"term\"') OR CONTAINS(System.ItemNameDisplay, '\"term*\"')) OR System.FileName LIKE '%term%')", "System.FileName LIKE '%term%'")]
    [DataRow("System.Kind:folders term", "System.Kind:folders", "((CONTAINS(System.ItemNameDisplay, '\"term\"') OR CONTAINS(System.ItemNameDisplay, '\"term*\"')) OR System.FileName LIKE '%term%')", "System.FileName LIKE '%term%'")]
    [DataRow("System.Kind:NOT folders term", "System.Kind:NOT folders", "((CONTAINS(System.ItemNameDisplay, '\"term\"') OR CONTAINS(System.ItemNameDisplay, '\"term*\"')) OR System.FileName LIKE '%term%')", "System.FileName LIKE '%term%'")]
    [DataRow("\"two words\"", null, "((CONTAINS(System.ItemNameDisplay, '\"two words\"') OR CONTAINS(System.ItemNameDisplay, '\"two words*\"') OR CONTAINS(System.ItemNameDisplay, '\"two\" AND \"words\"') OR CONTAINS(System.ItemNameDisplay, '\"two*\" AND \"words*\"')) OR System.FileName LIKE '%two words%')", "System.FileName LIKE '%two words%'")]
    [DataRow("foo bar", null, "((CONTAINS(System.ItemNameDisplay, '\"foo bar\"') OR CONTAINS(System.ItemNameDisplay, '\"foo bar*\"') OR CONTAINS(System.ItemNameDisplay, '\"foo\" AND \"bar\"') OR CONTAINS(System.ItemNameDisplay, '\"foo*\" AND \"bar*\"')) OR (System.FileName LIKE '%foo%' AND System.FileName LIKE '%bar%'))", "(System.FileName LIKE '%foo%' AND System.FileName LIKE '%bar%')")]
    [DataRow("foo-bar", null, "((CONTAINS(System.ItemNameDisplay, '\"foo bar\"') OR CONTAINS(System.ItemNameDisplay, '\"foo bar*\"') OR CONTAINS(System.ItemNameDisplay, '\"foo\" AND \"bar\"') OR CONTAINS(System.ItemNameDisplay, '\"foo*\" AND \"bar*\"')) OR System.FileName LIKE '%foo-bar%')", "System.FileName LIKE '%foo-bar%'")]
    [DataRow("foo & bar", null, "((CONTAINS(System.ItemNameDisplay, '\"foo bar\"') OR CONTAINS(System.ItemNameDisplay, '\"foo bar*\"') OR CONTAINS(System.ItemNameDisplay, '\"foo\" AND \"bar\"') OR CONTAINS(System.ItemNameDisplay, '\"foo*\" AND \"bar*\"')) OR (System.FileName LIKE '%foo%' AND System.FileName LIKE '%bar%'))", "(System.FileName LIKE '%foo%' AND System.FileName LIKE '%bar%')")]
    [DataRow("tonträger", null, "((CONTAINS(System.ItemNameDisplay, '\"tonträger\"') OR CONTAINS(System.ItemNameDisplay, '\"tonträger*\"')) OR System.FileName LIKE '%tonträger%')", "System.FileName LIKE '%tonträger%'")]
    [DataRow("O'Hara", null, "((CONTAINS(System.ItemNameDisplay, '\"Hara\"') OR CONTAINS(System.ItemNameDisplay, '\"Hara*\"')) OR System.FileName LIKE '%O''Hara%')", "System.FileName LIKE '%O''Hara%'")]
    [DataRow("AT&T", null, "System.FileName LIKE '%AT&T%'", null)]
    [DataRow("file_100%", null, "((CONTAINS(System.ItemNameDisplay, '\"file 100\"') OR CONTAINS(System.ItemNameDisplay, '\"file 100*\"') OR CONTAINS(System.ItemNameDisplay, '\"file\" AND \"100\"') OR CONTAINS(System.ItemNameDisplay, '\"file*\" AND \"100*\"')) OR System.FileName LIKE '%file[_]100[%]%')", "System.FileName LIKE '%file[_]100[%]%'")]
    public void BuildExpandedQuery_BuildsExpectedRestrictions(string query, string expectedStructuredSearchText, string expectedPrimaryClause, string expectedFallbackClause)
    {
        var expandedQuery = ImplicitWildcardQueryBuilder.BuildExpandedQuery(query);

        Assert.AreEqual(expectedStructuredSearchText, expandedQuery.StructuredSearchText);
        Assert.AreEqual(expectedPrimaryClause, expandedQuery.PrimaryRestriction);
        Assert.AreEqual(expectedFallbackClause, expandedQuery.FallbackRestriction);
    }

    [TestMethod]
    public void BuildExpandedQuery_PreservesBracketWrappedTermAsLiteralOnly()
    {
        var expandedQuery = ImplicitWildcardQueryBuilder.BuildExpandedQuery("[red]");

        Assert.AreEqual("System.FileName LIKE '%[[]red[]]%'", expandedQuery.PrimaryRestriction);
        Assert.IsFalse(expandedQuery.HasFallbackRestriction);
    }

    [TestMethod]
    public void BuildExpandedQuery_TreatsSinglePercentAsLiteralCharacter()
    {
        var expandedQuery = ImplicitWildcardQueryBuilder.BuildExpandedQuery("%");

        Assert.AreEqual("System.FileName LIKE '%[%]%'", expandedQuery.PrimaryRestriction);
        Assert.IsFalse(expandedQuery.HasFallbackRestriction);
    }

    [TestMethod]
    public void BuildExpandedQuery_TreatsSingleUnderscoreAsLiteralCharacter()
    {
        var expandedQuery = ImplicitWildcardQueryBuilder.BuildExpandedQuery("_");

        Assert.AreEqual("System.FileName LIKE '%[_]%'", expandedQuery.PrimaryRestriction);
        Assert.IsFalse(expandedQuery.HasFallbackRestriction);
    }

    [DataTestMethod]
    [DataRow("kind:folder")]
    [DataRow("name:term")]
    [DataRow("name: term")]
    [DataRow("name:\"two words\"")]
    [DataRow("*term*")]
    [DataRow("C:\\Users")]
    [DataRow("System.Kind:folders")]
    [DataRow("kind:folder AND term")]
    public void BuildExpandedQuery_DoesNotBroadenStructuredOrExplicitQueries(string query)
    {
        var expandedQuery = ImplicitWildcardQueryBuilder.BuildExpandedQuery(query);

        Assert.IsFalse(expandedQuery.HasPrimaryRestriction);
        Assert.IsFalse(expandedQuery.HasFallbackRestriction);
    }
}
