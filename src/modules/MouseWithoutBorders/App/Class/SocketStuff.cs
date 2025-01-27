// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using MouseWithoutBorders.Core;

// <summary>
//     Socket code.
// </summary>
// <history>
//     2008 created by Truong Do (ductdo).
//     2009-... modified by Truong Do (TruongDo).
//     2023- Included in PowerToys.
// </history>
using MouseWithoutBorders.Exceptions;

using Thread = MouseWithoutBorders.Core.Thread;

[module: SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Scope = "member", Target = "MouseWithoutBorders.SocketStuff.#SendData(System.Byte[])", Justification = "Dotnet port with style preservation")]
[module: SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Scope = "member", Target = "MouseWithoutBorders.SocketStuff.#Close()", Justification = "Dotnet port with style preservation")]
[module: SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Scope = "member", Target = "MouseWithoutBorders.SocketStuff.#CreateSocket(System.Boolean)", Justification = "Dotnet port with style preservation")]
[module: SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Scope = "member", Target = "MouseWithoutBorders.SocketStuff.#SendData(System.Byte[],MouseWithoutBorders.IP)", Justification = "Dotnet port with style preservation")]
[module: SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Scope = "member", Target = "MouseWithoutBorders.SocketStuff.#SendData(System.Object)", Justification = "Dotnet port with style preservation")]
[module: SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Scope = "member", Target = "MouseWithoutBorders.SocketStuff.#SendFile(System.Net.Sockets.Socket,System.String)", Justification = "Dotnet port with style preservation")]
[module: SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Scope = "member", Target = "MouseWithoutBorders.SocketStuff.#MainTCPRoutine(System.Net.Sockets.Socket,System.String)", Justification = "Dotnet port with style preservation")]
[module: SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Scope = "member", Target = "MouseWithoutBorders.SocketStuff.#TCPServerThread(System.Object)", Justification = "Dotnet port with style preservation")]
[module: SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Scope = "member", Target = "MouseWithoutBorders.SocketStuff.#SendClipboardData(System.Object)", Justification = "Dotnet port with style preservation")]
[module: SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Scope = "member", Target = "MouseWithoutBorders.SocketStuff.#StartNewTcpClient(MouseWithoutBorders.MachineInf)", Justification = "Dotnet port with style preservation")]
[module: SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Scope = "member", Target = "MouseWithoutBorders.SocketStuff.#StartNewTcpServer(System.Net.Sockets.Socket,System.String)", Justification = "Dotnet port with style preservation")]
[module: SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Scope = "member", Target = "MouseWithoutBorders.SocketStuff.#UpdateTCPClients()", Justification = "Dotnet port with style preservation")]
[module: SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Scope = "member", Target = "MouseWithoutBorders.SocketStuff.#UpdateTcpSockets(System.Net.Sockets.Socket,MouseWithoutBorders.SocketStatus)", Justification = "Dotnet port with style preservation")]
[module: SuppressMessage("Microsoft.Usage", "CA2201:DoNotRaiseReservedExceptionTypes", Scope = "member", Target = "MouseWithoutBorders.SocketStuff.#.ctor(System.Int32,System.Boolean)", Justification = "Dotnet port with style preservation")]
[module: SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Scope = "member", Target = "MouseWithoutBorders.SocketStuff.#SendData(System.Byte[],MouseWithoutBorders.IP,System.Int32)", Justification = "Dotnet port with style preservation")]
[module: SuppressMessage("Microsoft.Design", "CA1001:TypesThatOwnDisposableFieldsShouldBeDisposable", Scope = "type", Target = "MouseWithoutBorders.SocketStuff", Justification = "Dotnet port with style preservation")]

namespace MouseWithoutBorders.Class
{
    internal enum SocketStatus : int
    {
        NA = 0,
        Resolving = 1,
        Connecting = 2,
        Handshaking = 3,
        Error = 4,
        ForceClosed = 5,
        InvalidKey = 6,
        Timeout = 7,
        SendError = 8,
        Connected = 9,
    }

    internal class TcpSk : IDisposable
    {
        public TcpSk(bool isClient, Socket s, SocketStatus status, string machineName, IPAddress address = null)
        {
            IsClient = isClient;
            BackingSocket = s;
            Status = status;
            MachineName = machineName;
            Address = address;
            BirthTime = Common.GetTick();
        }

        public bool IsClient { get; set; }

        public Socket BackingSocket { get; set; }

        public SocketStatus Status { get; set; }

        public string MachineName { get; set; }

        public long BirthTime { get; set; }

        public uint MachineId { get; set; }

        public IPAddress Address { get; set; }

        private Stream encryptedStream;
        private Stream decryptedStream;
        private Stream socketStream;

        public Stream EncryptedStream
        {
            get
            {
                if (encryptedStream == null && BackingSocket.Connected)
                {
                    encryptedStream = Common.GetEncryptedStream(new NetworkStream(BackingSocket));
                    Common.SendOrReceiveARandomDataBlockPerInitialIV(encryptedStream);
                }

                return encryptedStream;
            }
        }

        public Stream DecryptedStream
        {
            get
            {
                if (decryptedStream == null && BackingSocket.Connected)
                {
                    decryptedStream = Common.GetDecryptedStream(new NetworkStream(BackingSocket));
                    Common.SendOrReceiveARandomDataBlockPerInitialIV(decryptedStream, false);
                }

                return decryptedStream;
            }
        }

        public Stream SocketStream
        {
            get
            {
                if (socketStream == null && BackingSocket.Connected)
                {
                    socketStream = new NetworkStream(BackingSocket);
                }

                return socketStream;
            }
        }

        public void Dispose()
        {
            encryptedStream?.Dispose();

            decryptedStream?.Dispose();

            socketStream?.Dispose();
        }
    }

    internal class SocketStuff
    {
        private readonly int bASE_PORT;
        private TcpServer skClipboardServer;
        private TcpServer skMessageServer;
        internal object TcpSocketsLock = new();
        internal static bool InvalidKeyFound;
        internal static bool InvalidKeyFoundOnClientSocket;

        internal const int CONNECT_TIMEOUT = 60000;
        private static readonly ConcurrentDictionary<string, int> FailedAttempt = new();

        internal List<TcpSk> TcpSockets
        {
            get; private set;

            // set { tcpSockets = value; }
        }

        internal int TcpPort
        {
            get;

            // set { tcpPort = value; }
        }

        private static bool firstRun;
        private readonly long connectTimeout;
        private static int restartCount;

