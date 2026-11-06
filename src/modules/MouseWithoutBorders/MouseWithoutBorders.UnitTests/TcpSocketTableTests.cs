// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Net;
using System.Net.Sockets;
using Microsoft.MouseWithoutBorders.UITests;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MouseWithoutBorders.UnitTests;

[TestClass]
public sealed class TcpSocketTableTests
{
    [DataTestMethod]
    [DataRow("0.0.0.0")]
    [DataRow("::")]
    public void ModuleReadinessRequiresBothOwnedListeners(string address)
    {
        var sockets = new[]
        {
            new TcpSocketTable.SocketInfo { OwningProcess = 123, LocalAddress = address, LocalPort = 15100, State = "Listen" },
            new TcpSocketTable.SocketInfo { OwningProcess = 123, LocalAddress = address, LocalPort = 15101, State = "Listen" },
        };
        Assert.IsTrue(TcpSocketTable.HasMwbListeners(123, sockets));
    }

    [DataTestMethod]
    [DataRow(123, 15100, 15100, "Listen")]
    [DataRow(123, 15101, 15101, "Listen")]
    [DataRow(456, 15100, 15101, "Listen")]
    [DataRow(123, 15100, 15101, "Established")]
    public void PartialForeignOrConnectedSocketsDoNotEstablishModuleReadiness(int owner, int firstPort, int secondPort, string state)
    {
        var sockets = new[]
        {
            new TcpSocketTable.SocketInfo { OwningProcess = owner, LocalPort = firstPort, State = state },
            new TcpSocketTable.SocketInfo { OwningProcess = owner, LocalPort = secondPort, State = state },
        };
        Assert.IsFalse(TcpSocketTable.HasMwbListeners(123, sockets));
        Assert.IsFalse(TcpSocketTable.HasMwbListeners(123, []));
    }

    [TestMethod]
    public void ListenersFromDifferentOwnersCannotBeCombined()
    {
        var sockets = new[]
        {
            new TcpSocketTable.SocketInfo { OwningProcess = 123, LocalPort = 15100, State = "Listen" },
            new TcpSocketTable.SocketInfo { OwningProcess = 456, LocalPort = 15101, State = "Listen" },
        };
        Assert.IsFalse(TcpSocketTable.HasMwbListeners(123, sockets));
    }

    [TestMethod]
    public void ModuleReadinessRejectsInvalidOwnerOrMissingSnapshot()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => TcpSocketTable.HasMwbListeners(0, []));
        Assert.ThrowsExactly<ArgumentNullException>(() => TcpSocketTable.HasMwbListeners(123, null!));
    }

    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void NativeSnapshotKeepsSocketAddressesPortsStatesAndOwner(bool ipv6)
    {
        var address = ipv6 ? IPAddress.IPv6Loopback : IPAddress.Loopback;
        using var listener = new TcpListener(address, 0);
        listener.Start();
        var endpoint = (IPEndPoint)listener.LocalEndpoint;
        using var client = new TcpClient(address.AddressFamily);
        client.Connect(endpoint);
        using var accepted = listener.AcceptTcpClient();
        var remote = (IPEndPoint)accepted.Client.RemoteEndPoint!;
        TcpSocketTable.SocketInfo[] sockets = [];
        RunFiles.Wait(
            () =>
            {
                sockets = TcpSocketTable.Read(Environment.ProcessId);
                return sockets.Any(socket => socket.LocalAddress == address.ToString() &&
                    socket.LocalPort == endpoint.Port && socket.RemotePort == remote.Port && socket.State == "Established");
            },
            TimeSpan.FromSeconds(10),
            "The native table did not report the owned loopback connection.");
        Assert.IsTrue(sockets.All(socket => socket.OwningProcess == Environment.ProcessId));
        Assert.IsTrue(sockets.Any(socket => socket.LocalAddress == address.ToString() &&
            socket.LocalPort == endpoint.Port && socket.State == "Listen"));
        Assert.IsTrue(sockets.Any(socket => socket.RemoteAddress == address.ToString() &&
            socket.LocalPort == remote.Port && socket.RemotePort == endpoint.Port && socket.State == "Established"));
        Assert.AreEqual(0, TcpSocketTable.Read(int.MaxValue).Length, "Unowned sockets must not be returned.");
    }
}
