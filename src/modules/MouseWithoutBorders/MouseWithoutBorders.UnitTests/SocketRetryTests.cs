// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using MouseWithoutBorders.Class;

namespace MouseWithoutBorders.UnitTests;

[TestClass]
public sealed class SocketRetryTests
{
    private const string ThisMachine = "THIS-PC";

    private static readonly string[] Layout = ["THIS-PC", "OFFICE-PC", string.Empty, string.Empty];
    private static readonly string[] LayoutWithBlanks = [" this-pc ", null!, "   ", " OFFICE-PC "];
    private static readonly string[] LayoutWithSpaces = ["THIS-PC", " office-pc ", string.Empty, string.Empty];
    private static readonly string[] LayoutWithDuplicate = ["THIS-PC", "OFFICE-PC", "office-pc", string.Empty];
    private static readonly string[] LayoutWithLaptop = ["THIS-PC", "OFFICE-PC", "LAPTOP", string.Empty];
    private static readonly string[] OfficePc = ["OFFICE-PC"];
    private static readonly string[] LaptopOnly = ["LAPTOP"];
    private static readonly string[] OfficePcThenLaptop = ["OFFICE-PC", "LAPTOP"];

    [TestMethod]
    public void MachineWithoutSocketsIsRetried()
    {
        CollectionAssert.AreEqual(OfficePc, SocketStuff.GetMachinesToRetry(false, Layout, ThisMachine, []));
    }