        internal SocketStuff(int port, bool byUser)
        {
            Logger.LogDebug("SocketStuff started.");

            bASE_PORT = port;
            Common.Ran = new Random();

            Logger.LogDebug("Validating session...");

            if (Common.CurrentProcess.SessionId != NativeMethods.WTSGetActiveConsoleSessionId())
            {
                if (Common.DesMachineID != Common.MachineID)
                {
                    MachineStuff.SwitchToMultipleMode(false, true);
                }

                if (!Common.RunOnLogonDesktop && !Common.RunOnScrSaverDesktop)
                {
                    Common.MainForm.SetTrayIconText("Not physical console session.");
                    if (byUser)
                    {
                        Common.ShowToolTip("Not physical console session.", 5000);
                    }
                }

                Common.MMSleep(1);
                Logger.Log("Not physical console session.");

                throw new NotPhysicalConsoleException("Not physical console session.");
            }

            Logger.LogDebug("Creating socket list and mutex...");

            try
            {
                lock (TcpSocketsLock)
                {
                    TcpSockets = new List<TcpSk>();
                }

                bool dummy1 = Setting.Values.MatrixOneRow; // Reading from reg to variable
                dummy1 = Setting.Values.MatrixCircle;

                if (Setting.Values.IsMyKeyRandom)
                {
                    Setting.Values.MyKey = Common.MyKey;
                }

                Common.MagicNumber = Common.Get24BitHash(Common.MyKey);
                Common.PackageID = Setting.Values.PackageID;

                TcpPort = bASE_PORT;

                if (Common.SocketMutex == null)
                {
                    firstRun = true;
                    Common.SocketMutex = new Mutex(false, $"Global\\{Application.ProductName}-{FrmAbout.AssemblyVersion}-FF7CDABE-1015-0904-1103-24670FA5D16E");
                }

                Common.AcquireSocketMutex();
            }
            catch (AbandonedMutexException e)
            {
                Logger.TelemetryLogTrace($"{nameof(SocketStuff)}: {e.Message}", SeverityLevel.Warning);
            }

            Common.GetScreenConfig();

            if (firstRun && Common.RunOnScrSaverDesktop)
            {
                firstRun = false;
            }

            // JUST_GOT_BACK_FROM_SCREEN_SAVER: For bug: monitor does not turn off after logon screen saver exits
            else if (!Common.RunOnScrSaverDesktop)
            {
                if (Setting.Values.LastX == Common.JUST_GOT_BACK_FROM_SCREEN_SAVER)
                {
                    MachineStuff.NewDesMachineID = Common.DesMachineID = Common.MachineID;
                }
                else
                {
                    // Common.Log("Getting IP: " + Setting.Values.DesMachineID.ToString(CultureInfo.CurrentCulture));
                    Common.LastX = Setting.Values.LastX;
                    Common.LastY = Setting.Values.LastY;

                    if (Common.RunOnLogonDesktop && Setting.Values.DesMachineID == (uint)ID.ALL)
                    {
                        MachineStuff.SwitchToMultipleMode(true, false);
                    }
                    else
                    {
                        MachineStuff.SwitchToMultipleMode(false, false);
                    }
                }
            }

            connectTimeout = Common.GetTick() + (CONNECT_TIMEOUT / 2);
            Exception openSocketErr;

            /*
             * The machine might be getting a new IP address from its DHCP server
             * for ex, when a laptop with a wireless connection just wakes up, might take long time:(
             * */

            Common.GetMachineName(); // IPs might have been changed
            Common.UpdateMachineTimeAndID();

            Logger.LogDebug("Creating sockets...");

            openSocketErr = CreateSocket();

            int sleepSecs = 0, errCode = 0;

            if (openSocketErr != null)
            {
                if (openSocketErr is SocketException)
                {
                    errCode = (openSocketErr as SocketException).ErrorCode;

                    switch (errCode)
                    {
                        case 0: // No error.
                            break;

                        case 10048: // WSAEADDRINUSE
                            sleepSecs = 10;

                            // It is reasonable to give a try on restarting MwB processes in other sessions.
                            if (restartCount++ < 5 && Common.IsMyDesktopActive() && !Common.RunOnLogonDesktop && !Common.RunOnScrSaverDesktop)
                            {
                                Logger.TelemetryLogTrace("Restarting the service dues to WSAEADDRINUSE.", SeverityLevel.Warning);
                                Program.StartService();
                                Common.PleaseReopenSocket = Common.REOPEN_WHEN_WSAECONNRESET;
                            }

                            break;

                        case 10049: // WSAEADDRNOTAVAIL
                            sleepSecs = 1;
                            break;

                        default:
                            sleepSecs = 5;
                            break;
                    }
                }
                else
                {
                    sleepSecs = 10;
                }

                if (!Common.RunOnLogonDesktop && !Common.RunOnScrSaverDesktop)
                {
                    if (byUser)
                    {
                        Common.ShowToolTip(errCode.ToString(CultureInfo.CurrentCulture) + ": " + openSocketErr.Message, 5000, ToolTipIcon.Warning, Setting.Values.ShowClipNetStatus);
                    }
                }

                Common.MMSleep(sleepSecs);
                Common.ReleaseSocketMutex();
                throw new ExpectedSocketException(openSocketErr.Message);
            }
            else
            {
                restartCount = 0;

                if (!Common.RunOnLogonDesktop && !Common.RunOnScrSaverDesktop)
                {
                    IpcHelper.CreateIpcServer(false);
                }

                Common.MainForm.UpdateNotifyIcon();
            }
        }

        internal void Close(bool sentWait)
        {
            try
            {
                if (!Common.RunOnScrSaverDesktop)
                {
                    Setting.Values.LastX = Common.LastX;
                    Setting.Values.LastY = Common.LastY;
                    Setting.Values.PackageID = Common.PackageID;

                    // Common.Log("Saving IP: " + Setting.Values.DesMachineID.ToString(CultureInfo.CurrentCulture));
                    Setting.Values.DesMachineID = (uint)Common.DesMachineID;
                }

                _ = Common.ExecuteAndTrace(
                    "Closing sockets",
                    () =>
                    {
                        Logger.LogDebug($"Closing socket [{skMessageServer?.Name}].");
                        skMessageServer?.Close(); // The original ones, not the socket instances produced by the accept() method.
                        skMessageServer = null;

                        Logger.LogDebug($"Closing socket [{skClipboardServer?.Name}].");
                        skClipboardServer?.Close();
                        skClipboardServer = null;
                        try
                        {
                            // If these sockets are failed to be closed then the tool would not function properly, more logs are added for debugging.
                            lock (TcpSocketsLock)
                            {
                                int c = 0;

                                if (TcpSockets != null)
                                {
                                    Logger.LogDebug("********** Closing Sockets: " + TcpSockets.Count.ToString(CultureInfo.InvariantCulture));

                                    List<TcpSk> notClosedSockets = new();

                                    foreach (TcpSk t in TcpSockets)
                                    {
                                        if (t != null && t.BackingSocket != null && t.Status != SocketStatus.Resolving)
                                        {
                                            try
                                            {
                                                t.MachineName = "$*NotUsed*$";
                                                t.Status = t.Status >= 0 ? 0 : t.Status - 1;

                                                if (sentWait)
                                                {
                                                    t.BackingSocket.Close(1);
                                                }
                                                else
                                                {
                                                    t.BackingSocket.Close();
                                                }

                                                c++;

                                                continue;
                                            }
                                            catch (SocketException e)
                                            {
                                                string log = $"Socket.Close error: {e.GetType()}/{e.Message}. This is expected when the socket is already closed by remote host.";
                                                Logger.Log(log);
                                            }
                                            catch (ObjectDisposedException e)
                                            {
                                                string log = $"Socket.Close error: {e.GetType()}/{e.Message}. This is expected when the socket is already disposed.";
                                                Logger.Log(log);
                                            }
                                            catch (Exception e)
                                            {
                                                Logger.Log(e);
                                            }

                                            // If there was an error closing the socket:
                                            if ((int)t.Status > -5)
                                            {
                                                notClosedSockets.Add(t); // Try to give a few times to close the socket later on.
                                            }
                                        }
                                    }

                                    TcpSockets = notClosedSockets;
                                }

                                Logger.LogDebug("********** Sockets Closed: " + c.ToString(CultureInfo.CurrentCulture));
                            }
                        }
                        catch (Exception e)
                        {
                            Logger.Log(e);
                        }
                    },
                    TimeSpan.FromSeconds(3),
                    true);
            }
            catch (Exception e)
            {
                Logger.Log(e);
            }
            finally
            {
                Common.ReleaseSocketMutex();
            }

            if (!Common.RunOnLogonDesktop && !Common.RunOnScrSaverDesktop)
            {
                try
                {
                    IpcHelper.CreateIpcServer(true);
                }
                catch (Exception e)
                {
                    Logger.Log(e);
                }
            }
        }

        private Exception CreateSocket()
        {
            try
            {
                skMessageServer = new TcpServer(TcpPort + 1, new ParameterizedThreadStart(TCPServerThread));
                skClipboardServer = new TcpServer(TcpPort, new ParameterizedThreadStart(AcceptConnectionAndSendClipboardData));
            }
            catch (SocketException e)
            {
                Logger.Log(e);
                return e;
            }
            catch (Exception e)
            {
                Logger.Log(e);
                return e;
            }

            Logger.LogDebug("==================================================");
            return null;
        }

        private static int TcpSendData(TcpSk tcp, byte[] bytes)
        {
            Stream encryptedStream = tcp.EncryptedStream;

            if (tcp.BackingSocket == null || !tcp.BackingSocket.Connected || encryptedStream == null)
            {
                string log = $"{nameof(TcpSendData)}: The socket is no longer connected, it could have been closed by the remote host.";
                Logger.Log(log);
                throw new ExpectedSocketException(log);
            }

            bytes[3] = (byte)((Common.MagicNumber >> 24) & 0xFF);
            bytes[2] = (byte)((Common.MagicNumber >> 16) & 0xFF);
            bytes[1] = 0;
            for (int i = 2; i < Common.PACKAGE_SIZE; i++)
            {
                bytes[1] = (byte)(bytes[1] + bytes[i]);
            }

            try
            {
                encryptedStream.Write(bytes, 0, bytes.Length);
            }
            catch (IOException e)
            {
                string log = $"{nameof(TcpSendData)}: Exception writing to the socket: {tcp.MachineName}: {e.InnerException?.GetType()}/{e.Message}. (This is expected when the remote machine closes the connection during desktop switch or reconnection.)";
                Logger.Log(log);

                throw e.InnerException is SocketException se ? new ExpectedSocketException(se) : new ExpectedSocketException(log);
            }

            return bytes.Length;
        }

