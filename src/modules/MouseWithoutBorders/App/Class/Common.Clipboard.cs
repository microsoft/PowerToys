// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

using Microsoft.PowerToys.Telemetry;

// <summary>
//     Clipboard related routines.
// </summary>
// <history>
//     2008 created by Truong Do (ductdo).
//     2009-... modified by Truong Do (TruongDo).
//     2023- Included in PowerToys.
// </history>
using MouseWithoutBorders.Class;
using MouseWithoutBorders.Core;
using MouseWithoutBorders.Exceptions;

using SystemClipboard = System.Windows.Forms.Clipboard;
using Thread = MouseWithoutBorders.Core.Thread;

namespace MouseWithoutBorders
{
    internal partial class Common
    {
        internal static readonly char[] Comma = new char[] { ',' };
        internal static readonly char[] Star = new char[] { '*' };
        internal static readonly char[] NullSeparator = new char[] { '\0' };

        internal const uint BIG_CLIPBOARD_DATA_TIMEOUT = 30000;
        private const uint MAX_CLIPBOARD_DATA_SIZE_CAN_BE_SENT_INSTANTLY_TCP = 1024 * 1024; // 1MB
        private const uint MAX_CLIPBOARD_FILE_SIZE_CAN_BE_SENT = 100 * 1024 * 1024; // 100MB
        private const int TEXT_HEADER_SIZE = 12;
        private const int DATA_SIZE = 48;
        private const string TEXT_TYPE_SEP = "{4CFF57F7-BEDD-43d5-AE8F-27A61E886F2F}";
        private static long lastClipboardEventTime;
        private static string lastMachineWithClipboardData;
        private static string lastDragDropFile;
#pragma warning disable SA1307 // Accessible fields should begin with upper-case letter
        internal static long clipboardCopiedTime;
#pragma warning restore SA1307

        internal static ID LastIDWithClipboardData { get; set; }

        internal static string LastDragDropFile
        {
            get => Common.lastDragDropFile;
            set => Common.lastDragDropFile = value;
        }

        internal static string LastMachineWithClipboardData
        {
            get => Common.lastMachineWithClipboardData;
            set => Common.lastMachineWithClipboardData = value;
        }

        internal static long LastClipboardEventTime
        {
            get => Common.lastClipboardEventTime;
            set => Common.lastClipboardEventTime = value;
        }

        internal static IntPtr NextClipboardViewer { get; set; }

        internal static bool IsClipboardDataImage { get; private set; }

        internal static byte[] LastClipboardData { get; private set; }

        private static object lastClipboardObject = string.Empty;

        internal static bool HasSwitchedMachineSinceLastCopy { get; set; }

