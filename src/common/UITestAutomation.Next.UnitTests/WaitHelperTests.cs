// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.UITestAutomationNext.UnitTests;

[TestClass]
public class WaitHelperTests
{
    private static readonly int[] ExpectedRecoveredValues = [1, 2];

    [TestMethod]
    public void WaitForStableRequiresConsecutiveMatches()
    {
        var observations = new Queue<bool>([true, false, true, true, true]);
        var observationCount = 0;

        var result = WaitHelper.WaitForStable(
            observe: () =>
            {
                observationCount++;
                return observations.Dequeue();
            },
            isMatch: value => value,
            timeoutMS: 1_000,
            requiredConsecutiveMatches: 3,
            pollIntervalMS: 1);

        Assert.IsTrue(result.Succeeded);
        Assert.AreEqual(5, observationCount);
        Assert.AreEqual(3, result.ConsecutiveMatches);
    }

    [TestMethod]
    public void WaitForStableRunsRecoveryOnMismatch()
    {
        var observations = new Queue<int>([1, 2, 3]);
        var recoveredValues = new List<int>();

        var result = WaitHelper.WaitForStable(
            observe: observations.Dequeue,
            isMatch: value => value == 3,
            timeoutMS: 1_000,
            pollIntervalMS: 1,
            recover: value => recoveredValues.Add(value));

        Assert.IsTrue(result.Succeeded);
        CollectionAssert.AreEqual(ExpectedRecoveredValues, recoveredValues);
    }

    [TestMethod]
    public void WaitForStableReturnsLastObservationOnTimeout()
    {
        var observation = 0;

        var result = WaitHelper.WaitForStable(
            observe: () => ++observation,
            isMatch: _ => false,
            timeoutMS: 20,
            pollIntervalMS: 1);

        Assert.IsFalse(result.Succeeded);
        Assert.AreEqual(observation, result.LastObservation);
        Assert.IsTrue(observation > 0);
    }

    [TestMethod]
    public void WaitForStableRetriesOnlyClassifiedExceptions()
    {
        var attempts = 0;
        var transient = new InvalidOperationException("Transient");

        var result = WaitHelper.WaitForStable<int>(
            observe: () =>
            {
                attempts++;
                if (attempts == 1)
                {
                    throw transient;
                }

                return 42;
            },
            isMatch: value => value == 42,
            timeoutMS: 1_000,
            pollIntervalMS: 1,
            shouldRetryException: exception => ReferenceEquals(exception, transient));

        Assert.IsTrue(result.Succeeded);
        Assert.AreEqual(2, attempts);
        Assert.IsNull(result.LastException);
    }
}
