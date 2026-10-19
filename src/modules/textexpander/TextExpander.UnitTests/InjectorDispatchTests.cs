// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.TextExpander.UnitTests;

[TestClass]
public sealed class InjectorDispatchTests
{
    private static readonly int[] CompleteTypingBatchSizes = [2, 2, 2, 4];
    private static readonly int[] ControlOnlyBatchSizes = [1];
    private static readonly int[] PasteChordBatchSizes = [1, 2, 1];
    private static readonly uint[] ControlDownFailure = [0];
    private static readonly uint[] PastePartialFailure = [1, 1, 1];
    private static readonly uint[] ControlUpFailure = [1, 2, 0];
    private static readonly uint[] CompletePasteChord = [1, 2, 1];

    [TestMethod]
    [DataRow("\n", 2)]
    [DataRow("\t", 2)]
    [DataRow("A", 2)]
    [DataRow("😀", 4)]
    public void TypingRejectsEveryPartialDispatchBatch(string text, int expectedBatchSize)
    {
        InjectionDiagnostics.Reset();
        var batches = new List<int>();

        bool sent = Injector.SendText(
            text,
            IntPtr.Zero,
            inputs =>
            {
                batches.Add(inputs.Length);
                return (uint)(inputs.Length - 1);
            },
            _ => true,
            () => new IntPtr(1),
            _ => { });

        Assert.IsFalse(sent);
        CollectionAssert.AreEqual(new[] { expectedBatchSize }, batches);
        Assert.AreEqual(1, InjectionDiagnostics.SendInputFailures);
        InjectionDiagnostics.Reset();
    }

    [TestMethod]
    public void TypingAcceptsOnlyCompleteNewlineTabUnicodeAndSurrogateBatches()
    {
        var batches = new List<int>();

        Assert.IsTrue(Injector.SendText(
            "\r\n\tA😀",
            IntPtr.Zero,
            inputs =>
            {
                batches.Add(inputs.Length);
                return (uint)inputs.Length;
            },
            _ => true,
            () => new IntPtr(1),
            _ => { }));

        CollectionAssert.AreEqual(CompleteTypingBatchSizes, batches);
    }

    [TestMethod]
    public void PasteChordRequiresCompleteControlAndVDispatches()
    {
        AssertPasteChord(ControlDownFailure, expected: false, mayHavePasted: false, expectedBatchSizes: ControlOnlyBatchSizes);
        AssertPasteChord(PastePartialFailure, expected: false, mayHavePasted: true, expectedBatchSizes: PasteChordBatchSizes);
        AssertPasteChord(ControlUpFailure, expected: false, mayHavePasted: true, expectedBatchSizes: PasteChordBatchSizes);
        AssertPasteChord(CompletePasteChord, expected: true, mayHavePasted: true, expectedBatchSizes: PasteChordBatchSizes);
    }

    [TestMethod]
    public void AFailureAfterEraseIsTerminalAndCannotRetainUndo()
    {
        InjectionBackend originalBackend = Injector.Backend;
        try
        {
            InjectionDiagnostics.Reset();
            Injector.Backend = InjectionBackend.Typing;
            bool erased = false;
            bool cursorMoved = false;
            Assert.IsFalse(Injector.ReplaceCore(
                3,
                "replacement",
                IntPtr.Zero,
                2,
                _ => true,
                () => new IntPtr(1),
                erase: (_, _) =>
                {
                    erased = true;
                    return true;
                },
                sendText: (_, _) => false,
                sendLeft: (_, _) =>
                {
                    cursorMoved = true;
                    return true;
                }));
            Assert.IsTrue(erased);
            Assert.IsFalse(cursorMoved);
            Assert.AreEqual(0, InjectionDiagnostics.Typed);

            Injector.Backend = InjectionBackend.Clipboard;
            bool erasedTwice = false;
            bool typedFallback = false;
            Assert.IsFalse(Injector.ReplaceCore(
                3,
                "replacement",
                IntPtr.Zero,
                0,
                _ => true,
                () => new IntPtr(1),
                (_, _, _, _) => PasteOutcome.FailedAfterErase,
                (_, _) =>
                {
                    erasedTwice = true;
                    return true;
                },
                (_, _) =>
                {
                    typedFallback = true;
                    return true;
                }));
            Assert.IsFalse(erasedTwice);
            Assert.IsFalse(typedFallback);
            Assert.AreEqual(0, InjectionDiagnostics.Typed);
            Assert.AreEqual(1, InjectionDiagnostics.Abandoned);

            Assert.IsFalse(Program.ShouldRecordExpansion(inserted: false, sourceReplacement: "ordinary replacement"));
            Assert.IsTrue(Program.ShouldRecordExpansion(inserted: true, sourceReplacement: "ordinary replacement"));
            Assert.IsFalse(Program.ShouldRecordExpansion(inserted: true, sourceReplacement: "{{clipboard}}"));
        }
        finally
        {
            InjectionDiagnostics.Reset();
            Injector.Backend = originalBackend;
        }
    }

    private static void AssertPasteChord(
        uint[] dispatchResults,
        bool expected,
        bool mayHavePasted,
        int[] expectedBatchSizes)
    {
        InjectionDiagnostics.Reset();
        var results = new Queue<uint>(dispatchResults);
        var batches = new List<int>();

        bool sent = Injector.SendPasteChord(
            IntPtr.Zero,
            out bool actualMayHavePasted,
            inputs =>
            {
                batches.Add(inputs.Length);
                return results.Dequeue();
            },
            _ => true,
            () => new IntPtr(1),
            _ => 0,
            _ => { });

        Assert.AreEqual(expected, sent);
        Assert.AreEqual(mayHavePasted, actualMayHavePasted);
        CollectionAssert.AreEqual(expectedBatchSizes, batches);
        Assert.AreEqual(
            dispatchResults.Count(result => result == 0)
                + dispatchResults.Zip(expectedBatchSizes).Count(pair => pair.First > 0 && pair.First < pair.Second),
            InjectionDiagnostics.SendInputFailures);
        InjectionDiagnostics.Reset();
    }
}