        internal static bool CheckClipboardEx(ByteArrayOrString data, bool isFilePath)
        {
            Logger.LogDebug($"{nameof(CheckClipboardEx)}: ShareClipboard = {Setting.Values.ShareClipboard}, TransferFile = {Setting.Values.TransferFile}, data = {data}.");
            Logger.LogDebug($"{nameof(CheckClipboardEx)}: {nameof(Setting.Values.OneWayClipboardMode)} = {Setting.Values.OneWayClipboardMode}.");

            if (!Setting.Values.ShareClipboard)
            {
                return false;
            }

            if (Common.RunWithNoAdminRight && Setting.Values.OneWayClipboardMode)
            {
                return false;
            }

            if (GetTick() - LastClipboardEventTime < 1000)
            {
                Logger.LogDebug("GetTick() - lastClipboardEventTime < 1000");
                LastClipboardEventTime = GetTick();
                return false;
            }

            LastClipboardEventTime = GetTick();

            try
            {
                IsClipboardDataImage = false;
                LastClipboardData = null;
                LastDragDropFile = null;
                GC.Collect();

                string stringData = null;
                byte[] byteData = null;

                if (data.IsByteArray)
                {
                    byteData = data.GetByteArray();
                }
                else
                {
                    stringData = data.GetString();
                }

                if (stringData != null)
                {
                    if (!HasSwitchedMachineSinceLastCopy)
                    {
                        if (lastClipboardObject is string lastStringData && lastStringData.Equals(stringData, StringComparison.OrdinalIgnoreCase))
                        {
                            Logger.LogDebug("CheckClipboardEx: Same string data.");
                            return false;
                        }
                    }

                    HasSwitchedMachineSinceLastCopy = false;

                    if (isFilePath)
                    {
                        Logger.LogDebug("Clipboard contains FileDropList");

                        if (!Setting.Values.TransferFile)
                        {
                            Logger.LogDebug("TransferFile option is unchecked.");
                            return false;
                        }

                        string filePath = stringData;

                        _ = Launch.ImpersonateLoggedOnUserAndDoSomething(() =>
                        {
                            if (File.Exists(filePath) || Directory.Exists(filePath))
                            {
                                if (File.Exists(filePath) && new FileInfo(filePath).Length <= MAX_CLIPBOARD_FILE_SIZE_CAN_BE_SENT)
                                {
                                    Logger.LogDebug("Clipboard contains: " + filePath);
                                    LastDragDropFile = filePath;
                                    SendClipboardBeat();
                                    SetToggleIcon(new int[TOGGLE_ICONS_SIZE] { ICON_BIG_CLIPBOARD, -1, ICON_BIG_CLIPBOARD, -1 });
                                }
                                else
                                {
                                    if (Directory.Exists(filePath))
                                    {
                                        Logger.LogDebug("Clipboard contains a directory: " + filePath);
                                        LastDragDropFile = filePath;
                                        SendClipboardBeat();
                                    }
                                    else
                                    {
                                        LastDragDropFile = filePath + " - File too big (greater than 100MB), please drag and drop the file instead!";
                                        SendClipboardBeat();
                                        Logger.Log("Clipboard: File too big: " + filePath);
                                    }

                                    SetToggleIcon(new int[TOGGLE_ICONS_SIZE] { ICON_ERROR, -1, ICON_ERROR, -1 });
                                }
                            }
                            else
                            {
                                Logger.Log("CheckClipboardEx: File not found: " + filePath);
                            }
                        });
                    }
                    else
                    {
                        byte[] texts = Common.GetBytesU(stringData);

                        using MemoryStream ms = new();
                        using (DeflateStream s = new(ms, CompressionMode.Compress, true))
                        {
                            s.Write(texts, 0, texts.Length);
                        }

                        Logger.LogDebug("Plain/Zip = " + texts.Length.ToString(CultureInfo.CurrentCulture) + "/" +
                            ms.Length.ToString(CultureInfo.CurrentCulture));

                        LastClipboardData = ms.GetBuffer();
                    }
                }
                else if (byteData != null)
                {
                    if (!HasSwitchedMachineSinceLastCopy)
                    {
                        if (lastClipboardObject is byte[] lastByteData && Enumerable.SequenceEqual(lastByteData, byteData))
                        {
                            Logger.LogDebug("CheckClipboardEx: Same byte[] data.");
                            return false;
                        }
                    }

                    HasSwitchedMachineSinceLastCopy = false;

                    Logger.LogDebug("Clipboard contains image");
                    IsClipboardDataImage = true;
                    LastClipboardData = byteData;
                }
                else
                {
                    Logger.LogDebug("*** Clipboard contains something else!");
                    return false;
                }

                lastClipboardObject = data;

                if (LastClipboardData != null && LastClipboardData.Length > 0)
                {
                    if (LastClipboardData.Length > MAX_CLIPBOARD_DATA_SIZE_CAN_BE_SENT_INSTANTLY_TCP)
                    {
                        SendClipboardBeat();
                        SetToggleIcon(new int[TOGGLE_ICONS_SIZE] { ICON_BIG_CLIPBOARD, -1, ICON_BIG_CLIPBOARD, -1 });
                    }
                    else
                    {
                        SetToggleIcon(new int[TOGGLE_ICONS_SIZE] { ICON_SMALL_CLIPBOARD, -1, -1, -1 });
                        SendClipboardDataUsingTCP(LastClipboardData, IsClipboardDataImage);
                    }

                    return true;
                }
            }
            catch (Exception e)
            {
                Logger.Log(e);
            }

            return false;
        }

        private static void SendClipboardDataUsingTCP(byte[] bytes, bool image)
        {
            if (Sk == null)
            {
                return;
            }

            new Task(() =>
            {
                // SuppressFlow fixes an issue on service mode, where the helper process can't get enough permissions to be started again.
                // More details can be found on: https://github.com/microsoft/PowerToys/pull/36892
                using var asyncFlowControl = ExecutionContext.SuppressFlow();

                System.Threading.Thread thread = Thread.CurrentThread;
                thread.Name = $"{nameof(SendClipboardDataUsingTCP)}.{thread.ManagedThreadId}";
                Thread.UpdateThreads(thread);
                int l = bytes.Length;
                int index = 0;
                int len;
                DATA package = new();
                byte[] buf = new byte[PACKAGE_SIZE_EX];
                int dataStart = PACKAGE_SIZE_EX - DATA_SIZE;

                while (true)
                {
                    if ((index + DATA_SIZE) > l)
                    {
                        len = l - index;
                        Array.Clear(buf, 0, PACKAGE_SIZE_EX);
                    }
                    else
                    {
                        len = DATA_SIZE;
                    }

                    Array.Copy(bytes, index, buf, dataStart, len);
                    package.Bytes = buf;

                    package.Type = image ? PackageType.ClipboardImage : PackageType.ClipboardText;
                    package.Des = ID.ALL;
                    SkSend(package, (uint)MachineID, false);

                    index += DATA_SIZE;
                    if (index >= l)
                    {
                        break;
                    }
                }

                package.Type = PackageType.ClipboardDataEnd;
                package.Des = ID.ALL;
                SkSend(package, (uint)MachineID, false);
            }).Start();
        }

