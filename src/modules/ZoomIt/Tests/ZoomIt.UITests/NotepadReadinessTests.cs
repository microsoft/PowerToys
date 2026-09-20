// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.ZoomIt.UITests;

[TestClass]
[TestCategory("ZoomIt")]
public sealed class NotepadReadinessTests
{
    [TestMethod]
    public void ReplacementWindowMustBeReadyTwiceBeforeUse()
    {
        var observed = 0;
        var result = NotepadReadiness.WaitForStableWindow(
            () => new Candidate(++observed == 1 ? 1 : 2),
            candidate => candidate.Handle,
            timeoutMS: 10_000,
            pollIntervalMS: 1);

        Assert.IsTrue(result.Succeeded);
        Assert.AreEqual(2L, result.LastObservation!.Handle);
        Assert.AreEqual(3, observed);
    }

    [TestMethod]
    public void DisappearingHwndIsReboundAndReadinessRestarts()
    {
        var observed = 0;
        var result = NotepadReadiness.WaitForStableWindow(
            () =>
            {
                observed++;
                if (observed == 2)
                {
                    throw new AssertFailedException("internal_error: Window HWND 1 not found or not accessible.");
                }

                return new Candidate(observed == 1 ? 1 : 2);
            },
            candidate => candidate.Handle,
            timeoutMS: 10_000,
            pollIntervalMS: 1);

        Assert.IsTrue(result.Succeeded);
        Assert.AreEqual(2L, result.LastObservation!.Handle);
        Assert.AreEqual(4, observed);
    }

    [TestMethod]
    public void MissingEditorResetsWindowReadiness()
    {
        var observed = 0;
        var result = NotepadReadiness.WaitForStableWindow(
            () => ++observed == 2 ? null : new Candidate(1),
            candidate => candidate.Handle,
            timeoutMS: 10_000,
            pollIntervalMS: 1);

        Assert.IsTrue(result.Succeeded);
        Assert.AreEqual(4, observed);
    }

    [TestMethod]
    public void ZeroHandleCannotSatisfyReadiness()
    {
        var observed = 0;
        var result = NotepadReadiness.WaitForStableWindow(
            () => new Candidate(++observed <= 2 ? 0 : 1),
            candidate => candidate.Handle,
            timeoutMS: 10_000,
            pollIntervalMS: 1);

        Assert.IsTrue(result.Succeeded);
        Assert.AreEqual(1L, result.LastObservation!.Handle);
        Assert.AreEqual(4, observed);
    }

    [TestMethod]
    public void ConstantWindowReplacementTimesOut()
    {
        var observed = 0;
        var result = NotepadReadiness.WaitForStableWindow(
            () => new Candidate(++observed),
            candidate => candidate.Handle,
            timeoutMS: 1_000,
            pollIntervalMS: 10);

        Assert.IsFalse(result.Succeeded);
        Assert.IsTrue(observed > 0);
    }

    [TestMethod]
    public void UnrelatedFailuresAreNotRetried()
    {
        var expected = new AssertFailedException("access_denied");
        var observed = 0;
        var actual = Assert.ThrowsExactly<AssertFailedException>(() =>
            NotepadReadiness.WaitForStableWindow<Candidate>(
                () =>
                {
                    observed++;
                    throw expected;
                },
                candidate => candidate.Handle,
                timeoutMS: 10_000,
                pollIntervalMS: 1));

        Assert.AreSame(expected, actual);
        Assert.AreEqual(1, observed);
        Assert.IsFalse(NotepadReadiness.IsTransientException(new InvalidOperationException("stale_element")));
    }

    [TestMethod]
    [DataRow("stale_element", true)]
    [DataRow("element_not_found", true)]
    [DataRow("window_not_found", true)]
    [DataRow("internal_error: Window HWND 42 not found or not accessible.", true)]
    [DataRow("internal_error: malformed output", false)]
    [DataRow("access_denied: Window HWND 42 not found or not accessible.", false)]
    public void OnlyKnownLifetimeFailuresAreTransient(string message, bool expected)
    {
        Assert.AreEqual(expected, NotepadReadiness.IsTransientException(new AssertFailedException(message)));
    }

    private sealed record Candidate(long Handle);
}