        private static void ProcessReceivedDataEx(byte[] buf)
        {
            int magic;
            byte checksum = 0;

            magic = (buf[3] << 24) + (buf[2] << 16);

            if (magic != (Common.MagicNumber & 0xFFFF0000))
            {
                Logger.Log("Magic number invalid!");
                buf[0] = (byte)PackageType.Invalid;
            }

            for (int i = 2; i < Common.PACKAGE_SIZE; i++)
            {
                checksum = (byte)(checksum + buf[i]);
            }

            if (buf[1] != checksum)
            {
                Logger.Log("Checksum invalid!");
                buf[0] = (byte)PackageType.Invalid;
            }

            buf[3] = buf[2] = buf[1] = 0;
        }

        internal static DATA TcpReceiveData(TcpSk tcp, out int bytesReceived)
        {
            byte[] buf = new byte[Common.PACKAGE_SIZE_EX];
            Stream decryptedStream = tcp.DecryptedStream;

            if (tcp.BackingSocket == null || !tcp.BackingSocket.Connected || decryptedStream == null)
            {
                string log = $"{nameof(TcpReceiveData)}: The socket is no longer connected, it could have been closed by the remote host.";
                Logger.Log(log);
                throw new ExpectedSocketException(log);
            }

            DATA package;

            try
            {
                bytesReceived = decryptedStream.ReadEx(buf, 0, Common.PACKAGE_SIZE);

                if (bytesReceived != Common.PACKAGE_SIZE)
                {
                    buf[0] = bytesReceived == 0 ? (byte)PackageType.Error : (byte)PackageType.Invalid;
                }
                else
                {
                    ProcessReceivedDataEx(buf);
                }

                package = new DATA(buf);

                if (package.IsBigPackage)
                {
                    bytesReceived = decryptedStream.ReadEx(buf, Common.PACKAGE_SIZE, Common.PACKAGE_SIZE);

                    if (bytesReceived != Common.PACKAGE_SIZE)
                    {
                        buf[0] = bytesReceived == 0 ? (byte)PackageType.Error : (byte)PackageType.Invalid;
                    }
                    else
                    {
                        package.Bytes = buf;
                    }
                }
            }
            catch (IOException e)
            {
                string log = $"{nameof(TcpReceiveData)}: Exception reading from the socket: {tcp.MachineName}: {e.InnerException?.GetType()}/{e.Message}. (This is expected when the remote machine closes the connection during desktop switch or reconnection.)";
                Logger.Log(log);

                throw e.InnerException is SocketException se ? new ExpectedSocketException(se) : new ExpectedSocketException(log);
            }

            return package;
        }

        private static void PreProcessData(PackageType type)
        {
            switch (type)
            {
                case PackageType.Keyboard:
                    Common.PackageSent.Keyboard++;
                    break;

                case PackageType.Mouse:
                    Common.PackageSent.Mouse++;
                    break;

                case PackageType.Heartbeat:
                case PackageType.Heartbeat_ex:
                    Common.PackageSent.Heartbeat++;
                    break;

                case PackageType.Hello:
                    Common.PackageSent.Hello++;
                    break;

                case PackageType.ByeBye:
                    Common.PackageSent.ByeBye++;
                    break;

                case PackageType.Matrix:
                    Common.PackageSent.Matrix++;
                    break;

                default:
                    byte subtype = (byte)((uint)type & 0x000000FF);
                    switch (subtype)
                    {
                        case (byte)PackageType.ClipboardText:
                            Common.PackageSent.ClipboardText++;
                            break;

                        case (byte)PackageType.ClipboardImage:
                            Common.PackageSent.ClipboardImage++;
                            break;

                        default:
                            // Common.Log("Send: Other type (1-17)");
                            break;
                    }

                    break;
            }
        }

        internal int TcpSend(TcpSk tcp, DATA data)
        {
            PreProcessData(data.Type);

            if (data.Src == ID.NONE)
            {
                data.Src = Common.MachineID;
            }

            byte[] dataAsBytes = data.Bytes;
            int rv = TcpSendData(tcp, dataAsBytes);
            if (rv < dataAsBytes.Length)
            {
                Logger.Log("TcpSend error! Length of sent data is unexpected.");
                UpdateTcpSockets(tcp, SocketStatus.SendError);
                throw new SocketException((int)SocketStatus.SendError);
            }

            return rv;
        }

        private void TCPServerThread(object param)
        {
            // SuppressFlow fixes an issue on service mode, where the helper process can't get enough permissions to be started again.
            // More details can be found on: https://github.com/microsoft/PowerToys/pull/36892
            using var asyncFlowControl = ExecutionContext.SuppressFlow();

            try
            {
                TcpListener server = param as TcpListener;
                do
                {
                    Logger.LogDebug("TCPServerThread: Waiting for request...");
                    Socket s = server.AcceptSocket();

                    _ = Task.Run(() =>
                    {
                        try
                        {
                            AddSocket(s);
                        }
                        catch (Exception e)
                        {
                            Logger.Log(e);
                        }
                    });
                }
                while (true);
            }
            catch (InvalidOperationException e)
            {
                string log = $"TCPServerThread.AcceptSocket: The server socket could have been closed. {e.Message}";
                Logger.Log(log);
            }
            catch (SocketException e)
            {
                if (e.ErrorCode == (int)SocketError.Interrupted)
                {
                    Logger.Log("TCPServerThread.AcceptSocket: A blocking socket call was canceled.");
                }
                else
                {
                    Logger.Log(e);
                }
            }
            catch (Exception e)
            {
                Logger.Log(e);
            }
        }

        private static string GetMachineNameFromSocket(Socket socket)
        {
            string stringIP = socket.RemoteEndPoint.ToString();
            string name = null;

            try
            {
                // Remote machine has IP changed, update it.
                name = Dns.GetHostEntry((socket.RemoteEndPoint as IPEndPoint).Address).HostName;
            }
            catch (SocketException e)
            {
                Logger.Log($"{nameof(GetMachineNameFromSocket)}: {e.Message}");
                return stringIP;
            }

            // Remove the domain part.
            if (!string.IsNullOrEmpty(name))
            {
                int dotPos = name.IndexOf('.');

                if (dotPos > 0)
                {
                    Logger.LogDebug("Removing domain part from the full machine name: {0}.", name);
                    name = name[..dotPos];
                }
            }

            return string.IsNullOrEmpty(name) ? stringIP : name;
        }

        private void AddSocket(Socket s)
        {
            string machineName = GetMachineNameFromSocket(s);
            Logger.Log($"New connection from client: [{machineName}].");
            TcpSk tcp = AddTcpSocket(false, s, SocketStatus.Connecting, machineName);
            StartNewTcpServer(tcp, machineName);
        }

        private void StartNewTcpServer(TcpSk tcp, string machineName)
        {
            void ServerThread()
            {
                // SuppressFlow fixes an issue on service mode, where the helper process can't get enough permissions to be started again.
                // More details can be found on: https://github.com/microsoft/PowerToys/pull/36892
                using var asyncFlowControl = ExecutionContext.SuppressFlow();

                try
                {
                    // Receiving packages
                    MainTCPRoutine(tcp, machineName, false);
                }
                catch (Exception e)
                {
                    Logger.Log(e);
                }
            }

            Thread t = new(ServerThread, "TCP Server Thread " + tcp.BackingSocket.LocalEndPoint.ToString() + " : " + machineName);

            t.SetApartmentState(ApartmentState.STA);
            t.Start();
        }