        internal static void ReceiveClipboardDataUsingTCP(DATA data, bool image, TcpSk tcp)
        {
            try
            {
                if (Sk == null || RunOnLogonDesktop || RunOnScrSaverDesktop)
                {
                    return;
                }

                MemoryStream m = new();
                int dataStart = PACKAGE_SIZE_EX - DATA_SIZE;
                m.Write(data.Bytes, dataStart, DATA_SIZE);
                int unexpectedCount = 0;

                bool done = false;
                do
                {
                    data = SocketStuff.TcpReceiveData(tcp, out int err);

                    switch (data.Type)
                    {
                        case PackageType.ClipboardImage:
                        case PackageType.ClipboardText:
                            m.Write(data.Bytes, dataStart, DATA_SIZE);
                            break;

                        case PackageType.ClipboardDataEnd:
                            done = true;
                            break;

                        default:
                            Receiver.ProcessPackage(data, tcp);
                            if (++unexpectedCount > 100)
                            {
                                Logger.Log("ReceiveClipboardDataUsingTCP: unexpectedCount > 100!");
                                done = true;
                            }

                            break;
                    }
                }
                while (!done);

                LastClipboardEventTime = GetTick();

                if (image)
                {
                    Image im = Image.FromStream(m);
                    Clipboard.SetImage(im);
                    LastClipboardEventTime = GetTick();
                }
                else
                {
                    Common.SetClipboardData(m.GetBuffer());
                    LastClipboardEventTime = GetTick();
                }

                m.Dispose();

                SetToggleIcon(new int[TOGGLE_ICONS_SIZE] { ICON_SMALL_CLIPBOARD, -1, ICON_SMALL_CLIPBOARD, -1 });
            }
            catch (Exception e)
            {
                Logger.Log("ReceiveClipboardDataUsingTCP: " + e.Message);
            }
        }

        private static readonly Lock ClipboardThreadOldLock = new();
        private static System.Threading.Thread clipboardThreadOld;

        internal static void GetRemoteClipboard(string postAction)
        {
            if (!RunOnLogonDesktop && !RunOnScrSaverDesktop)
            {
                if (Common.LastMachineWithClipboardData == null ||
                    Common.LastMachineWithClipboardData.Length < 1)
                {
                    return;
                }

                new Task(() =>
                {
                    // SuppressFlow fixes an issue on service mode, where the helper process can't get enough permissions to be started again.
                    // More details can be found on: https://github.com/microsoft/PowerToys/pull/36892
                    using var asyncFlowControl = ExecutionContext.SuppressFlow();

                    System.Threading.Thread thread = Thread.CurrentThread;
                    thread.Name = $"{nameof(ConnectAndGetData)}.{thread.ManagedThreadId}";
                    Thread.UpdateThreads(thread);
                    ConnectAndGetData(postAction);
                }).Start();
            }
        }

        private static Stream m;