    [DataTestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void MachineConnectedInEitherDirectionIsNotRetried(bool isClient)
    {
        CollectionAssert.AreEqual(Array.Empty<string>(), SocketStuff.GetMachinesToRetry(false, Layout, ThisMachine, [("OFFICE-PC", isClient, SocketStatus.Connected, true)]));
    }

    [DataTestMethod]
    [DataRow(nameof(SocketStatus.Connected), true)]
    [DataRow(nameof(SocketStatus.Connected), false)]
    [DataRow(nameof(SocketStatus.Handshaking), true)]
    public void SocketThatLostItsBackingSocketCountsForNothing(string status, bool isClient)
    {
        CollectionAssert.AreEqual(OfficePc, SocketStuff.GetMachinesToRetry(false, Layout, ThisMachine, [("OFFICE-PC", isClient, Enum.Parse<SocketStatus>(status), false)]));
    }

    [DataTestMethod]
    [DataRow(nameof(SocketStatus.NA))]
    [DataRow(nameof(SocketStatus.Error))]
    [DataRow(nameof(SocketStatus.ForceClosed))]
    [DataRow(nameof(SocketStatus.InvalidKey))]
    [DataRow(nameof(SocketStatus.Timeout))]
    [DataRow(nameof(SocketStatus.SendError))]
    public void MachineWhoseAttemptsEndedIsRetried(string status)
    {
        CollectionAssert.AreEqual(OfficePc, SocketStuff.GetMachinesToRetry(false, Layout, ThisMachine, [("OFFICE-PC", true, Enum.Parse<SocketStatus>(status), true)]));
    }

    [DataTestMethod]
    [DataRow(nameof(SocketStatus.Resolving))]
    [DataRow(nameof(SocketStatus.Connecting))]
    [DataRow(nameof(SocketStatus.Handshaking))]
    public void MachineWithAnAttemptInProgressIsNotRetried(string status)
    {
        CollectionAssert.AreEqual(Array.Empty<string>(), SocketStuff.GetMachinesToRetry(false, Layout, ThisMachine, [("OFFICE-PC", true, Enum.Parse<SocketStatus>(status), true)]));
    }

    [DataTestMethod]
    [DataRow(nameof(SocketStatus.Connecting))]
    [DataRow(nameof(SocketStatus.Handshaking))]
    public void IncomingConnectionInProgressDoesNotDelayTheRetry(string status)
    {
        CollectionAssert.AreEqual(OfficePc, SocketStuff.GetMachinesToRetry(false, Layout, ThisMachine, [("OFFICE-PC", false, Enum.Parse<SocketStatus>(status), true)]));
    }

    [TestMethod]
    public void ThisMachineAndEmptySlotsAreNotRetried()
    {
        CollectionAssert.AreEqual(OfficePc, SocketStuff.GetMachinesToRetry(false, LayoutWithBlanks, " This-PC ", []));
    }

    [TestMethod]
    public void MissingLayoutRetriesNothing()
    {
        CollectionAssert.AreEqual(Array.Empty<string>(), SocketStuff.GetMachinesToRetry(false, null!, ThisMachine, []));
    }

    [TestMethod]
    public void SocketNamesMatchIgnoringCaseAndSpaces()
    {
        CollectionAssert.AreEqual(Array.Empty<string>(), SocketStuff.GetMachinesToRetry(false, LayoutWithSpaces, ThisMachine, [(" Office-PC ", true, SocketStatus.Connected, true)]));
    }

    [DataTestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("$*NotUsed*$")]
    public void SocketsWithoutAUsableNameAreIgnored(string? machineName)
    {
        CollectionAssert.AreEqual(OfficePc, SocketStuff.GetMachinesToRetry(false, Layout, ThisMachine, [(machineName!, true, SocketStatus.Connected, true)]));
    }

    [TestMethod]
    public void MachineListedTwiceIsRetriedOnce()
    {
        CollectionAssert.AreEqual(OfficePc, SocketStuff.GetMachinesToRetry(false, LayoutWithDuplicate, ThisMachine, []));
    }

    [TestMethod]
    public void KeyMismatchStopsAllRetries()
    {
        CollectionAssert.AreEqual(Array.Empty<string>(), SocketStuff.GetMachinesToRetry(true, Layout, ThisMachine, []));
    }

    [TestMethod]
    public void EachMachineIsDecidedOnItsOwn()
    {
        CollectionAssert.AreEqual(LaptopOnly, SocketStuff.GetMachinesToRetry(false, LayoutWithLaptop, ThisMachine, [("OFFICE-PC", true, SocketStatus.Connected, true), ("LAPTOP", true, SocketStatus.Timeout, true)]));
    }

    [TestMethod]
    public void SeveralMachinesAreRetriedInLayoutOrder()
    {
        CollectionAssert.AreEqual(OfficePcThenLaptop, SocketStuff.GetMachinesToRetry(false, LayoutWithLaptop, ThisMachine, []));
    }

    [TestMethod]
    public void HelperTimerRetriesMachinesOnceAMinute()
    {
        // FrmScreen's timer cannot run in this unit test; check that its once-a-minute block ends with the retry.
        var screen = SettingsKeyTests.ReadRepositorySource(@"src\modules\MouseWithoutBorders\App\Form\frmScreen.cs");
        var minuteBlock = screen.IndexOf("if (count % 600 == 0 && Common.Sk != null)", StringComparison.Ordinal);
        Assert.IsTrue(minuteBlock >= 0, "The once-a-minute block is missing.");
        var prune = screen.IndexOf("Common.CloseAnUnusedSocket();", minuteBlock, StringComparison.Ordinal);
        Assert.IsTrue(prune > minuteBlock, "The once-a-minute block no longer prunes unused sockets.");

        StringAssert.Contains(screen[prune..screen.IndexOf('}', prune)], "Common.Sk?.RetryMachinesWithoutConnection();");
    }

    [TestMethod]
    public void RetryDoesNotDialOutFromClosedSockets()
    {
        // The key check that FrmScreen runs one minute after it opens the sockets can close them and leave Common.Sk
        // set. Close() clears skMessageServer, so the retry must return before it copies the socket list when that
        // field is null.
        var sockets = SettingsKeyTests.ReadRepositorySource(@"src\modules\MouseWithoutBorders\App\Class\SocketStuff.cs");
        StringAssert.Contains(sockets, "skMessageServer = null;");
        var retry = sockets.IndexOf("internal void RetryMachinesWithoutConnection()", StringComparison.Ordinal);
        Assert.IsTrue(retry >= 0, "The retry method is missing.");
        var snapshot = sockets.IndexOf("lock (TcpSocketsLock)", retry, StringComparison.Ordinal);
        Assert.IsTrue(snapshot > retry, "The retry no longer copies the socket list under its lock.");

        StringAssert.Contains(sockets[retry..snapshot], "if (skMessageServer == null)");
    }
}
