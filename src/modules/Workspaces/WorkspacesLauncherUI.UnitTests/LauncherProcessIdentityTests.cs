// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WorkspacesLauncherUI.IPC;

namespace WorkspacesLauncherUI.UnitTests
{
    [TestClass]
    public sealed class LauncherProcessIdentityTests
    {
        [DataTestMethod]
        [DataRow("parent")]
        [DataRow("missing-own-image")]
        [DataRow("wrong-own-image")]
        [DataRow("wrong-parent-image")]
        [DataRow("not-sibling")]
        [DataRow("zero-version")]
        [DataRow("major")]
        [DataRow("minor")]
        [DataRow("build")]
        [DataRow("revision")]
        public void RejectsInvalidObservedIdentityTuples(string scenario)
        {
            var observation = LauncherPipeTestSession.ValidObservation(Environment.ProcessId);
            observation = scenario switch
            {
                "parent" => observation with { ParentProcessId = 0 },
                "missing-own-image" => observation with { OwnPath = null },
                "wrong-own-image" => observation with { OwnPath = @"C:\IPC-test-observation\Other.exe" },
                "wrong-parent-image" => observation with { ImagePath = @"C:\IPC-test-observation\Other.exe" },
                "not-sibling" => observation with { ImagePath = @"C:\Elsewhere\PowerToys.WorkspacesLauncher.exe" },
                "zero-version" => observation with { OwnVersion = default, ImageVersion = default },
                "major" => observation with { ImageVersion = (2, 2, 3, 4) },
                "minor" => observation with { ImageVersion = (1, 3, 3, 4) },
                "build" => observation with { ImageVersion = (1, 2, 4, 4) },
                _ => observation with { ImageVersion = (1, 2, 3, 5) },
            };
            Assert.ThrowsExactly<InvalidDataException>(() =>
            {
                using var identity = LauncherProcessIdentity.Open(Environment.ProcessId, _ => observation);
            });
        }

        [TestMethod]
        public void AcceptsNormalizedCaseInsensitiveSiblingAndNonzeroNumericVersion()
        {
            var observation = LauncherPipeTestSession.ValidObservation(Environment.ProcessId) with
            {
                OwnPath = @"C:\IPC-test-observation\POWerTOYS.WorkspacesLauncherUI.EXE",
                ImagePath = @"c:\ipc-test-observation\subdirectory\..\powertoys.workspaceslauncher.exe",
                OwnVersion = (0, 0, 0, 1),
                ImageVersion = (0, 0, 0, 1),
            };
            using var identity = LauncherProcessIdentity.Open(Environment.ProcessId, _ => observation);
            Assert.IsNotNull(identity);
        }

        [TestMethod]
        public void RealOsObservationRejectsNonParentAndNonProductTestHost()
        {
            var parentProcessId = LauncherProcessIdentity.GetParentProcessId();
            Assert.AreNotEqual(0U, parentProcessId);
            Assert.AreNotEqual((uint)Environment.ProcessId, parentProcessId);
            Assert.ThrowsExactly<InvalidDataException>(() =>
            {
                using var identity = LauncherProcessIdentity.Open(Environment.ProcessId);
            });
            Assert.ThrowsExactly<InvalidDataException>(() =>
            {
                using var identity = LauncherProcessIdentity.Open((int)parentProcessId);
            });
            Assert.ThrowsExactly<Win32Exception>(() =>
            {
                using var identity = LauncherProcessIdentity.Open(0);
            });
        }

        [TestMethod]
        public async Task ProductionConstructorRejectsRealNonProductParentWithoutConnectingOrSending()
        {
            var name = "PowerToys.Workspaces.UnitTests." + Guid.NewGuid().ToString("N");
            using var server = new NamedPipeServerStream(name, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
            await using var connection = new LauncherConnection((int)LauncherProcessIdentity.GetParentProcessId(), name);
            await connection.ConnectAsync().WaitAsync(LauncherPipeTestSession.Bound);
            Assert.IsTrue(connection.HasFailed);
            Assert.IsFalse(connection.IsOpen);
            Assert.IsFalse(await connection.SendReadyAsync());
            using var timeout = new CancellationTokenSource(150);
            await Assert.ThrowsAsync<OperationCanceledException>(() => server.WaitForConnectionAsync(timeout.Token));
        }

        [TestMethod]
        public async Task ActualLocalPipeServerPidMustMatchExpectedPeer()
        {
            await using var session = new LauncherPipeTestSession();
            await session.ConnectAsync();
            using var identity = LauncherPipeTestSession.OpenObservedProcess(Environment.ProcessId);
            var name = "PowerToys.Workspaces.UnitTests." + Guid.NewGuid().ToString("N");
            using var server = new NamedPipeServerStream(name, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
            using var client = LauncherPipeTestSession.CreateClient(name);
            using var timeout = new CancellationTokenSource(LauncherPipeTestSession.Bound);
            await Task.WhenAll(server.WaitForConnectionAsync(timeout.Token), client.ConnectAsync(timeout.Token));
            identity.AuthenticatePipe(client, Environment.ProcessId);
            Assert.ThrowsExactly<InvalidDataException>(() => identity.AuthenticatePipe(client, 0));
        }

        [TestMethod]
        public async Task ConnectionRejectsActualPipeOwnedByDifferentLiveProcess()
        {
            await using var child = await LauncherPipeChild.StartAsync();
            using var client = LauncherPipeTestSession.CreateClient(child.PipeName);
            await using var connection = new LauncherConnection(Environment.ProcessId, client, LauncherPipeTestSession.OpenObservedProcess, 2000, 1000, 500);
            await connection.ConnectAsync().WaitAsync(LauncherPipeTestSession.Bound);
            Assert.IsTrue(connection.HasFailed);
            Assert.IsFalse(connection.IsOpen);
            Assert.IsFalse(await connection.SendReadyAsync());
        }

        [TestMethod]
        public async Task RetainedNativeProcessHandleWaitsForExitAndCancellationIsIndependent()
        {
            await using var child = await LauncherPipeChild.StartAsync();
            using var client = LauncherPipeTestSession.CreateClient(child.PipeName);
            using var timeout = new CancellationTokenSource(LauncherPipeTestSession.Bound);
            await client.ConnectAsync(timeout.Token);
            await child.WaitForConnectionAsync();
            using var identity = LauncherPipeTestSession.OpenObservedProcess(child.ProcessId);
            identity.AuthenticatePipe(client, child.ProcessId);
            using var cancellation = new CancellationTokenSource();
            var canceledWait = identity.WaitForExitAsync(cancellation.Token);
            await cancellation.CancelAsync();
            await Assert.ThrowsAsync<OperationCanceledException>(() => canceledWait);
            var exited = identity.WaitForExitAsync(timeout.Token);
            Assert.IsFalse(exited.IsCompleted);
            await child.ExitAsync();
            await exited.WaitAsync(LauncherPipeTestSession.Bound);
            Assert.ThrowsExactly<InvalidDataException>(() =>
            {
                using var reopened = LauncherPipeTestSession.OpenObservedProcess(child.ProcessId);
            });
        }
    }
}