        private static void ConnectAndGetData(object postAction)
        {
            if (Sk == null)
            {
                Logger.Log("ConnectAndGetData: Sk == null!");
                return;
            }

            string remoteMachine;
            TcpClient clipboardTcpClient = null;
            string postAct = (string)postAction;

            Logger.LogDebug("ConnectAndGetData.postAction: " + postAct);

            ClipboardPostAction clipboardPostAct = postAct.Contains("mspaint,") ? ClipboardPostAction.Mspaint
                : postAct.Equals("desktop", StringComparison.OrdinalIgnoreCase) ? ClipboardPostAction.Desktop
                : ClipboardPostAction.Other;

            try
            {
                remoteMachine = postAct.Contains("mspaint,") ? postAct.Split(Comma)[1] : Common.LastMachineWithClipboardData;

                remoteMachine = remoteMachine.Trim();

                if (!IsConnectedByAClientSocketTo(remoteMachine))
                {
                    Logger.Log($"No potential inbound connection from {MachineName} to {remoteMachine}, ask for a push back instead.");
                    ID machineId = MachineStuff.MachinePool.ResolveID(remoteMachine);

                    if (machineId != ID.NONE)
                    {
                        SkSend(
                            new DATA()
                            {
                                Type = PackageType.ClipboardAsk,
                                Des = machineId,
                                MachineName = MachineName,
                                PostAction = clipboardPostAct,
                            },
                            null,
                            false);
                    }
                    else
                    {
                        Logger.Log($"Unable to resolve {remoteMachine} to its long IP.");
                    }

                    return;
                }

                ShowToolTip("Connecting to " + remoteMachine, 2000, ToolTipIcon.Info, Setting.Values.ShowClipNetStatus);

                clipboardTcpClient = ConnectToRemoteClipboardSocket(remoteMachine);
            }
            catch (ThreadAbortException)
            {
                Logger.Log("The current thread is being aborted (1).");
                if (clipboardTcpClient != null && clipboardTcpClient.Connected)
                {
                    clipboardTcpClient.Client.Close();
                }

                return;
            }
            catch (Exception e)
            {
                Logger.Log(e);
                Common.SetToggleIcon(new int[Common.TOGGLE_ICONS_SIZE]
                {
                    Common.ICON_BIG_CLIPBOARD,
                    -1, Common.ICON_BIG_CLIPBOARD, -1,
                });
                ShowToolTip(e.Message, 1000, ToolTipIcon.Warning, Setting.Values.ShowClipNetStatus);
                return;
            }

            bool clientPushData = false;

            if (!ShakeHand(ref remoteMachine, clipboardTcpClient.Client, out Stream enStream, out Stream deStream, ref clientPushData, ref clipboardPostAct))
            {
                return;
            }

            ReceiveAndProcessClipboardData(remoteMachine, clipboardTcpClient.Client, enStream, deStream, postAct);
        }

