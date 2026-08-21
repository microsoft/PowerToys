// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Ext.Indexer.Indexer;
using Microsoft.CmdPal.Ext.Indexer.Properties;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.Indexer.UnitTests;

[TestClass]
public class FallbackOpenFileItemTests
{
    [TestMethod]
    public void GetFallbackNoticeText_UsesExtensionNameAsTitle()
    {
        var notice = new SearchNoticeInfo(Resources.Indexer_SearchFailedMessage!, Resources.Indexer_SearchFailedMessageTip!);

        var text = FallbackOpenFileItem.GetFallbackNoticeText(notice);

        Assert.AreEqual(Resources.IndexerCommandsProvider_DisplayName, text.Title);
        Assert.AreEqual(Resources.Indexer_SearchFailedMessage, text.Subtitle);
    }
}
