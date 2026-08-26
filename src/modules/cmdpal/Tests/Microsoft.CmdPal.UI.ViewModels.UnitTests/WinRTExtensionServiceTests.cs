// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public class WinRTExtensionServiceTests
{
    [TestMethod]
    public void IsSuccessfulCompletion_IncompleteOperation_ReturnsFalse()
    {
        Assert.IsFalse(WinRTExtensionService.IsSuccessfulPackageOperation(isComplete: false, errorCode: null));
    }

    [TestMethod]
    public void IsSuccessfulCompletion_CompletedOperationWithoutError_ReturnsTrue()
    {
        Assert.IsTrue(WinRTExtensionService.IsSuccessfulPackageOperation(isComplete: true, errorCode: null));
    }

    [TestMethod]
    public void IsSuccessfulCompletion_CompletedOperationWithAnyError_ReturnsFalse()
    {
        var error = new InvalidOperationException("Any projected package operation failure");

        Assert.IsFalse(WinRTExtensionService.IsSuccessfulPackageOperation(isComplete: true, errorCode: error));
    }
}