        internal static void ReceiveAndProcessClipboardData(string remoteMachine, Socket s, Stream enStream, Stream deStream, string postAct)
        {
            lock (ClipboardThreadOldLock)
            {
                // Do not enable two connections at the same time.
                if (clipboardThreadOld != null && clipboardThreadOld.ThreadState != System.Threading.ThreadState.AbortRequested
                    && clipboardThreadOld.ThreadState != System.Threading.ThreadState.Aborted && clipboardThreadOld.IsAlive
                    && clipboardThreadOld.ManagedThreadId != Thread.CurrentThread.ManagedThreadId)
                {
                    if (clipboardThreadOld.Join(3000))
                    {
                        if (m != null)
                        {
                            m.Flush();
                            m.Close();
                            m = null;
                        }
                    }
                }

                clipboardThreadOld = Thread.CurrentThread;
            }

            try
            {
                byte[] header = new byte[1024];
                byte[] buf = new byte[NETWORK_STREAM_BUF_SIZE];
                string fileName = null;
                string tempFile = "data", savingFolder = string.Empty;
                Common.ToggleIconsIndex = 0;
                int rv;
                long receivedCount = 0;

                if ((rv = deStream.ReadEx(header, 0, header.Length)) < header.Length)
                {
                    Logger.Log("Reading header failed: " + rv.ToString(CultureInfo.CurrentCulture));
                    Common.SetToggleIcon(new int[Common.TOGGLE_ICONS_SIZE]
                    {
                        Common.ICON_BIG_CLIPBOARD,
                        -1, -1, -1,
                    });
                    return;
                }

                fileName = Common.GetStringU(header).Replace("\0", string.Empty);
                Logger.LogDebug("Header: " + fileName);
                string[] headers = fileName.Split(Star);

                if (headers.Length < 2 || !long.TryParse(headers[0], out long dataSize))
                {
                    Logger.Log(string.Format(
                        CultureInfo.CurrentCulture,
                        "Reading header failed: {0}:{1}",
                        headers.Length,
                        fileName));
                    Common.SetToggleIcon(new int[Common.TOGGLE_ICONS_SIZE]
                    {
                        Common.ICON_BIG_CLIPBOARD,
                        -1, -1, -1,
                    });
                    return;
                }

                fileName = headers[1];

                Logger.LogDebug(string.Format(
                    CultureInfo.CurrentCulture,
                    "Receiving {0}:{1} from {2}...",
                    Path.GetFileName(fileName),
                    dataSize,
                    remoteMachine));
                ShowToolTip(
                    string.Format(
                        CultureInfo.CurrentCulture,
                        "Receiving {0} from {1}...",
                        Path.GetFileName(fileName),
                        remoteMachine),
                    5000,
                    ToolTipIcon.Info,
                    Setting.Values.ShowClipNetStatus);
                if (fileName.StartsWith("image", StringComparison.CurrentCultureIgnoreCase) ||
                    fileName.StartsWith("text", StringComparison.CurrentCultureIgnoreCase))
                {
                    m = new MemoryStream();
                }
                else
                {
                    if (postAct.Equals("desktop", StringComparison.OrdinalIgnoreCase))
                    {
                        _ = Launch.ImpersonateLoggedOnUserAndDoSomething(() =>
                        {
                            savingFolder = Environment.GetFolderPath(Environment.SpecialFolder.Desktop) + "\\MouseWithoutBorders\\";

                            if (!Directory.Exists(savingFolder))
                            {
                                _ = Directory.CreateDirectory(savingFolder);
                            }
                        });

                        tempFile = savingFolder + Path.GetFileName(fileName);
                        m = new FileStream(tempFile, FileMode.Create);
                    }
                    else if (postAct.Contains("mspaint"))
                    {
                        tempFile = GetMyStorageDir() + @"ScreenCapture-" +
                            remoteMachine + ".png";
                        m = new FileStream(tempFile, FileMode.Create);
                    }
                    else
                    {
                        tempFile = GetMyStorageDir();
                        tempFile += Path.GetFileName(fileName);
                        m = new FileStream(tempFile, FileMode.Create);
                    }

                    Logger.Log("==> " + tempFile);
                }

                ShowToolTip(
                    string.Format(
                        CultureInfo.CurrentCulture,
                        "Receiving {0} from {1}...",
                        Path.GetFileName(fileName),
                        remoteMachine),
                    5000,
                    ToolTipIcon.Info,
                    Setting.Values.ShowClipNetStatus);

                do
                {
                    rv = deStream.ReadEx(buf, 0, buf.Length);

                    if (rv > 0)
                    {
                        receivedCount += rv;

                        if (receivedCount > dataSize)
                        {
                            rv -= (int)(receivedCount - dataSize);
                        }

                        m.Write(buf, 0, rv);
                    }

                    if (Common.ToggleIcons == null)
                    {
                        Common.SetToggleIcon(new int[Common.TOGGLE_ICONS_SIZE]
                        {
                                    Common.ICON_SMALL_CLIPBOARD,
                                    -1, Common.ICON_SMALL_CLIPBOARD, -1,
                        });
                    }

                    string text = string.Format(CultureInfo.CurrentCulture, "{0}KB received: {1}", m.Length / 1024, Path.GetFileName(fileName));

                    DoSomethingInUIThread(() =>
                    {
                        MainForm.SetTrayIconText(text);
                    });
                }
                while (rv > 0);

                if (m != null && fileName != null)
                {
                    m.Flush();
                    Logger.LogDebug(m.Length.ToString(CultureInfo.CurrentCulture) + " bytes received.");
                    Common.LastClipboardEventTime = Common.GetTick();
                    string toolTipText = null;
                    string sizeText = m.Length >= 1024
                        ? (m.Length / 1024).ToString(CultureInfo.CurrentCulture) + "KB"
                        : m.Length.ToString(CultureInfo.CurrentCulture) + "Bytes";

                    PowerToysTelemetry.Log.WriteEvent(new MouseWithoutBorders.Telemetry.MouseWithoutBordersClipboardFileTransferEvent());

                    if (fileName.StartsWith("image", StringComparison.CurrentCultureIgnoreCase))
                    {
                        Clipboard.SetImage(Image.FromStream(m));
                        toolTipText = string.Format(
                            CultureInfo.CurrentCulture,
                            "{0} {1} from {2} is in Clipboard.",
                            sizeText,
                            "image",
                            remoteMachine);
                    }
                    else if (fileName.StartsWith("text", StringComparison.CurrentCultureIgnoreCase))
                    {
                        byte[] data = (m as MemoryStream).GetBuffer();
                        toolTipText = string.Format(
                            CultureInfo.CurrentCulture,
                            "{0} {1} from {2} is in Clipboard.",
                            sizeText,
                            "text",
                            remoteMachine);
                        Common.SetClipboardData(data);
                    }
                    else if (tempFile != null)
                    {
                        if (postAct.Equals("desktop", StringComparison.OrdinalIgnoreCase))
                        {
                            toolTipText = string.Format(
                                CultureInfo.CurrentCulture,
                                "{0} {1} received from {2}!",
                                sizeText,
                                Path.GetFileName(fileName),
                                remoteMachine);

                            _ = Launch.ImpersonateLoggedOnUserAndDoSomething(() =>
                            {
                                ProcessStartInfo startInfo = new();
                                startInfo.UseShellExecute = true;
                                startInfo.WorkingDirectory = savingFolder;
                                startInfo.FileName = savingFolder;
                                startInfo.Verb = "open";
                                _ = Process.Start(startInfo);
                            });
                        }
                        else if (postAct.Contains("mspaint"))
                        {
                            m.Close();
                            m = null;
                            OpenImage(tempFile);
                            toolTipText = string.Format(
                                CultureInfo.CurrentCulture,
                                "{0} {1} from {2} is in Mspaint.",
                                sizeText,
                                Path.GetFileName(tempFile),
                                remoteMachine);
                        }
                        else
                        {
                            StringCollection filePaths = new()
                            {
                                tempFile,
                            };
                            Clipboard.SetFileDropList(filePaths);
                            toolTipText = string.Format(
                                CultureInfo.CurrentCulture,
                                "{0} {1} from {2} is in Clipboard.",
                                sizeText,
                                Path.GetFileName(fileName),
                                remoteMachine);
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(toolTipText))
                    {
                        Common.ShowToolTip(toolTipText, 5000, ToolTipIcon.Info, Setting.Values.ShowClipNetStatus);
                    }

                    DoSomethingInUIThread(() =>
                    {
                        MainForm.UpdateNotifyIcon();
                    });

                    m?.Close();
                    m = null;
                }
            }
            catch (ThreadAbortException)
            {
                Logger.Log("The current thread is being aborted (3).");
                s.Close();

                if (m != null)
                {
                    m.Close();
                    m = null;
                }

                return;
            }
            catch (Exception e)
            {
                if (e is IOException)
                {
                    string log = $"{nameof(ReceiveAndProcessClipboardData)}: Exception accessing the socket: {e.InnerException?.GetType()}/{e.Message}. (This is expected when the remote machine closes the connection during desktop switch or reconnection.)";
                    Logger.Log(log);
                }
                else
                {
                    Logger.Log(e);
                }

                Common.SetToggleIcon(new int[Common.TOGGLE_ICONS_SIZE]
                {
                    Common.ICON_BIG_CLIPBOARD,
                    -1, Common.ICON_BIG_CLIPBOARD, -1,
                });
                ShowToolTip(e.Message, 1000, ToolTipIcon.Info, Setting.Values.ShowClipNetStatus);

                if (m != null)
                {
                    m.Close();
                    m = null;
                }

                return;
            }

            s.Close();
        }

        internal static bool ShakeHand(ref string remoteName, Socket s, out Stream enStream, out Stream deStream, ref bool clientPushData, ref ClipboardPostAction postAction)
        {
            const int CLIPBOARD_HANDSHAKE_TIMEOUT = 30;
            s.ReceiveTimeout = CLIPBOARD_HANDSHAKE_TIMEOUT * 1000;
            s.NoDelay = true;
            s.SendBufferSize = s.ReceiveBufferSize = 1024000;

            bool handShaken = false;
            enStream = deStream = null;

            try
            {
                DATA package = new()
                {
                    Type = clientPushData ? PackageType.ClipboardPush : PackageType.Clipboard,
                    PostAction = postAction,
                    Src = MachineID,
                    MachineName = MachineName,
                };

                byte[] buf = new byte[PACKAGE_SIZE_EX];

                NetworkStream ns = new(s);
                enStream = Common.GetEncryptedStream(ns);
                Common.SendOrReceiveARandomDataBlockPerInitialIV(enStream);
                Logger.LogDebug($"{nameof(ShakeHand)}: Writing header package.");
                enStream.Write(package.Bytes, 0, PACKAGE_SIZE_EX);

                Logger.LogDebug($"{nameof(ShakeHand)}: Sent: clientPush={clientPushData}, postAction={postAction}.");

                deStream = Common.GetDecryptedStream(ns);
                Common.SendOrReceiveARandomDataBlockPerInitialIV(deStream, false);

                Logger.LogDebug($"{nameof(ShakeHand)}: Reading header package.");

                int bytesReceived = deStream.ReadEx(buf, 0, Common.PACKAGE_SIZE_EX);
                package.Bytes = buf;

                string name = "Unknown";

                if (bytesReceived == Common.PACKAGE_SIZE_EX)
                {
                    if (package.Type is PackageType.Clipboard or PackageType.ClipboardPush)
                    {
                        name = remoteName = package.MachineName;

                        Logger.LogDebug($"{nameof(ShakeHand)}: Connection from {name}:{package.Src}");

                        if (MachineStuff.MachinePool.ResolveID(name) == package.Src && Common.IsConnectedTo(package.Src))
                        {
                            clientPushData = package.Type == PackageType.ClipboardPush;
                            postAction = package.PostAction;
                            handShaken = true;
                            Logger.LogDebug($"{nameof(ShakeHand)}: Received: clientPush={clientPushData}, postAction={postAction}.");
                        }
                        else
                        {
                            Logger.LogDebug($"{nameof(ShakeHand)}: No active connection to the machine: {name}.");
                        }
                    }
                    else
                    {
                        Logger.LogDebug($"{nameof(ShakeHand)}: Unexpected package type: {package.Type}.");
                    }
                }
                else
                {
                    Logger.LogDebug($"{nameof(ShakeHand)}: BytesTransferred != PACKAGE_SIZE_EX: {bytesReceived}");
                }

                if (!handShaken)
                {
                    string msg = $"Clipboard connection rejected: {name}:{remoteName}/{package.Src}\r\n\r\nMake sure you run the same version in all machines.";
                    Logger.Log(msg);
                    Common.ShowToolTip(msg, 3000, ToolTipIcon.Warning);
                    Common.SetToggleIcon(new int[Common.TOGGLE_ICONS_SIZE] { Common.ICON_BIG_CLIPBOARD, -1, -1, -1 });
                }
            }
            catch (ThreadAbortException)
            {
                Logger.Log($"{nameof(ShakeHand)}: The current thread is being aborted.");
                s.Close();
            }
            catch (Exception e)
            {
                if (e is IOException)
                {
                    string log = $"{nameof(ShakeHand)}: Exception accessing the socket: {e.InnerException?.GetType()}/{e.Message}. (This is expected when the remote machine closes the connection during desktop switch or reconnection.)";
                    Logger.Log(log);
                }
                else
                {
                    Logger.Log(e);
                }

                Common.SetToggleIcon(new int[Common.TOGGLE_ICONS_SIZE]
                {
                    Common.ICON_BIG_CLIPBOARD,
                    -1, Common.ICON_BIG_CLIPBOARD, -1,
                });
                MainForm.UpdateNotifyIcon();
                ShowToolTip(e.Message + "\r\n\r\nMake sure you run the same version in all machines.", 1000, ToolTipIcon.Warning, Setting.Values.ShowClipNetStatus);

                if (m != null)
                {
                    m.Close();
                    m = null;
                }
            }

            return handShaken;
        }

        internal static TcpClient ConnectToRemoteClipboardSocket(string remoteMachine)
        {
            TcpClient clipboardTcpClient;
            clipboardTcpClient = new TcpClient(AddressFamily.InterNetworkV6);
            clipboardTcpClient.Client.DualMode = true;

            SocketStuff sk = Common.Sk;

            if (sk != null)
            {
                Common.DoSomethingInUIThread(() => Common.MainForm.ChangeIcon(Common.ICON_SMALL_CLIPBOARD));

                System.Net.IPAddress ip = GetConnectedClientSocketIPAddressFor(remoteMachine);
                Logger.LogDebug($"{nameof(ConnectToRemoteClipboardSocket)}Connecting to {remoteMachine}:{ip}:{sk.TcpPort}...");

                if (ip != null)
                {
                    clipboardTcpClient.Connect(ip, sk.TcpPort);
                }
                else
                {
                    clipboardTcpClient.Connect(remoteMachine, sk.TcpPort);
                }

                Logger.LogDebug($"Connected from {clipboardTcpClient.Client.LocalEndPoint}. Getting data...");
                return clipboardTcpClient;
            }
            else
            {
                throw new ExpectedSocketException($"{nameof(ConnectToRemoteClipboardSocket)}: No longer connected.");
            }
        }

        internal static void SetClipboardData(byte[] data)
        {
            if (data == null || data.Length <= 0)
            {
                Logger.Log("data is null or empty!");
                return;
            }

            if (data.Length > 1024000)
            {
                ShowToolTip(
                    string.Format(
                        CultureInfo.CurrentCulture,
                        "Decompressing {0} clipboard data ...",
                        (data.Length / 1024).ToString(CultureInfo.CurrentCulture) + "KB"),
                    5000,
                    ToolTipIcon.Info,
                    Setting.Values.ShowClipNetStatus);
            }

            string st = string.Empty;

            using (MemoryStream ms = new(data))
            {
                using DeflateStream s = new(ms, CompressionMode.Decompress, true);
                const int BufferSize = 1024000; // Buffer size should be big enough, this is critical to performance!

                int rv = 0;

                do
                {
                    byte[] buffer = new byte[BufferSize];
                    rv = s.ReadEx(buffer, 0, BufferSize);

                    if (rv > 0)
                    {
                        st += Common.GetStringU(buffer);
                    }
                    else
                    {
                        break;
                    }
                }
                while (true);
            }

            int textTypeCount = 0;
            string[] texts = st.Split(new string[] { TEXT_TYPE_SEP }, StringSplitOptions.RemoveEmptyEntries);
            string tmp;
            DataObject data1 = new();

            foreach (string txt in texts)
            {
                if (string.IsNullOrEmpty(txt.Trim(NullSeparator)))
                {
                    continue;
                }

                tmp = txt[3..];

                if (txt.StartsWith("RTF", StringComparison.CurrentCultureIgnoreCase))
                {
                    Logger.LogDebug(((double)tmp.Length / 1024).ToString("0.00", CultureInfo.InvariantCulture) + "KB of RTF <-");
                    data1.SetData(DataFormats.Rtf, tmp);
                }
                else if (txt.StartsWith("HTM", StringComparison.CurrentCultureIgnoreCase))
                {
                    Logger.LogDebug(((double)tmp.Length / 1024).ToString("0.00", CultureInfo.InvariantCulture) + "KB of HTM <-");
                    data1.SetData(DataFormats.Html, tmp);
                }
                else if (txt.StartsWith("TXT", StringComparison.CurrentCultureIgnoreCase))
                {
                    Logger.LogDebug(((double)tmp.Length / 1024).ToString("0.00", CultureInfo.InvariantCulture) + "KB of TXT <-");
                    data1.SetData(DataFormats.UnicodeText, tmp);
                }
                else
                {
                    if (textTypeCount == 0)
                    {
                        Logger.LogDebug(((double)txt.Length / 1024).ToString("0.00", CultureInfo.InvariantCulture) + "KB of UNI <-");
                        data1.SetData(DataFormats.UnicodeText, txt);
                    }

                    Logger.Log("Invalid clipboard format received!");
                }

                textTypeCount++;
            }

            if (textTypeCount > 0)
            {
                Clipboard.SetDataObject(data1);
            }
        }
    }

