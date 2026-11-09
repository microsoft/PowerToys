// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.ZoomIt.UITests;

[TestClass]
[TestCategory("ZoomIt")]
public sealed class ZoomItDialogReadinessTests
{
    private static readonly string[] ReacquiredTargets = ["stale", "fresh"];
    private static readonly string[] ReacquiredObservations = ["stale", "fresh", "fresh"];
    private static readonly string[] StableObservations = ["fresh", "fresh"];

    [TestMethod]
    public void StaleProbeReacquiresTheButtonAndRequiresFreshStableSamples()
    {
        var resolved = 0;
        var observations = new List<string>();
        var prepared = new List<string>();
        var ready = ZoomItUi.WaitForDialogButton(
            () => ++resolved == 1 ? "stale" : "fresh",
            prepared.Add,
            target =>
            {
                observations.Add(target);
                if (target == "stale")
                {
                    throw new AssertFailedException("winapp ui inspect: stale_element");
                }

                return target;
            },
            target => target == "fresh",
            _ => Assert.Fail("A transient exception should invalidate the cached target, not perform an input action."),
            timeoutMS: 10_000,
            pollIntervalMS: 1);

        Assert.IsTrue(ready.Succeeded);
        Assert.AreEqual("fresh", ready.LastObservation);
        CollectionAssert.AreEqual(ReacquiredTargets, prepared);
        CollectionAssert.AreEqual(ReacquiredObservations, observations);
        Assert.AreEqual(2, ready.ConsecutiveMatches);
    }

    [TestMethod]
    public void StalePreparationIsRetriedWithAReplacementButton()
    {
        var resolved = 0;
        var prepared = new List<string>();
        var observed = new List<string>();
        var ready = ZoomItUi.WaitForDialogButton(
            () => ++resolved == 1 ? "stale" : "fresh",
            target =>
            {
                prepared.Add(target);
                if (target == "stale")
                {
                    throw new AssertFailedException("winapp ui focus: stale_element");
                }
            },
            target =>
            {
                observed.Add(target);
                return target;
            },
            target => target == "fresh",
            _ => Assert.Fail("Preparation retries must not open the dialog."),
            timeoutMS: 10_000,
            pollIntervalMS: 1);

        Assert.IsTrue(ready.Succeeded);
        CollectionAssert.AreEqual(ReacquiredTargets, prepared);
        CollectionAssert.AreEqual(StableObservations, observed);
    }

    [TestMethod]
    public void MissingProbeReacquiresTheButtonAfterRecovery()
    {
        var resolved = 0;
        var recoveries = 0;
        var ready = ZoomItUi.WaitForDialogButton(
            () => ++resolved == 1 ? "missing" : "fresh",
            _ => { },
            target => target == "missing" ? null : target,
            target => target == "fresh",
            target =>
            {
                Assert.IsNull(target);
                recoveries++;
            },
            timeoutMS: 10_000,
            pollIntervalMS: 1);

        Assert.IsTrue(ready.Succeeded);
        Assert.AreEqual("fresh", ready.LastObservation);
        Assert.AreEqual(2, resolved);
        Assert.AreEqual(1, recoveries);
    }

    [TestMethod]
    public void TransientFailureResetsTheConsecutiveReadinessCount()
    {
        var resolved = 0;
        var observed = 0;
        var ready = ZoomItUi.WaitForDialogButton(
            () => $"button-{++resolved}",
            _ => { },
            target =>
            {
                if (++observed == 2)
                {
                    throw new AssertFailedException("stale_element");
                }

                return target;
            },
            target => target is not null,
            _ => Assert.Fail("The stale observation must not trigger an input action."),
            timeoutMS: 10_000,
            pollIntervalMS: 1);

        Assert.IsTrue(ready.Succeeded);
        Assert.AreEqual("button-2", ready.LastObservation);
        Assert.AreEqual(4, observed);
        Assert.AreEqual(2, resolved);
        Assert.AreEqual(2, ready.ConsecutiveMatches);
    }

    [TestMethod]
    public void NonTransientAssertionsAreNotRetried()
    {
        var expected = new AssertFailedException("winapp ui inspect: access_denied");
        var resolved = 0;
        var actual = Assert.ThrowsExactly<AssertFailedException>(() =>
            ZoomItUi.WaitForDialogButton<string>(
                () =>
                {
                    resolved++;
                    return "button";
                },
                _ => { },
                _ => throw expected,
                _ => true,
                _ => { },
                timeoutMS: 10_000,
                pollIntervalMS: 1));

        Assert.AreSame(expected, actual);
        Assert.AreEqual(1, resolved);
    }

    [TestMethod]
    public void NonAssertionExceptionsAreNotRetriedEvenWithTransientText()
    {
        var expected = new InvalidOperationException("stale_element");
        var actual = Assert.ThrowsExactly<InvalidOperationException>(() =>
            ZoomItUi.WaitForDialogButton<string>(
                () => throw expected,
                _ => { },
                target => target,
                _ => true,
                _ => { },
                timeoutMS: 10_000,
                pollIntervalMS: 1));

        Assert.AreSame(expected, actual);
    }

    [TestMethod]
    public void PersistentStalenessTimesOutWithTheLastError()
    {
        var expected = new AssertFailedException("stale_element");
        var resolved = 0;
        var ready = ZoomItUi.WaitForDialogButton<string>(
            () =>
            {
                resolved++;
                return "button";
            },
            _ => { },
            _ => throw expected,
            _ => true,
            _ => { },
            timeoutMS: 1_000,
            pollIntervalMS: 10);

        Assert.IsFalse(ready.Succeeded);
        Assert.AreSame(expected, ready.LastException);
        Assert.AreEqual(0, ready.ConsecutiveMatches);
        Assert.IsTrue(resolved > 0);
    }
}