        internal void UpdateTCPClients()
        {
            if (InvalidKeyFound)
            {
                return;
            }

            Logger.LogDebug("!!!!! UpdateTCPClients !!!!!");

            try
            {
                if (MachineStuff.MachineMatrix != null)
                {
                    Logger.LogDebug("MachineMatrix = " + string.Join(", ", MachineStuff.MachineMatrix));

                    foreach (string st in MachineStuff.MachineMatrix)
                    {
                        string machineName = st.Trim();
                        if (!string.IsNullOrEmpty(machineName) &&
                            !machineName.Equals(Common.MachineName.Trim(), StringComparison.OrdinalIgnoreCase))
                        {
                            bool found = false;

                            found = Common.IsConnectedByAClientSocketTo(machineName);

                            if (found)
                            {
                                Logger.LogDebug(machineName + " is already connected! ^^^^^^^^^^^^^^^^^^^^^");
                                continue;
                            }

                            StartNewTcpClient(machineName);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Log(e);
            }
        }

        private static readonly Dictionary<string, List<IPAddress>> BadIPs = new();
        private static readonly Lock BadIPsLock = new();

        private static bool IsBadIP(string machineName, IPAddress ip)
        {
            lock (BadIPsLock)
            {
                return BadIPs.ContainsKey(machineName) && BadIPs.TryGetValue(machineName, out List<IPAddress> ips) && ips.Contains(ip);
            }
        }

        private static void AddBadIP(string machineName, IPAddress ip)
        {
            if (!IsBadIP(machineName, ip))
            {
                lock (BadIPsLock)
                {
                    List<IPAddress> ips;

                    if (BadIPs.ContainsKey(machineName))
                    {
                        _ = BadIPs.TryGetValue(machineName, out ips);
                    }
                    else
                    {
                        ips = new List<IPAddress>();
                        BadIPs.Add(machineName, ips);
                    }

                    ips.Add(ip);
                }
            }
        }

        internal static void ClearBadIPs()
        {
            lock (BadIPsLock)
            {
                if (BadIPs.Count > 0)
                {
                    BadIPs.Clear();
                }
            }
        }

        internal void StartNewTcpClient(string machineName)
        {
            void ClientThread(object obj)
            {
                // SuppressFlow fixes an issue on service mode, where the helper process can't get enough permissions to be started again.
                // More details can be found on: https://github.com/microsoft/PowerToys/pull/36892
                using var asyncFlowControl = ExecutionContext.SuppressFlow();

                IPHostEntry host;
                bool useName2IP = false;
                List<IPAddress> validAddresses = new();
                List<IPAddress> validatedAddresses = new();
                string validAddressesSt = string.Empty;

                // Add a dummy socket to show the status.
                Socket dummySocket = new(AddressFamily.Unspecified, SocketType.Stream, ProtocolType.Tcp);
                TcpSk dummyTcp = AddTcpSocket(true, dummySocket, SocketStatus.Resolving, machineName);

                Logger.LogDebug("Connecting to: " + machineName);

                if (!string.IsNullOrEmpty(Setting.Values.Name2IP))
                {
                    string combinedName2ipList = Setting.Values.Name2IpPolicyList + Separator + Setting.Values.Name2IP;
                    string[] name2ip = combinedName2ipList.Split(Separator, StringSplitOptions.RemoveEmptyEntries);
                    string[] nameNip;

                    if (name2ip != null)
                    {
                        foreach (string st in name2ip)
                        {
                            nameNip = st.Split(BlankSeparator, StringSplitOptions.RemoveEmptyEntries);

                            if (nameNip != null && nameNip.Length >= 2 && nameNip[0].Trim().Equals(machineName, StringComparison.OrdinalIgnoreCase)
                                && IPAddress.TryParse(nameNip[1].Trim(), out IPAddress ip) && !validAddressesSt.Contains("[" + ip.ToString() + "]")
                                )
                            {
                                validatedAddresses.Add(ip);
                                validAddressesSt += "[" + ip.ToString() + "]";
                            }
                        }
                    }

                    if (validatedAddresses.Count > 0)
                    {
                        useName2IP = true;

                        Logger.LogDebug("Using both user-defined Name-to-IP mappings and DNS result for " + machineName);

                        Common.ShowToolTip("Using both user-defined Name-to-IP mappings and DNS result for " + machineName, 3000, ToolTipIcon.Info, false);

                        if (!CheckForSameSubNet(validatedAddresses, machineName))
                        {
                            return;
                        }

                        foreach (IPAddress vip in validatedAddresses)
                        {
                            StartNewTcpClientThread(machineName, vip);
                        }

                        validatedAddresses.Clear();
                    }
                }

                try
                {
                    host = Dns.GetHostEntry(machineName);
                }
                catch (SocketException e)
                {
                    host = null;

                    UpdateTcpSockets(dummyTcp, SocketStatus.Timeout);

                    Common.ShowToolTip(e.Message + ": " + machineName, 10000, ToolTipIcon.Warning, Setting.Values.ShowClipNetStatus);

                    Logger.Log($"{nameof(StartNewTcpClient)}.{nameof(Dns.GetHostEntry)}: {e.Message}");
                }

                UpdateTcpSockets(dummyTcp, SocketStatus.NA);

                if (!MachineStuff.InMachineMatrix(machineName))
                {
                    // While Resolving from name to IP, user may have changed the machine name and clicked on Apply.
                    return;
                }

                if (host != null)
                {
                    string ipLog = string.Empty;

                    foreach (IPAddress ip in host.AddressList)
                    {
                        ipLog += "<" + ip.ToString() + ">";

                        if ((ip.AddressFamily == AddressFamily.InterNetwork || ip.AddressFamily == AddressFamily.InterNetworkV6) && !validAddressesSt.Contains("[" + ip.ToString() + "]"))
                        {
                            validAddresses.Add(ip);
                            validAddressesSt += "[" + ip.ToString() + "]";
                        }
                    }

                    Logger.LogDebug(machineName + ipLog);
                }

                if (validAddresses.Count > 0)
                {
                    if (!Setting.Values.ReverseLookup)
                    {
                        validatedAddresses = validAddresses;
                        ClearBadIPs();
                    }
                    else
                    {
                        foreach (IPAddress ip in validAddresses)
                        {
                            if (IsBadIP(machineName, ip))
                            {
                                Logger.Log($"Skip bad IP address: {ip}");
                                continue;
                            }

                            try
                            {
                                // Reverse lookup to validate the IP Address.
                                string hn = Dns.GetHostEntry(ip).HostName;

                                if (hn.StartsWith(machineName, StringComparison.CurrentCultureIgnoreCase) || hn.Equals(ip.ToString(), StringComparison.OrdinalIgnoreCase))
                                {
                                    validatedAddresses.Add(ip);
                                }
                                else
                                {
                                    Logger.Log($"DNS information of machine not matched: {machineName} => {ip} => {hn}.");
                                    AddBadIP(machineName, ip);
                                }
                            }
                            catch (SocketException se)
                            {
                                Logger.Log($"{nameof(StartNewTcpClient)}: DNS information of machine not matched: {machineName} => {ip} => {se.Message}.");
                                AddBadIP(machineName, ip);
                            }
                            catch (ArgumentException ae)
                            {
                                Logger.Log($"{nameof(StartNewTcpClient)}: DNS information of machine not matched: {machineName} => {ip} => {ae.Message}.");
                                AddBadIP(machineName, ip);
                            }
                        }
                    }
                }

                if (validatedAddresses.Count > 0)
                {
                    if (!CheckForSameSubNet(validatedAddresses, machineName))
                    {
                        return;
                    }

                    foreach (IPAddress ip in validatedAddresses)
                    {
                        StartNewTcpClientThread(machineName, ip);
                    }
                }
                else
                {
                    Logger.Log("Cannot resolve IPv4 Addresses of machine: " + machineName);

                    if (!useName2IP)
                    {
                        Common.ShowToolTip($"Cannot resolve IP Address of the remote machine: {machineName}.\r\nPlease fix your DNS or use the Mapping option in the Settings form.", 10000, ToolTipIcon.Warning, Setting.Values.ShowClipNetStatus);
                    }
                }
            }

            Thread t = new(
                ClientThread, "StartNewTcpClient." + machineName);

            t.SetApartmentState(ApartmentState.STA);
            t.Start();
        }

        private bool CheckForSameSubNet(List<IPAddress> validatedAddresses, string machineName)
        {
            if (!Setting.Values.SameSubNetOnly)
            {
                return true;
            }

            // Only support if IPv4 addresses found in both.
            IEnumerable<IPAddress> remoteIPv4Addresses = validatedAddresses.Where(addr => addr?.AddressFamily == AddressFamily.InterNetwork);

            if (!remoteIPv4Addresses.Any())
            {
                Logger.Log($"No IPv4 resolved from the remote machine: {machineName}.");
                return true;
            }

            List<IPAddress> localIPv4Addresses = GetMyIPv4Addresses().ToList();

            if (localIPv4Addresses.Count == 0)
            {
                Logger.Log($"No IPv4 resolved from the local machine: {Common.MachineName}");
                return true;
            }

            foreach (IPAddress remote in remoteIPv4Addresses)
            {
                foreach (IPAddress local in localIPv4Addresses)
                {
                    byte[] myIPAddressBytes = local.GetAddressBytes();
                    byte[] yourIPAddressBytes = remote.GetAddressBytes();

                    // Same WAN?
                    if (myIPAddressBytes[0] == yourIPAddressBytes[0] && myIPAddressBytes[1] == yourIPAddressBytes[1])
                    {
                        return true;
                    }
                }
            }

            Logger.Log($"Skip machine not in the same network: {machineName}.");

            return false;
        }

        private IEnumerable<IPAddress> GetMyIPv4Addresses()
        {
            try
            {
                IEnumerable<IPAddress> ip4addresses = NetworkInterface.GetAllNetworkInterfaces()?
                    .Where(networkInterface =>
                        (networkInterface.NetworkInterfaceType == NetworkInterfaceType.Ethernet || networkInterface.NetworkInterfaceType == NetworkInterfaceType.Wireless80211)
                        && networkInterface.OperationalStatus == OperationalStatus.Up)
                        .SelectMany(ni => ni?.GetIPProperties()?.UnicastAddresses.Select(uni => uni?.Address))
                        .Where(addr => addr?.AddressFamily == AddressFamily.InterNetwork);

                return ip4addresses;
            }
            catch (Exception e)
            {
                Logger.Log(e);
                return Enumerable.Empty<IPAddress>();
            }
        }

        private void StartNewTcpClientThread(string machineName, IPAddress ip)
        {
            void NewTcpClient()
            {
                // SuppressFlow fixes an issue on service mode, where the helper process can't get enough permissions to be started again.
                // More details can be found on: https://github.com/microsoft/PowerToys/pull/36892
                using var asyncFlowControl = ExecutionContext.SuppressFlow();

                TcpClient tcpClient = null;

                try
                {
                    tcpClient = new TcpClient(AddressFamily.InterNetworkV6);
                    tcpClient.Client.DualMode = true;

                    if (Common.IsConnectedByAClientSocketTo(machineName))
                    {
                        Logger.LogDebug(machineName + " is already connected by another client socket.");
                        return;
                    }

                    if (Common.IsConnectingByAClientSocketTo(machineName, ip))
                    {
                        Logger.LogDebug($"{machineName}:{ip} is already being connected by another client socket.");
                        return;
                    }

                    TcpSk tcp = AddTcpSocket(true, tcpClient.Client, SocketStatus.Connecting, machineName, ip);

                    // Update the other server socket's machine name based on this corresponding client socket.
                    UpdateTcpSockets(tcp, SocketStatus.Connecting);

                    Logger.LogDebug(string.Format(CultureInfo.CurrentCulture, "=====> Connecting to: {0}:{1}", machineName, ip.ToString()));

                    long timeoutLeft;

                    do
                    {
                        try
                        {
                            tcpClient.Connect(ip, TcpPort + 1);
                        }
                        catch (ObjectDisposedException)
                        {
                            // When user reconnects.
                            Logger.LogDebug($"tcpClient.Connect: The socket has already been disposed: {machineName}:{ip}");
                            return;
                        }
                        catch (SocketException e)
                        {
                            timeoutLeft = connectTimeout - Common.GetTick();

                            if (timeoutLeft > 0)
                            {
                                Logger.LogDebug($"tcpClient.Connect: {timeoutLeft}: {e.Message}");
                                Thread.Sleep(1000);
                                continue;
                            }
                            else
                            {
                                Logger.Log($"tcpClient.Connect: Unable to connect after a timeout: {machineName}:{ip} : {e.Message}");

                                string message = $"Connection timed out: {machineName}:{ip}";

                                Common.ShowToolTip(message, 5000, ToolTipIcon.Warning, Setting.Values.ShowClipNetStatus);

                                UpdateTcpSockets(tcp, SocketStatus.Timeout);
                                return;
                            }
                        }

                        break;
                    }
                    while (true);

                    Logger.LogDebug($"=====> Connected: {tcpClient.Client.LocalEndPoint} => {machineName}: {ip}");

                    // Sending/Receiving packages
                    MainTCPRoutine(tcp, machineName, true);
                }
                catch (ObjectDisposedException e)
                {
                    Logger.Log($"{nameof(StartNewTcpClientThread)}: The socket could have been closed/disposed due to machine switch: {e.Message}");
                }
                catch (SocketException e)
                {
                    // DHCP error, etc.
                    string localIP = tcpClient?.Client?.LocalEndPoint?.ToString();

                    if (localIP != null && (localIP.StartsWith("169.254", StringComparison.InvariantCulture) || localIP.ToString().StartsWith("0.0", StringComparison.InvariantCulture)))
                    {
                        Common.ShowToolTip($"Error: The machine has limited connectivity on [{localIP}].", 5000, ToolTipIcon.Warning, Setting.Values.ShowClipNetStatus);
                    }
                    else
                    {
                        Logger.TelemetryLogTrace($"{nameof(StartNewTcpClientThread)}: Error: {e.Message} on the IP Address: {localIP}", SeverityLevel.Error);
                    }
                }
                catch (Exception e)
                {
                    Logger.Log(e);
                }
            }

            Thread t = new(NewTcpClient, "TCP Client Thread " + machineName + " " + ip.ToString());

            t.SetApartmentState(ApartmentState.STA);
            t.Start();
        }

        private void FlagReopenSocketIfNeeded(Exception e)
        {
            /* SCENARIO: MachineA has MM blocked by firewall but MachineA can connect to MachineB so the tool would work normally (MM is not blocked by firewall in MachineB).
            * 1. a connection from A to B is working. Mouse/Keyboard is connected to A.
            * 2. User moves Mouse to B and lock B by Ctrl+Alt+L.
            * 3. B closes all sockets before switches to logon desktop. The client socket in A gets reset by B (the only connection between A and B).
            * 4. B is now on the logon desktop and tries to connect to A, connection fails since it is block by firewall in A.
            * 5. When the client socket gets reset in A, it should retry to connect to B => this is the fix implemented by few lines of code below.
            * */

            // WSAECONNRESET
            if (e is ExpectedSocketException se && se.ShouldReconnect)
            {
                Common.PleaseReopenSocket = Common.REOPEN_WHEN_WSAECONNRESET;
                Logger.Log($"MainTCPRoutine: {nameof(FlagReopenSocketIfNeeded)}");
            }
        }

        private long lastRemoteMachineID;
        internal static readonly string[] Separator = new string[] { "\r\n" };
        internal static readonly char[] BlankSeparator = new char[] { ' ' };

        private void MainTCPRoutine(TcpSk tcp, string machineName, bool isClient)
        {
            int packageCount = 0;
            DATA d;
            string remoteMachine = string.Empty;
            string strIP = string.Empty;
            ID remoteID = ID.NONE;

            byte[] buf = RandomNumberGenerator.GetBytes(Common.PACKAGE_SIZE_EX);
            d = new DATA(buf);

            TcpSk currentTcp = tcp;
            Socket currentSocket = currentTcp.BackingSocket;

            if (currentSocket == null)
            {
                Logger.LogDebug($"{nameof(MainTCPRoutine)}: The socket could have been closed/disposed by other threads.");
                return;
            }

            try
            {
                currentSocket.SendBufferSize = Common.PACKAGE_SIZE * 10000;
                currentSocket.ReceiveBufferSize = Common.PACKAGE_SIZE * 10000;
                currentSocket.NoDelay = true; // This is very interesting to know:(
                currentSocket.SendTimeout = 500;
                d.MachineName = Common.MachineName;

                d.Type = PackageType.Handshake;

                for (int i = 0; i < 10; i++)
                {
                    _ = TcpSend(currentTcp, d);
                }

                d.Machine1 = ~d.Machine1;
                d.Machine2 = ~d.Machine2;
                d.Machine3 = ~d.Machine3;
                d.Machine4 = ~d.Machine4;

                UpdateTcpSockets(currentTcp, SocketStatus.Handshaking);

                strIP = Common.GetRemoteStringIP(currentSocket, true);
                remoteMachine = string.IsNullOrEmpty(machineName) ? GetMachineNameFromSocket(currentSocket) : machineName;

                Logger.LogDebug($"MainTCPRoutine: Remote machineName/IP = {remoteMachine}/{strIP}");
            }
            catch (ObjectDisposedException e)
            {
                Common.PleaseReopenSocket = Common.REOPEN_WHEN_WSAECONNRESET;
                UpdateTcpSockets(currentTcp, SocketStatus.ForceClosed);
                currentSocket.Close();
                Logger.Log($"{nameof(MainTCPRoutine)}: The socket could have been closed/disposed by other threads: {e.Message}");
            }
            catch (Exception e)
            {
                UpdateTcpSockets(currentTcp, SocketStatus.ForceClosed);
                FlagReopenSocketIfNeeded(e);
                currentSocket.Close();
                Logger.Log(e);
            }

            int errCount = 0;

            while (true)
            {
                try
                {
                    DATA package = TcpReceiveData(currentTcp, out int receivedCount);
                    remoteID = package.Src;

                    if (package.Type == PackageType.Error)
                    {
                        errCount++;

                        string log = $"{nameof(MainTCPRoutine)}.TcpReceive error, invalid package from {remoteMachine}: {receivedCount}";
                        Logger.Log(log);

                        if (receivedCount > 0)
                        {
                            Common.ShowToolTip($"Invalid package from {remoteMachine}. Ensure the security keys are the same in both machines.", 5000, ToolTipIcon.Warning, false);
                        }

                        if (errCount > 5)
                        {
                            Common.MMSleep(1);

                            UpdateTcpSockets(currentTcp, SocketStatus.Error);
                            currentSocket.Close();

                            /*
                             * Sometimes when the peer machine closes the connection, we do not actually get an exception.
                             * Socket status is still connected and a read on the socket stream returns 0 byte.
                             * In this case, we should give ONE try to reconnect.
                             */

                            if (Common.ReopenSocketDueToReadError)
                            {
                                Common.PleaseReopenSocket = Common.REOPEN_WHEN_WSAECONNRESET;
                                Common.ReopenSocketDueToReadError = false;
                            }

                            break;
                        }
                    }
                    else
                    {
                        errCount = 0;
                    }

                    if (package.Type == PackageType.Handshake)
                    {
                        // Common.Log("Got a Handshake signal!");
                        package.Type = PackageType.HandshakeAck;
                        package.Src = ID.NONE;
                        package.MachineName = Common.MachineName;

                        package.Machine1 = ~package.Machine1;
                        package.Machine2 = ~package.Machine2;
                        package.Machine3 = ~package.Machine3;
                        package.Machine4 = ~package.Machine4;

                        _ = TcpSend(currentTcp, package);
                    }
                    else
                    {
                        if (packageCount >= 0)
                        {
                            if (++packageCount >= 10)
                            {
                                // Common.ShowToolTip("Invalid Security Key from " + remoteMachine, 5000);
                                Logger.Log("More than 10 invalid packages received!");

                                package.Type = PackageType.Invalid;

                                for (int i = 0; i < 10; i++)
                                {
                                    _ = TcpSend(currentTcp, package);
                                }

                                Common.MMSleep(2);

                                UpdateTcpSockets(currentTcp, SocketStatus.InvalidKey);
                                currentSocket.Close();
                                break;
                            }
                            else if (package.Type == PackageType.HandshakeAck)
                            {
                                if (package.Machine1 == d.Machine1 && package.Machine2 == d.Machine2 &&
                                   package.Machine3 == d.Machine3 && package.Machine4 == d.Machine4)
                                {
                                    string claimedMachineName = package.MachineName;

                                    if (!remoteMachine.Equals(claimedMachineName, StringComparison.Ordinal))
                                    {
                                        Logger.LogDebug($"DNS.RemoteMachineName({remoteMachine}) <> Claimed.MachineName({claimedMachineName}), using the claimed machine name.");
                                        remoteMachine = claimedMachineName;
                                        currentTcp.MachineName = remoteMachine;
                                    }

                                    // Double check to avoid a redundant client socket.
                                    if (isClient && Common.IsConnectedByAClientSocketTo(remoteMachine))
                                    {
                                        Logger.LogDebug("=====> Duplicate connected client socket for: " + remoteMachine + ":" + strIP + " is being removed.");
                                        UpdateTcpSockets(currentTcp, SocketStatus.ForceClosed);
                                        currentSocket.Close();
                                        return;
                                    }

                                    if (remoteMachine.Equals(Common.MachineName, StringComparison.OrdinalIgnoreCase))
                                    {
                                        Logger.LogDebug("Connected to/from local socket: " + strIP + (isClient ? "-Client" : "-Server"));
                                        UpdateTcpSockets(currentTcp, SocketStatus.NA);
                                        Common.MMSleep(1);
                                        currentSocket.Close();
                                        return;
                                    }

                                    packageCount = -1; // Trusted
                                    InvalidKeyFound = false;
                                    currentTcp.MachineId = (uint)remoteID;
                                    currentTcp.Status = SocketStatus.Connected;
                                    UpdateTcpSockets(currentTcp, SocketStatus.Connected);
                                    Logger.LogDebug("))))))))))))))) Machine got trusted: " + remoteMachine + ":" + strIP + ", Is client: " + isClient);

                                    if (Math.Abs(Common.GetTick() - Common.LastReconnectByHotKeyTime) < 5000)
                                    {
                                        Common.ShowToolTip("Connected to " + remoteMachine, 1000, ToolTipIcon.Info, Setting.Values.ShowClipNetStatus);
                                    }

                                    Common.SendHeartBeat(initial: true);

                                    if (MachineStuff.MachinePool.TryFindMachineByName(remoteMachine, out MachineInf machineInfo))
                                    {
                                        Logger.LogDebug("Machine updated: " + remoteMachine + "/" + remoteID.ToString());

                                        if (machineInfo.Name.Equals(MachineStuff.DesMachineName, StringComparison.OrdinalIgnoreCase))
                                        {
                                            Logger.LogDebug("Des ID updated: " + Common.DesMachineID.ToString() +
                                                "/" + remoteID.ToString());
                                            MachineStuff.NewDesMachineID = Common.DesMachineID = remoteID;
                                        }

                                        _ = MachineStuff.MachinePool.TryUpdateMachineID(remoteMachine, remoteID, true);
                                        MachineStuff.UpdateMachinePoolStringSetting();
                                    }
                                    else
                                    {
                                        Logger.LogDebug("New machine connected: {0}.", remoteMachine);

                                        if (!Common.RunOnLogonDesktop && !Common.RunOnScrSaverDesktop)
                                        {
                                            Common.ShowToolTip("Connected to new machine " + remoteMachine, 1000, ToolTipIcon.Info, Setting.Values.ShowClipNetStatus);
                                        }
                                    }

                                    if (!isClient)
                                    {
                                        MachineStuff.UpdateClientSockets("MainTCPRoutine");
                                    }
                                }
                                else
                                {
                                    Logger.LogDebug("Invalid ACK from " + remoteMachine);
                                    UpdateTcpSockets(currentTcp, SocketStatus.InvalidKey);

                                    string remoteEP = currentSocket.RemoteEndPoint.ToString();

                                    if (FailedAttempt.AddOrUpdate(remoteEP, 1, (key, value) => value + 1) > 10)
                                    {
                                        _ = FailedAttempt.AddOrUpdate(remoteEP, 0, (key, value) => 0);

                                        _ = MessageBox.Show($"Too many connection attempts from [{remoteEP}]!\r\nRestart the app to retry.", Application.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Error);
                                        Common.MainForm.Quit(true, false);
                                    }

                                    currentSocket.Close();
                                    break;
                                }
                            }
                            else if (package.Type == PackageType.Mouse)
                            {
                                if (packageCount > 5)
                                {
                                    packageCount--;
                                }
                            }
                            else if (package.Type is PackageType.Heartbeat or PackageType.Heartbeat_ex)
                            {
                                if (packageCount > 5)
                                {
                                    packageCount--;
                                }
                            }
                            else
                            {
                                if (packageCount > 5)
                                {
                                    UpdateTcpSockets(currentTcp, SocketStatus.InvalidKey);
                                }
                                else
                                {
                                    Logger.Log(string.Format(
                                        CultureInfo.CurrentCulture,
                                        "Unexpected package, size = {0}, type = {1}",
                                        receivedCount,
                                        package.Type));
                                }
                            }
                        }
                        else if (receivedCount > 0)
                        {
                            // Add some log when remote machine switches.
                            if (lastRemoteMachineID != (long)remoteID)
                            {
                                _ = Interlocked.Exchange(ref lastRemoteMachineID, (long)remoteID);
                                Logger.LogDebug($"MainTCPRoutine: Remote machine = {strIP}/{lastRemoteMachineID}");
                            }

                            if (package.Type == PackageType.HandshakeAck)
                            {
                                Logger.LogDebug("Skipping the rest of the Handshake packages.");
                            }
                            else
                            {
                                Receiver.ProcessPackage(package, currentTcp);
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    UpdateTcpSockets(currentTcp, SocketStatus.Error);
                    FlagReopenSocketIfNeeded(e);
                    currentSocket.Close();
                    Logger.Log(e);
                    break;
                }
            }

            if (remoteID != ID.NONE)
            {
                _ = MachineStuff.RemoveDeadMachines(remoteID);
            }
        }

        private static void AcceptConnectionAndSendClipboardData(object param)
        {
            // SuppressFlow fixes an issue on service mode, where the helper process can't get enough permissions to be started again.
            // More details can be found on: https://github.com/microsoft/PowerToys/pull/36892
            using var asyncFlowControl = ExecutionContext.SuppressFlow();

            TcpListener server = param as TcpListener;

            do
            {
                Logger.LogDebug("SendClipboardData: Waiting for request...");
                Socket s = null;

                try
                {
                    s = server.AcceptSocket();
                }
                catch (InvalidOperationException e)
                {
                    Logger.Log($"The clipboard socket could have been closed. {e.Message}");
                    break;
                }
                catch (SocketException e)
                {
                    if (e.ErrorCode == (int)SocketError.Interrupted)
                    {
                        Logger.Log("server.AcceptSocket: A blocking socket call was canceled.");
                        continue;
                    }
                    else
                    {
                        Logger.Log(e);
                        break;
                    }
                }
                catch (Exception e)
                {
                    Logger.Log(e);
                    break;
                }

                if (s != null)
                {
                    try
                    {
                        new Task(() =>
                        {
                            // SuppressFlow fixes an issue on service mode, where the helper process can't get enough permissions to be started again.
                            // More details can be found on: https://github.com/microsoft/PowerToys/pull/36892
                            using var asyncFlowControl = ExecutionContext.SuppressFlow();

                            System.Threading.Thread thread = Thread.CurrentThread;
                            thread.Name = $"{nameof(SendOrReceiveClipboardData)}.{thread.ManagedThreadId}";
                            Thread.UpdateThreads(thread);
                            SendOrReceiveClipboardData(s);
                        }).Start();
                    }
                    catch (Exception e)
                    {
                        Logger.Log(e);
                    }
                }
            }
            while (true);
        }

        internal static void SendOrReceiveClipboardData(Socket s)
        {
            try
            {
                string remoteEndPoint = s.RemoteEndPoint.ToString();
                Logger.LogDebug("SendClipboardData: Request accepted: " + s.LocalEndPoint.ToString() + "/" + remoteEndPoint);
                DragDrop.IsDropping = false;
                DragDrop.IsDragging = false;
                DragDrop.DragMachine = (ID)1;

                bool clientPushData = true;
                ClipboardPostAction postAction = ClipboardPostAction.Other;
                bool handShaken = Common.ShakeHand(ref remoteEndPoint, s, out Stream enStream, out Stream deStream, ref clientPushData, ref postAction);

                if (!handShaken)
                {
                    s.Close();
                    return;
                }
                else
                {
                    Logger.LogDebug($"{nameof(SendOrReceiveClipboardData)}: Clipboard connection accepted: " + remoteEndPoint);
                    Common.SetToggleIcon(new int[Common.TOGGLE_ICONS_SIZE] { Common.ICON_SMALL_CLIPBOARD, -1, -1, -1 });
                }

                if (clientPushData)
                {
                    Common.ReceiveAndProcessClipboardData(remoteEndPoint, s, enStream, deStream, $"{postAction}");
                }
                else
                {
                    SendClipboardData(s, enStream);
                }
            }
            catch (Exception e)
            {
                Logger.Log(e);
            }
        }

        internal static void SendClipboardData(Socket s, Stream ecStream)
        {
            if (Common.RunWithNoAdminRight && Setting.Values.OneWayClipboardMode)
            {
                s?.Close();
                return;
            }

            const int CLOSE_TIMEOUT = 10;
            byte[] header = new byte[1024];
            string headerString = string.Empty;
            if (Common.LastDragDropFile != null)
            {
                string fileName = null;

                if (!Common.ImpersonateLoggedOnUserAndDoSomething(() =>
                {
                    if (!File.Exists(Common.LastDragDropFile))
                    {
                        headerString = Directory.Exists(Common.LastDragDropFile)
                            ? $"{0}*{Common.LastDragDropFile} - Folder is not supported, zip it first!"
                            : Common.LastDragDropFile.Contains("- File too big")
                                ? $"{0}*{Common.LastDragDropFile}"
                                : $"{0}*{Common.LastDragDropFile} not found!";
                    }
                    else
                    {
                        fileName = Common.LastDragDropFile;
                        headerString = $"{new FileInfo(fileName).Length}*{fileName}";
                    }
                }))
                {
                    s?.Close();
                    return;
                }

                Common.GetBytesU(headerString).CopyTo(header, 0);

                try
                {
                    ecStream.Write(header, 0, header.Length);

                    if (!string.IsNullOrEmpty(fileName))
                    {
                        if (SendFile(s, ecStream, fileName))
                        {
                            s.Close(CLOSE_TIMEOUT);
                        }
                    }
                    else
                    {
                        s.Close(CLOSE_TIMEOUT);
                    }
                }
                catch (IOException e)
                {
                    string log = $"{nameof(SendClipboardData)}: Exception accessing the socket: {e.InnerException?.GetType()}/{e.Message}. (This is expected when the remote machine closes the connection during desktop switch or reconnection.)";
                    Logger.Log(log);
                }
                catch (SocketException e)
                {
                    string log = $"{nameof(SendClipboardData)}: {e.GetType()}/{e.Message}. This is expected when the connection is closed by the remote host.";
                    Logger.Log(log);
                }
                catch (ObjectDisposedException e)
                {
                    string log = $"{nameof(SendClipboardData)}: {e.GetType()}/{e.Message}. This is expected when the socket is disposed by a machine switch for ex..";
                    Logger.Log(log);
                }
            }
            else if (!Common.IsClipboardDataImage && Common.LastClipboardData != null)
            {
                try
                {
                    byte[] data = Common.LastClipboardData;

                    headerString = $"{data.Length}*{"text"}";
                    Common.GetBytesU(headerString).CopyTo(header, 0);

                    if (data != null)
                    {
                        ecStream.Write(header, 0, header.Length);
                        _ = SendData(s, ecStream, data);
                        Logger.LogDebug("Text sent: " + data.Length.ToString(CultureInfo.CurrentCulture));
                    }

                    s.Close(CLOSE_TIMEOUT);
                }
                catch (IOException e)
                {
                    string log = $"{nameof(SendClipboardData)}: Exception accessing the socket: {e.InnerException?.GetType()}/{e.Message}. (This is expected when the remote machine closes the connection during desktop switch or reconnection.)";
                    Logger.Log(log);
                }
                catch (SocketException e)
                {
                    string log = $"{nameof(SendClipboardData)}: {e.GetType()}/{e.Message}. This is expected when the connection is closed by the remote host.";
                    Logger.Log(log);
                }
                catch (ObjectDisposedException e)
                {
                    string log = $"{nameof(SendClipboardData)}: {e.GetType()}/{e.Message}. This is expected when the socket is disposed by a machine switch for ex..";
                    Logger.Log(log);
                }
            }
            else if (Common.LastClipboardData != null && Common.LastClipboardData.Length > 0)
            {
                byte[] data = Common.LastClipboardData;

                headerString = $"{data.Length}*{"image"}";
                Common.GetBytesU(headerString).CopyTo(header, 0);
                try
                {
                    ecStream.Write(header, 0, header.Length);
                    _ = SendData(s, ecStream, data);
                    Logger.LogDebug("Image sent: " + data.Length.ToString(CultureInfo.CurrentCulture));
                    s.Close(CLOSE_TIMEOUT);
                }
                catch (IOException e)
                {
                    string log = $"{nameof(SendClipboardData)}: Exception accessing the socket: {e.InnerException?.GetType()}/{e.Message}. (This is expected when the remote machine closes the connection during desktop switch or reconnection.)";
                    Logger.Log(log);
                }
                catch (SocketException e)
                {
                    string log = $"{nameof(SendClipboardData)}: {e.GetType()}/{e.Message}. This is expected when the connection is closed by the remote host.";
                    Logger.Log(log);
                }
                catch (ObjectDisposedException e)
                {
                    string log = $"{nameof(SendClipboardData)}: {e.GetType()}/{e.Message}. This is expected when the socket is disposed by a machine switch for ex..";
                    Logger.Log(log);
                }
            }
            else
            {
                Logger.Log("No data available in clipboard or LastDragDropFile!");
                s.Close();
            }
        }

        private static bool SendFileEx(Socket s, Stream ecStream, string fileName)
        {
            try
            {
                using (FileStream f = File.OpenRead(fileName))
                {
                    byte[] buf = new byte[Common.NETWORK_STREAM_BUF_SIZE];
                    int rv, sentCount = 0;

                    do
                    {
                        if ((rv = f.Read(buf, 0, Common.NETWORK_STREAM_BUF_SIZE)) > 0)
                        {
                            ecStream.Write(buf, 0, rv);
                            sentCount += rv;
                        }
                    }
                    while (rv > 0);

                    if ((rv = Common.PACKAGE_SIZE - (sentCount % Common.PACKAGE_SIZE)) > 0)
                    {
                        Array.Clear(buf, 0, buf.Length);
                        ecStream.Write(buf, 0, rv);
                    }

                    ecStream.Flush();

                    Logger.LogDebug("File sent: " + fileName);
                }

                return true;
            }
            catch (Exception e)
            {
                if (e is IOException)
                {
                    string log = $"{nameof(SendFileEx)}: Exception accessing the socket: {e.InnerException?.GetType()}/{e.Message}. (This is expected when the remote machine closes the connection during desktop switch or reconnection.)";
                    Logger.Log(log);
                }
                else
                {
                    Logger.Log(e);
                }

                Common.ShowToolTip(e.Message, 1000, ToolTipIcon.Warning, Setting.Values.ShowClipNetStatus);
                s.Close();
            }

            return false;
        }

        private static bool SendFile(Socket s, Stream ecStream, string fileName)
        {
            bool r = false;

            if (Common.RunOnLogonDesktop || Common.RunOnScrSaverDesktop)
            {
                if (fileName.StartsWith(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles) + @"\" + Common.BinaryName + @"\ScreenCaptures\", StringComparison.CurrentCultureIgnoreCase))
                {
                    r = SendFileEx(s, ecStream, fileName);
                }
            }
            else
            {
                _ = Common.ImpersonateLoggedOnUserAndDoSomething(() => { r = SendFileEx(s, ecStream, fileName); });
            }

            return r;
        }

        private static bool SendData(Socket s, Stream ecStream, byte[] data)
        {
            bool r = false;

            try
            {
                using MemoryStream f = new(data);
                byte[] buf = new byte[Common.NETWORK_STREAM_BUF_SIZE];
                int rv, sentCount = 0;

                do
                {
                    if ((rv = f.Read(buf, 0, Common.NETWORK_STREAM_BUF_SIZE)) > 0)
                    {
                        ecStream.Write(buf, 0, rv);
                        sentCount += rv;
                    }
                }
                while (rv > 0);

                if ((rv = sentCount % Common.PACKAGE_SIZE) > 0)
                {
                    Array.Clear(buf, 0, buf.Length);
                    ecStream.Write(buf, 0, rv);
                }

                ecStream.Flush();
                Logger.LogDebug("Data sent: " + data.Length.ToString(CultureInfo.InvariantCulture));
                r = true;
            }
            catch (Exception e)
            {
                if (e is IOException)
                {
                    string log = $"{nameof(SendData)}: Exception accessing the socket: {e.InnerException?.GetType()}/{e.Message}. (This is expected when the remote machine closes the connection during desktop switch or reconnection.)";
                    Logger.Log(log);
                }
                else
                {
                    Logger.Log(e);
                }

                Common.ShowToolTip(e.Message, 1000, ToolTipIcon.Warning, Setting.Values.ShowClipNetStatus);
                ecStream.Close();
                s.Close();
            }

            return r;
        }

        private TcpSk AddTcpSocket(bool isClient, Socket s, SocketStatus status, string machineName)
        {
            Common.CloseAnUnusedSocket();
            TcpSk tcp = new(isClient, s, status, machineName);

            lock (TcpSocketsLock)
            {
#if ENABLE_LEGACY_DOS_PROTECTION
                PreventDoS(TcpSockets);
#endif
                TcpSockets.Add(tcp);
            }

            return tcp;
        }

        private TcpSk AddTcpSocket(bool isClient, Socket s, SocketStatus status, string machineName, IPAddress ip)
        {
            Common.CloseAnUnusedSocket();
            TcpSk tcp = new(isClient, s, status, machineName, ip);

            lock (TcpSocketsLock)
            {
#if ENABLE_LEGACY_DOS_PROTECTION
                PreventDoS(TcpSockets);
#endif
                TcpSockets.Add(tcp);
            }

            return tcp;
        }

        private void UpdateTcpSockets(TcpSk tcp, SocketStatus status)
        {
            if (status == SocketStatus.InvalidKey)
            {
                InvalidKeyFound = true;
            }

            InvalidKeyFoundOnClientSocket = tcp.IsClient && InvalidKeyFound;

            try
            {
                lock (TcpSocketsLock)
                {
                    if (TcpSockets != null)
                    {
                        if (status == SocketStatus.Connected && tcp.IsClient)
                        {
                            List<TcpSk> tobeRemovedSockets = TcpSockets;

                            if (tcp.MachineId == Setting.Values.MachineId)
                            {
                                tcp = null;
                                Setting.Values.MachineId = Common.Ran.Next();
                                Common.UpdateMachineTimeAndID();
                                Common.PleaseReopenSocket = Common.REOPEN_WHEN_HOTKEY;

                                Logger.TelemetryLogTrace("MachineID conflict.", SeverityLevel.Information);
                            }
                            else
                            {
                                // Keep the first connected one.
                                tobeRemovedSockets = TcpSockets.Where(item => item.IsClient && !ReferenceEquals(item, tcp) && item.MachineName.Equals(tcp.MachineName, StringComparison.OrdinalIgnoreCase)).ToList();
                            }

                            foreach (TcpSk t in tobeRemovedSockets)
                            {
                                t.Status = SocketStatus.ForceClosed;
                                Logger.LogDebug($"Closing duplicated socket {t.MachineName}: {t.Address}");
                            }
                        }

                        List<TcpSk> toBeRemoved = new();

                        foreach (TcpSk t in TcpSockets)
                        {
                            if (t.Status is SocketStatus.Error or
                                SocketStatus.ForceClosed or

                                // SocketStatus.InvalidKey or
                                SocketStatus.NA or
                                SocketStatus.Timeout or
                                SocketStatus.SendError)
                            {
                                try
                                {
                                    if (t.BackingSocket != null)
                                    {
                                        t.MachineName = "$*NotUsed*$";
                                        t.Status = t.Status >= 0 ? 0 : t.Status - 1; // If error closing, the socket will be closed again at SocketStuff.Close().
                                        t.BackingSocket.Close();
                                    }

                                    toBeRemoved.Add(t);
                                }
                                catch (SocketException e)
                                {
                                    string log = $"{nameof(UpdateTcpSockets)}: {e.GetType()}/{e.Message}. This is expected when the connection is closed by the remote host.";
                                    Logger.Log(log);
                                }
                                catch (ObjectDisposedException e)
                                {
                                    string log = $"{nameof(UpdateTcpSockets)}: {e.GetType()}/{e.Message}. This is expected when the socket is disposed by a machine switch for ex..";
                                    Logger.Log(log);
                                }
                            }
                        }

                        if (tcp != null)
                        {
                            tcp.Status = status;

                            if (status == SocketStatus.Connected)
                            {
                                // Update the socket's machine name based on its corresponding client/server socket.
                                foreach (TcpSk t in TcpSockets)
                                {
                                    if (t.MachineId == tcp.MachineId && t.IsClient != tcp.IsClient)
                                    {
                                        if ((string.IsNullOrEmpty(tcp.MachineName) || tcp.MachineName.Contains('.') || tcp.MachineName.Contains(':'))
                                            && !(string.IsNullOrEmpty(t.MachineName) || t.MachineName.Contains('.') || t.MachineName.Contains(':')))
                                        {
                                            tcp.MachineName = t.MachineName;
                                        }
                                        else if ((string.IsNullOrEmpty(t.MachineName) || t.MachineName.Contains('.') || t.MachineName.Contains(':'))
                                            && !(string.IsNullOrEmpty(tcp.MachineName) || tcp.MachineName.Contains('.') || tcp.MachineName.Contains(':')))
                                        {
                                            t.MachineName = tcp.MachineName;
                                        }
                                    }
                                }

                                if (string.IsNullOrEmpty(tcp.MachineName) || tcp.MachineName.Contains('.') || tcp.MachineName.Contains(':'))
                                {
                                    tcp.MachineName = MachineStuff.NameFromID((ID)tcp.MachineId);
                                }

                                if (string.IsNullOrEmpty(tcp.MachineName) || tcp.MachineName.Contains('.') || tcp.MachineName.Contains(':'))
                                {
                                    tcp.MachineName = Common.GetRemoteStringIP(tcp.BackingSocket);
                                }
                            }
                        }
                        else
                        {
                            Logger.Log("UpdateTcpSockets.Exception: Socket not found!");
                        }

                        foreach (TcpSk t in toBeRemoved)
                        {
                            _ = TcpSockets.Remove(t);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Log(e);
            }
        }

        private void PreventDoS(List<TcpSk> sockets)
        {
            if (sockets.Count > 100)
            {
                TcpSk tcp;

                try
                {
                    string msg = Application.ProductName + " has been terminated, too many connections.";

                    for (int i = 0; i < 10; i++)
                    {
                        tcp = sockets[i * 10];

                        if (tcp != null)
                        {
                            msg += $"\r\n{Common.MachineName}{(tcp.IsClient ? "=>" : "<=")}{tcp.MachineName}:{tcp.Status}";
                        }
                    }

                    _ = Common.CreateLowIntegrityProcess(
                        "\"" + Path.GetDirectoryName(Application.ExecutablePath) + "\\MouseWithoutBordersHelper.exe\"",
                        "InternalError" + " \"" + msg + "\"",
                        0,
                        false,
                        0,
                        (short)ProcessWindowStyle.Hidden);
                }
                finally
                {
                    Common.MainForm.Quit(true, false);
                }
            }
        }
    }
}
