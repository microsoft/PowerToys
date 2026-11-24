// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.UnitTests;

[TestClass]
public sealed class CompactModeLayoutTests
{
    [DataTestMethod]
    [DataRow(true, true, false, true)]
    [DataRow(true, false, true, false)]
    [DataRow(false, false, true, true)]
    [DataRow(false, false, false, false)]
    public void ShouldUpdateForPageChange_PreservesManuallyExpandedRootPage(
        bool isRootPage,
        bool queryChanged,
        bool contentChanged,
        bool expected)
    {
        Assert.AreEqual(
            expected,
            CompactModeLayout.ShouldUpdateForPageChange(isRootPage, queryChanged, contentChanged));
    }

    [DataTestMethod]
    [DataRow(false, false, true, false, false, true)]
    [DataRow(true, true, true, false, false, true)]
    [DataRow(true, false, false, false, true, false)]
    [DataRow(true, false, true, false, true, true)]
    [DataRow(true, false, true, false, false, false)]
    [DataRow(true, false, true, true, false, true)]
    public void ShouldExpand_UsesPageVisibilityState(
        bool compactMode,
        bool isTransient,
        bool isNested,
        bool hasQuery,
        bool hasPageContent,
        bool expected)
    {
        Assert.AreEqual(
            expected,
            CompactModeLayout.ShouldExpand(compactMode, isTransient, isNested, hasQuery, hasPageContent));
    }
}
