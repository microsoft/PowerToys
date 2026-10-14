// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using System.Text.Json;

namespace Microsoft.CmdPal.AdaptiveCards.IncrementalRendering.UnitTests;

[TestClass]
public sealed class IncrementalAdaptiveCardUpdaterTests
{
    [TestMethod]
    public void SuccessfulSnapshotIsRetained()
    {
        var expected = new IncrementalTreeSnapshot([new IncrementalNodeSnapshot("Root", 0)]);

        var snapshot = IncrementalAdaptiveCardUpdater.TryCreateSnapshot(() => expected);

        Assert.AreSame(expected, snapshot);
    }

    [TestMethod]
    public void JsonDeeperThanSnapshotLimitDisablesIncrementalUpdate()
    {
        var body = """{"type":"TextBlock","text":"Nested label"}""";
        for (var depth = 0; depth < 32; depth++)
        {
            body = $$"""{"type":"Container","items":[{{body}}]}""";
        }

        var json = $$"""{"type":"AdaptiveCard","version":"1.5","body":[{{body}}]}""";

        Assert.Throws<JsonException>(() => AdaptiveCardSemanticFingerprint.Create(json));

        var snapshot = IncrementalAdaptiveCardUpdater.TryCreateSnapshot(() =>
        {
            var fingerprint = AdaptiveCardSemanticFingerprint.Create(json);
            return new IncrementalTreeSnapshot([new IncrementalNodeSnapshot(fingerprint, 0)]);
        });

        Assert.IsNull(snapshot);
    }

    [TestMethod]
    public void ComFailureDisablesIncrementalUpdate()
    {
        var snapshot = IncrementalAdaptiveCardUpdater.TryCreateSnapshot(
            () => throw Marshal.GetExceptionForHR(unchecked((int)0x80004005))!);

        Assert.IsNull(snapshot);
    }

    [TestMethod]
    public void CancellationIsNotTreatedAsSnapshotFailure()
    {
        Assert.ThrowsExactly<OperationCanceledException>(() =>
            IncrementalAdaptiveCardUpdater.TryCreateSnapshot(() => throw new OperationCanceledException()));
    }

    [TestMethod]
    public void UnexpectedFailureIsNotSuppressed()
    {
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            IncrementalAdaptiveCardUpdater.TryCreateSnapshot(() => throw new InvalidOperationException()));
    }

    [TestMethod]
    public void SnapshotCreationCanRecoverAfterFailure()
    {
        var failed = IncrementalAdaptiveCardUpdater.TryCreateSnapshot(() => throw new JsonException());
        var expected = new IncrementalTreeSnapshot([new IncrementalNodeSnapshot("Root", 0)]);

        var recovered = IncrementalAdaptiveCardUpdater.TryCreateSnapshot(() => expected);

        Assert.IsNull(failed);
        Assert.AreSame(expected, recovered);
    }
}
