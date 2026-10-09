// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Ext.Indexer.Indexer;
using Microsoft.CmdPal.Ext.Indexer.Properties;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.Indexer.UnitTests;

[TestClass]
public class SearchNoticeInfoBuilderTests
{
    [DataTestMethod]
    [DataRow((int)SearchQuery.QueryState.NullDataSource)]
    [DataRow((int)SearchQuery.QueryState.CreateSessionFailed)]
    [DataRow((int)SearchQuery.QueryState.CreateCommandFailed)]
    public void FromQueryStatus_ReturnsUnavailableNotice_ForInfrastructureFailures(int stateValue)
    {
        var state = (SearchQuery.QueryState)stateValue;
        var notice = SearchNoticeInfoBuilder.FromQueryStatus(new SearchQuery.SearchExecutionStatus(state, null, "failure"));

        Assert.IsNotNull(notice);
        Assert.AreEqual(Resources.Indexer_SearchUnavailableMessage, notice.Value.Title);
        Assert.AreEqual(Resources.Indexer_SearchUnavailableMessageTip, notice.Value.Subtitle);
    }

    [TestMethod]
    public void FromQueryStatus_ReturnsUnavailableNotice_ForRpcFailures()
    {
        var notice = SearchNoticeInfoBuilder.FromQueryStatus(
            new SearchQuery.SearchExecutionStatus(
                SearchQuery.QueryState.ExecuteFailed,
                unchecked((int)0x800706BA),
                "RPC server unavailable"));

        Assert.IsNotNull(notice);
        Assert.AreEqual(Resources.Indexer_SearchUnavailableMessage, notice.Value.Title);
    }

    [TestMethod]
    public void FromQueryStatus_ReturnsGenericFailureNotice_ForUnexpectedFailures()
    {
        var notice = SearchNoticeInfoBuilder.FromQueryStatus(
            new SearchQuery.SearchExecutionStatus(
                SearchQuery.QueryState.ExecuteFailed,
                unchecked((int)0x80004005),
                "unexpected"));

        Assert.IsNotNull(notice);
        Assert.AreEqual(Resources.Indexer_SearchFailedMessage, notice.Value.Title);
        Assert.AreEqual(Resources.Indexer_SearchFailedMessageTip, notice.Value.Subtitle);
    }

    [DataTestMethod]
    [DataRow((int)SearchQuery.QueryState.Completed)]
    [DataRow((int)SearchQuery.QueryState.NoResults)]
    [DataRow((int)SearchQuery.QueryState.AllNoise)]
    public void FromQueryStatus_ReturnsNull_ForNonFailureStates(int stateValue)
    {
        var state = (SearchQuery.QueryState)stateValue;
        var notice = SearchNoticeInfoBuilder.FromQueryStatus(new SearchQuery.SearchExecutionStatus(state, null, null));

        Assert.IsNull(notice);
    }

    [TestMethod]
    public void FromCatalogStatus_ReturnsIndexingNotice_WhenItemsArePending()
    {
        var notice = SearchNoticeInfoBuilder.FromCatalogStatus(new SearchCatalogStatus(42, null));

        Assert.IsNotNull(notice);
        Assert.AreEqual(Resources.Indexer_SearchIndexingMessage, notice.Value.Title);
        StringAssert.Contains(notice.Value.Subtitle, "42");
    }

    [TestMethod]
    public void FromCatalogStatus_ReturnsNull_WhenStatusReadFails()
    {
        var notice = SearchNoticeInfoBuilder.FromCatalogStatus(new SearchCatalogStatus(0, unchecked((int)0x800706BA)));

        Assert.IsNull(notice);
    }

    [TestMethod]
    public void FromCatalogStatus_ReturnsNull_WhenIndexingIsIdle()
    {
        var notice = SearchNoticeInfoBuilder.FromCatalogStatus(new SearchCatalogStatus(0, null));

        Assert.IsNull(notice);
    }
}