    internal static class Clipboard
    {
        public static void SetFileDropList(StringCollection filePaths)
        {
            Common.DoSomethingInUIThread(() =>
            {
                try
                {
                    _ = Common.Retry(
                        nameof(SystemClipboard.SetFileDropList),
                        () =>
                        {
                            SystemClipboard.SetFileDropList(filePaths);
                            return true;
                        },
                        (log) => Logger.TelemetryLogTrace(
                            log,
                            SeverityLevel.Information),
                        () => Common.LastClipboardEventTime = Common.GetTick());
                }
                catch (ExternalException e)
                {
                    Logger.Log(e);
                }
                catch (ThreadStateException e)
                {
                    Logger.Log(e);
                }
                catch (ArgumentNullException e)
                {
                    Logger.Log(e);
                }
                catch (ArgumentException e)
                {
                    Logger.Log(e);
                }
            });
        }

        public static void SetImage(Image image)
        {
            Common.DoSomethingInUIThread(() =>
            {
                try
                {
                    _ = Common.Retry(
                        nameof(SystemClipboard.SetImage),
                        () =>
                    {
                        SystemClipboard.SetImage(image);
                        return true;
                    },
                        (log) => Logger.TelemetryLogTrace(log, SeverityLevel.Information),
                        () => Common.LastClipboardEventTime = Common.GetTick());
                }
                catch (ExternalException e)
                {
                    Logger.Log(e);
                }
                catch (ThreadStateException e)
                {
                    Logger.Log(e);
                }
                catch (ArgumentNullException e)
                {
                    Logger.Log(e);
                }
            });
        }

