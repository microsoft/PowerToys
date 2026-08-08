// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using MouseWithoutBorders.Core;

namespace MouseWithoutBorders.UnitTests.Core;

[TestClass]
public sealed class ImpersonationHelperTests
{
    [TestMethod]
    public void TryRevertToSelf_RetriesUntilItSucceeds()
    {
        int calls = 0;
        List<int> delays = [];

        bool result = ImpersonationHelper.TryRevertToSelf(
            () => ++calls == 2,
            milliseconds => delays.Add(milliseconds));

        Assert.IsTrue(result);
        Assert.AreEqual(2, calls);
        CollectionAssert.AreEqual(
            new[] { ImpersonationHelper.RevertToSelfRetryDelayMilliseconds },
            delays);
    }

    [TestMethod]
    public void TryRevertToSelf_FailsAfterBoundedRetries()
    {
        int calls = 0;
        int delays = 0;

        bool result = ImpersonationHelper.TryRevertToSelf(
            () =>
            {
                calls++;
                return false;
            },
            _ => delays++);

        Assert.IsFalse(result);
        Assert.AreEqual(ImpersonationHelper.RevertToSelfAttempts, calls);
        Assert.AreEqual(ImpersonationHelper.RevertToSelfAttempts - 1, delays);
    }

    [TestMethod]
    public void RevertToSelfOrFailFast_PreventsContinuationAfterFinalFailure()
    {
        bool failFastCalled = false;
        bool continued = false;

        _ = Assert.ThrowsException<FatalImpersonationException>(() =>
        {
            ImpersonationHelper.RevertToSelfOrFailFast(
                () => false,
                _ => { },
                _ => failFastCalled = true,
                () => "Unable to restore the process identity.");
            continued = true;
        });

        Assert.IsTrue(failFastCalled);
        Assert.IsFalse(continued);
    }
}