        public static void SetText(string text)
        {
            Common.DoSomethingInUIThread(() =>
            {
                try
                {
                    _ = Common.Retry(
                        nameof(SystemClipboard.SetText),
                        () =>
                    {
                        SystemClipboard.SetText(text);
                        return true;
                    },
                        (log) => Logger.TelemetryLogTrace(log, SeverityLevel.Information),
                        () => Common.LastClipboardEventTime = Common.GetTick());
                }
                catch (ExternalException e)
                {
                    Logger.Log(e);
                }
                catch (ThreadStateException e)
                {
                    Logger.Log(e);
                }
                catch (ArgumentNullException e)
                {
                    Logger.Log(e);
                }
            });
        }

        public static void SetDataObject(DataObject dataObject)
        {
            Common.DoSomethingInUIThread(() =>
            {
                try
                {
                    SystemClipboard.SetDataObject(dataObject, true, 10, 200);
                }
                catch (ExternalException e)
                {
                    string dataFormats = string.Join(",", dataObject.GetFormats());
                    Logger.Log($"{e.Message}: {dataFormats}");
                }
                catch (ThreadStateException e)
                {
                    Logger.Log(e);
                }
                catch (ArgumentNullException e)
                {
                    Logger.Log(e);
                }
            });
        }
    }
}
