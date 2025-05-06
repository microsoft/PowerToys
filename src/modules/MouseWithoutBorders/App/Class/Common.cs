// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows.Forms;

using Microsoft.PowerToys.Settings.UI.Library;

// <summary>
//     Most of the helper methods.
// </summary>
// <history>
//     2008 created by Truong Do (ductdo).
//     2009-... modified by Truong Do (TruongDo).
//     2023- Included in PowerToys.
// </history>
using MouseWithoutBorders.Class;
using MouseWithoutBorders.Core;
using MouseWithoutBorders.Exceptions;

using Thread = MouseWithoutBorders.Core.Thread;

// Log is enough
[module: SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Scope = "member", Target = "MouseWithoutBorders.Common.#CheckClipboard()", Justification = "Dotnet port with style preservation")]
[module: SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Scope = "member", Target = "MouseWithoutBorders.Common.#CheckForDesktopSwitchEvent(System.Boolean)", Justification = "Dotnet port with style preservation")]
[module: SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Scope = "member", Target = "MouseWithoutBorders.Common.#SetAsStartupItem(System.Boolean)", Justification = "Dotnet port with style preservation")]
[module: SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Scope = "member", Target = "MouseWithoutBorders.Common.#HelperThread()", Justification = "Dotnet port with style preservation")]
[module: SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Scope = "member", Target = "MouseWithoutBorders.Common.#GetMyStorageDir()", Justification = "Dotnet port with style preservation")]
[module: SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Scope = "member", Target = "MouseWithoutBorders.Common.#MouseEvent(MouseWithoutBorders.MOUSEDATA)", Justification = "Dotnet port with style preservation")]
[module: SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Scope = "member", Target = "MouseWithoutBorders.Common.#KeybdEvent(MouseWithoutBorders.KEYBDDATA)", Justification = "Dotnet port with style preservation")]
[module: SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Scope = "member", Target = "MouseWithoutBorders.Common.#ImpersonateLoggedOnUserAndDoSomething(System.Threading.ThreadStart)", Justification = "Dotnet port with style preservation")]
[module: SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Scope = "member", Target = "MouseWithoutBorders.Common.#StartMouseWithoutBordersService()", Justification = "Dotnet port with style preservation")]
[module: SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Scope = "member", Target = "MouseWithoutBorders.Common.#HookClipboard()", Justification = "Dotnet port with style preservation")]
[module: SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Scope = "member", Target = "MouseWithoutBorders.Common.#ReceiveClipboardData(MouseWithoutBorders.DATA,System.Boolean)", Justification = "Dotnet port with style preservation")]
[module: SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Scope = "member", Target = "MouseWithoutBorders.Common.#ReceiverCallback(System.Object)", Justification = "Dotnet port with style preservation")]
[module: SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Scope = "member", Target = "MouseWithoutBorders.Common.#ConnectAndGetData(System.Object)", Justification = "Dotnet port with style preservation")]
[module: SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Scope = "member", Target = "MouseWithoutBorders.Common.#CheckNewVersion()", Justification = "Dotnet port with style preservation")]
[module: SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Scope = "member", Target = "MouseWithoutBorders.Common.#StartServiceAndSendLogoffSignal()", Justification = "Dotnet port with style preservation")]
[module: SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Scope = "member", Target = "MouseWithoutBorders.Common.#GetScreenConfig()", Justification = "Dotnet port with style preservation")]
[module: SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Scope = "member", Target = "MouseWithoutBorders.Common.#CaptureScreen()", Justification = "Dotnet port with style preservation")]
[module: SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Scope = "member", Target = "MouseWithoutBorders.Common.#InitEncryption()", Justification = "Dotnet port with style preservation")]
[module: SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Scope = "member", Target = "MouseWithoutBorders.Common.#ToggleIcon()", Justification = "Dotnet port with style preservation")]
[module: SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Scope = "member", Target = "MouseWithoutBorders.Common.#GetNameAndIPAddresses()", Justification = "Dotnet port with style preservation")]
[module: SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Scope = "member", Target = "MouseWithoutBorders.Common.#Cleanup()", Justification = "Dotnet port with style preservation")]
[module: SuppressMessage("Microsoft.Maintainability", "CA1506:AvoidExcessiveClassCoupling", Scope = "type", Target = "MouseWithoutBorders.Common", Justification = "Dotnet port with style preservation")]
[module: SuppressMessage("Microsoft.Maintainability", "CA1502:AvoidExcessiveComplexity", Scope = "member", Target = "MouseWithoutBorders.Common.#ConnectAndGetData(System.Object)", Justification = "Dotnet port with style preservation")]
[module: SuppressMessage("Microsoft.Maintainability", "CA1502:AvoidExcessiveComplexity", Scope = "member", Target = "MouseWithoutBorders.Common.#ProcessPackage(MouseWithoutBorders.DATA)", Justification = "Dotnet port with style preservation")]
[module: SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Scope = "member", Target = "MouseWithoutBorders.Common.#SetOEMBackground(System.Boolean)", Justification = "Dotnet port with style preservation")]
[module: SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Scope = "member", Target = "MouseWithoutBorders.Common.#get_Machine_Pool()", Justification = "Dotnet port with style preservation")]
[module: SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Scope = "member", Target = "MouseWithoutBorders.Common.#SetOEMBackground(System.Boolean,System.String)", Justification = "Dotnet port with style preservation")]
[module: SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Scope = "member", Target = "MouseWithoutBorders.Common.#GetNewImageAndSaveTo(System.String,System.String)", Justification = "Dotnet port with style preservation")]
[module: SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Scope = "member", Target = "MouseWithoutBorders.Common.#CreateLowIntegrityProcess(System.String,System.String,System.Int32)", Justification = "Dotnet port with style preservation")]
[module: SuppressMessage("Microsoft.Usage", "CA1806:DoNotIgnoreMethodResults", Scope = "member", Target = "MouseWithoutBorders.Common.#LogAll()", MessageId = "System.String.Format(System.IFormatProvider,System.String,System.Object[])", Justification = "Dotnet port with style preservation")]
[module: SuppressMessage("Microsoft.Usage", "CA1806:DoNotIgnoreMethodResults", Scope = "member", Target = "MouseWithoutBorders.Common.#CheckForDesktopSwitchEvent(System.Boolean)", MessageId = "MouseWithoutBorders.NativeMethods.SendMessage(System.IntPtr,System.Int32,System.IntPtr,System.IntPtr)", Justification = "Dotnet port with style preservation")]
[module: SuppressMessage("Microsoft.Usage", "CA1806:DoNotIgnoreMethodResults", Scope = "member", Target = "MouseWithoutBorders.Common.#DragDropStep04()", MessageId = "MouseWithoutBorders.NativeMethods.SendMessage(System.IntPtr,System.Int32,System.IntPtr,System.IntPtr)", Justification = "Dotnet port with style preservation")]
[module: SuppressMessage("Microsoft.Usage", "CA1806:DoNotIgnoreMethodResults", Scope = "member", Target = "MouseWithoutBorders.Common.#CreateLowIntegrityProcess(System.String,System.String,System.Int32)", MessageId = "MouseWithoutBorders.NativeMethods.WaitForSingleObject(System.IntPtr,System.Int32)", Justification = "Dotnet port with style preservation")]
[module: SuppressMessage("Microsoft.Usage", "CA1806:DoNotIgnoreMethodResults", Scope = "member", Target = "MouseWithoutBorders.Common.#GetText(System.IntPtr)", MessageId = "MouseWithoutBorders.NativeMethods.GetWindowText(System.IntPtr,System.Text.StringBuilder,System.Int32)", Justification = "Dotnet port with style preservation")]
[module: SuppressMessage("Microsoft.Usage", "CA1806:DoNotIgnoreMethodResults", Scope = "member", Target = "MouseWithoutBorders.Common.#ImpersonateLoggedOnUserAndDoSomething(System.Threading.ThreadStart)", MessageId = "MouseWithoutBorders.NativeMethods.WTSQueryUserToken(System.UInt32,System.IntPtr@)", Justification = "Dotnet port with style preservation")]
[module: SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Scope = "member", Target = "MouseWithoutBorders.Common.#CreateLowIntegrityProcess(System.String,System.String,System.Int32,System.Boolean,System.Int64)", Justification = "Dotnet port with style preservation")]
[module: SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Scope = "member", Target = "MouseWithoutBorders.Common.#CreateProcessInInputDesktopSession(System.String,System.String,System.String,System.Boolean,System.Int16)", Justification = "Dotnet port with style preservation")]
[module: SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Scope = "member", Target = "MouseWithoutBorders.Common.#SkSend(MouseWithoutBorders.DATA,System.Boolean,System.Int32)", Justification = "Dotnet port with style preservation")]
[module: SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Scope = "member", Target = "MouseWithoutBorders.Common.#ReceiveClipboardDataUsingTCP(MouseWithoutBorders.DATA,System.Boolean,System.Net.Sockets.Socket)", Justification = "Dotnet port with style preservation")]
[module: SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Scope = "member", Target = "MouseWithoutBorders.Common.#UpdateMachineMatrix(MouseWithoutBorders.DATA)", Justification = "Dotnet port with style preservation")]
[module: SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Scope = "member", Target = "MouseWithoutBorders.Common.#ReopenSockets(System.Boolean)", Justification = "Dotnet port with style preservation")]

namespace MouseWithoutBorders
{
    internal partial class Common
    {
        internal Common()
        {
        }

        private static InputHook hook;
        private static FrmMatrix matrixForm;
        private static FrmInputCallback inputCallbackForm;
        private static FrmAbout aboutForm;
        private static Thread helper;
#pragma warning disable SA1307 // Accessible fields should begin with upper-case letter
        internal static int screenWidth;
        internal static int screenHeight;
#pragma warning restore SA1307
        private static int lastX;
        private static int lastY;

        private static bool mainFormVisible = true;
        private static bool runOnLogonDesktop;
        private static bool runOnScrSaverDesktop;

#pragma warning disable SA1307 // Accessible fields should begin with upper-case letter
        internal static int[] toggleIcons;
        internal static int toggleIconsIndex;
#pragma warning restore SA1307
        internal const int TOGGLE_ICONS_SIZE = 4;
        internal const int ICON_ONE = 0;
        internal const int ICON_ALL = 1;
        internal const int ICON_SMALL_CLIPBOARD = 2;
        internal const int ICON_BIG_CLIPBOARD = 3;
        internal const int ICON_ERROR = 4;
        internal const int JUST_GOT_BACK_FROM_SCREEN_SAVER = 9999;

        internal const int NETWORK_STREAM_BUF_SIZE = 1024 * 1024;
        internal static readonly EventWaitHandle EvSwitch = new(false, EventResetMode.AutoReset);
        private static Point lastPos;
#pragma warning disable SA1307 // Accessible fields should begin with upper-case names
        internal static int switchCount;
#pragma warning restore SA1307
        private static long lastReconnectByHotKeyTime;
        private static int tcpPort;
        private static bool secondOpenSocketTry;
        private static string binaryName;

        internal static Process CurrentProcess { get; set; }

        internal static bool HotkeyMatched(int vkCode, bool winDown, bool ctrlDown, bool altDown, bool shiftDown, HotkeySettings hotkey)
        {
            return !hotkey.IsEmpty() && (vkCode == hotkey.Code) && (!hotkey.Win || winDown) && (!hotkey.Alt || altDown) && (!hotkey.Shift || shiftDown) && (!hotkey.Ctrl || ctrlDown);
        }

        public static string BinaryName
        {
            get => Common.binaryName;
            set => Common.binaryName = value;
        }

        public static bool SecondOpenSocketTry
        {
            get => Common.secondOpenSocketTry;
            set => Common.secondOpenSocketTry = value;
        }

        public static long LastReconnectByHotKeyTime
        {
            get => Common.lastReconnectByHotKeyTime;
            set => Common.lastReconnectByHotKeyTime = value;
        }

        public static int SwitchCount
        {
            get => Common.switchCount;
            set => Common.switchCount = value;
        }

        public static Point LastPos
        {
            get => Common.lastPos;
            set => Common.lastPos = value;
        }

        internal static FrmAbout AboutForm
        {
            get => Common.aboutForm;
            set => Common.aboutForm = value;
        }

        internal static FrmInputCallback InputCallbackForm
        {
            get => Common.inputCallbackForm;
            set => Common.inputCallbackForm = value;
        }

        public static int PaintCount { get; set; }

        internal static bool RunOnScrSaverDesktop
        {
            get => Common.runOnScrSaverDesktop;
            set => Common.runOnScrSaverDesktop = value;
        }

        internal static bool RunOnLogonDesktop
        {
            get => Common.runOnLogonDesktop;
            set => Common.runOnLogonDesktop = value;
        }

        internal static bool RunWithNoAdminRight { get; set; }

        internal static int LastX
        {
            get => Common.lastX;
            set => Common.lastX = value;
        }

        internal static int LastY
        {
            get => Common.lastY;
            set => Common.lastY = value;
        }

        internal static int[] ToggleIcons => Common.toggleIcons;

        internal static int ScreenHeight => Common.screenHeight;

        internal static int ScreenWidth => Common.screenWidth;

        internal static bool Is64bitOS
        {
            get; private set;

            // set { Common.is64bitOS = value; }
        }

        internal static int ToggleIconsIndex
        {
            // get { return Common.toggleIconsIndex; }
            set => Common.toggleIconsIndex = value;
        }

        internal static InputHook Hook
        {
            get => Common.hook;
            set => Common.hook = value;
        }

        internal static SocketStuff Sk { get; set; }

        internal static FrmScreen MainForm { get; set; }

        internal static FrmMouseCursor MouseCursorForm { get; set; }

        internal static FrmMatrix MatrixForm
        {
            get => Common.matrixForm;
            set => Common.matrixForm = value;
        }

        internal static ID DesMachineID
        {
            get => MachineStuff.desMachineID;

            set
            {
                MachineStuff.desMachineID = value;
                MachineStuff.DesMachineName = MachineStuff.NameFromID(MachineStuff.desMachineID);
            }
        }

        internal static ID MachineID => (ID)Setting.Values.MachineId;

        internal static string MachineName { get; set; }

        internal static bool MainFormVisible
        {
            get => Common.mainFormVisible;
            set => Common.mainFormVisible = value;
        }

        internal static Mutex SocketMutex { get; set; } // Synchronization between MouseWithoutBorders running in different desktops

        // TODO: For telemetry only, to be removed.
        private static int socketMutexBalance;

        internal static void ReleaseSocketMutex()
        {
            if (SocketMutex != null)
            {
                Logger.LogDebug("SOCKET MUTEX BEGIN RELEASE.");

                try
                {
                    _ = Interlocked.Decrement(ref socketMutexBalance);
                    SocketMutex.ReleaseMutex();
                }
                catch (ApplicationException e)
                {
                    // The current thread does not own the mutex, the thread acquired it will own it.
                    Logger.TelemetryLogTrace($"{nameof(ReleaseSocketMutex)}: {e.Message}. {Thread.CurrentThread.ManagedThreadId}/{UIThreadID}.", SeverityLevel.Warning);
                }

                Logger.LogDebug("SOCKET MUTEX RELEASED.");
            }
            else
            {
                Logger.LogDebug("SOCKET MUTEX NULL.");
            }
        }

        internal static void AcquireSocketMutex()
        {
            if (SocketMutex != null)
            {
                Logger.LogDebug("SOCKET MUTEX BEGIN WAIT.");
                int waitTimeout = 60000; // TcpListener.Stop may take very long to complete for some reason.

                int socketMutexBalance = int.MinValue;

                bool acquireMutex = ExecuteAndTrace(
                    "Waiting for sockets to close",
                    () =>
                    {
                        socketMutexBalance = Interlocked.Increment(ref Common.socketMutexBalance);
                        _ = SocketMutex.WaitOne(waitTimeout); // The app now requires .Net 4.0. Note: .Net20RTM does not have the one-parameter version of the API.
                    },
                    TimeSpan.FromSeconds(5));

                // Took longer than expected.
                if (!acquireMutex)
                {
                    Process[] ps = Process.GetProcessesByName(Common.BinaryName);
                    Logger.TelemetryLogTrace($"Balance: {socketMutexBalance}, Active: {IsMyDesktopActive()}, Sid/Console: {Process.GetCurrentProcess().SessionId}/{NativeMethods.WTSGetActiveConsoleSessionId()}, Desktop/Input: {GetMyDesktop()}/{GetInputDesktop()}, count: {ps?.Length}.", SeverityLevel.Warning);
                }

                Logger.LogDebug("SOCKET MUTEX ENDED.");
            }
            else
            {
                Logger.LogDebug("SOCKET MUTEX NULL.");
            }
        }

        internal static bool BlockingUI { get; private set; }

        internal static bool ExecuteAndTrace(string actionName, Action action, TimeSpan timeout, bool restart = false)
        {
            bool rv = true;
            Logger.LogDebug(actionName);
            bool done = false;

            BlockingUI = true;

            if (restart)
            {
                Common.MainForm.Text = Setting.Values.MyIdEx;

                /* closesocket() rarely gets stuck for some reason inside ntdll!ZwClose ...=>... afd!AfdCleanupCore.
                 * There is no good workaround for it so far, still working with [Winsock 2.0 Discussions] to address the issue.
                 * */
                new Thread(
                    () =>
                {
                    for (int i = 0; i < timeout.TotalSeconds; i++)
                    {
                        Thread.Sleep(1000);

                        if (done)
                        {
                            return;
                        }
                    }

                    Logger.TelemetryLogTrace($"[{actionName}] took more than {(long)timeout.TotalSeconds}, restarting the process.", SeverityLevel.Warning, true);

                    string desktop = Common.GetMyDesktop();
                    MachineStuff.oneInstanceCheck?.Close();
                    _ = Process.Start(Application.ExecutablePath, desktop);
                    Logger.LogDebug($"Started on desktop {desktop}");

                    Process.GetCurrentProcess().KillProcess(true);
                },
                    $"{actionName} watchdog").Start();
            }

            Stopwatch timer = Stopwatch.StartNew();

            try
            {
                action();
            }
            finally
            {
                done = true;
                BlockingUI = false;

                if (restart)
                {
                    Common.MainForm.Text = Setting.Values.MyID;
                }

                timer.Stop();

                if (timer.Elapsed > timeout)
                {
                    rv = false;

                    if (!restart)
                    {
                        Logger.TelemetryLogTrace($"[{actionName}] took more than {(long)timeout.TotalSeconds}: {(long)timer.Elapsed.TotalSeconds}.", SeverityLevel.Warning);
                    }
                }
            }

            return rv;
        }

        internal static byte[] GetBytes(string st)
        {
            return ASCIIEncoding.ASCII.GetBytes(st);
        }

        internal static string GetString(byte[] bytes)
        {
            return ASCIIEncoding.ASCII.GetString(bytes);
        }

        internal static byte[] GetBytesU(string st)
        {
            return ASCIIEncoding.Unicode.GetBytes(st);
        }

        internal static string GetStringU(byte[] bytes)
        {
            return ASCIIEncoding.Unicode.GetString(bytes);
        }

        internal static int UIThreadID { get; set; }

        internal static void DoSomethingInUIThread(Action action, bool blocking = false)
        {
            InvokeInFormThread(MainForm, UIThreadID, action, blocking);
        }

        internal static int InputCallbackThreadID { get; set; }

        internal static void DoSomethingInTheInputCallbackThread(Action action, bool blocking = true)
        {
            InvokeInFormThread(InputCallbackForm, InputCallbackThreadID, action, blocking);
        }

        private static void InvokeInFormThread(System.Windows.Forms.Form form, int threadId, Action action, bool blocking)
        {
            if (form != null)
            {
                int currentThreadId = Thread.CurrentThread.ManagedThreadId;

                if (currentThreadId == threadId)
                {
                    action();
                }
                else
                {
                    bool done = false;

                    try
                    {
                        Action callback = () =>
                        {
                            try
                            {
                                action();
                            }
                            catch (Exception e)
                            {
                                Logger.Log(e);
                            }
                            finally
                            {
                                done = true;
                            }
                        };
                        _ = form.BeginInvoke(callback);
                    }
                    catch (Exception e)
                    {
                        done = true;
                        Logger.Log(e);
                    }

                    while (blocking && !done)
                    {
                        Thread.Sleep(16);

                        if (currentThreadId == UIThreadID || currentThreadId == InputCallbackThreadID)
                        {
                            Application.DoEvents();
                        }
                    }
                }
            }
        }

        private static readonly Lock InputSimulationLock = new();

        internal static void DoSomethingInTheInputSimulationThread(ThreadStart target)
        {
            /*
             * For some reason, SendInput may hit deadlock if it is called in the InputHookProc thread.
             * For now leave it as is in the caller thread which is the socket receiver thread.
             * */

            // SendInput is thread-safe but few users seem to hit a deadlock occasionally, probably a Windows bug.
            lock (InputSimulationLock)
            {
                target();
            }
        }

        internal static void SendPackage(ID des, PackageType packageType)
        {
            DATA package = new();
            package.Type = packageType;
            package.Des = des;
            package.MachineName = MachineName;

            SkSend(package, null, false);
        }

        internal static void SendHeartBeat(bool initial = false)
        {
            SendPackage(ID.ALL, initial && Common.GeneratedKey ? PackageType.Heartbeat_ex : PackageType.Heartbeat);
        }

        private static long lastSendNextMachine;

        internal static void SendNextMachine(ID hostMachine, ID nextMachine, Point requestedXY)
        {
            Logger.LogDebug($"SendNextMachine: Host machine: {hostMachine}, Next machine: {nextMachine}, Requested XY: {requestedXY}");

            if (GetTick() - lastSendNextMachine < 100)
            {
                Logger.LogDebug("Machine switching in progress."); // "Move Mouse relatively" mode, slow machine/network, quick/busy hand.
                return;
            }

            lastSendNextMachine = GetTick();

            DATA package = new();
            package.Type = PackageType.NextMachine;

            package.Des = hostMachine;

            package.Md.X = requestedXY.X;
            package.Md.Y = requestedXY.Y;
            package.Md.WheelDelta = (int)nextMachine;

            SkSend(package, null, false);

            Logger.LogDebug("SendNextMachine done.");
        }

        private static ulong lastInputEventCount;
        private static ulong lastRealInputEventCount;

        internal static void SendAwakeBeat()
        {
            if (!Common.RunOnLogonDesktop && !Common.RunOnScrSaverDesktop && Common.IsMyDesktopActive() &&
                Setting.Values.BlockScreenSaver && lastRealInputEventCount != Event.RealInputEventCount)
            {
                SendPackage(ID.ALL, PackageType.Awake);
            }
            else
            {
                SendHeartBeat();
            }

            lastInputEventCount = Event.InputEventCount;
            lastRealInputEventCount = Event.RealInputEventCount;
        }

        internal static void HumanBeingDetected()
        {
            if (lastInputEventCount == Event.InputEventCount)
            {
                if (!Common.RunOnLogonDesktop && !Common.RunOnScrSaverDesktop && Common.IsMyDesktopActive())
                {
                    PokeMyself();
                }
            }

            lastInputEventCount = Event.InputEventCount;
        }

        private static void PokeMyself()
        {
            int x, y = 0;

            for (int i = 0; i < 10; i++)
            {
                x = Ran.Next(-9, 10);
                InputSimulation.MoveMouseRelative(x, y);
                Thread.Sleep(50);
                InputSimulation.MoveMouseRelative(-x, -y);
                Thread.Sleep(50);

                if (lastInputEventCount != Event.InputEventCount)
                {
                    break;
                }
            }
        }

        internal static void InitLastInputEventCount()
        {
            lastInputEventCount = Event.InputEventCount;
            lastRealInputEventCount = Event.RealInputEventCount;
        }

        internal static void SendHello()
        {
            SendPackage(ID.ALL, PackageType.Hello);
        }

        /*
        internal static void SendHi()
        {
            SendPackage(IP.ALL, PackageType.hi);
        }
         * */

        private static void SendByeBye()
        {
            Logger.LogDebug($"{nameof(SendByeBye)}");
            SendPackage(ID.ALL, PackageType.ByeBye);
        }

        internal static void SendClipboardBeat()
        {
            SendPackage(ID.ALL, PackageType.Clipboard);
        }

        internal static void ProcessByeByeMessage(DATA package)
        {
            if (package.Src == MachineStuff.desMachineID)
            {
                MachineStuff.SwitchToMachine(MachineName.Trim());
            }

            _ = MachineStuff.RemoveDeadMachines(package.Src);
        }

        internal static long GetTick() // ms
        {
            return DateTime.Now.Ticks / 10000;
        }

        internal static void SetToggleIcon(int[] toggleIcons)
        {
            Logger.LogDebug($"{nameof(SetToggleIcon)}: {toggleIcons?.FirstOrDefault()}");
            Common.toggleIcons = toggleIcons;
            toggleIconsIndex = 0;
        }

        internal static string CaptureScreen()
        {
            try
            {
                string fileName = GetMyStorageDir() + @"ScreenCaptureByMouseWithoutBorders.png";
                int w = MachineStuff.desktopBounds.Right - MachineStuff.desktopBounds.Left;
                int h = MachineStuff.desktopBounds.Bottom - MachineStuff.desktopBounds.Top;
                Bitmap bm = new(w, h);
                Graphics g = Graphics.FromImage(bm);
                Size s = new(w, h);
                g.CopyFromScreen(MachineStuff.desktopBounds.Left, MachineStuff.desktopBounds.Top, 0, 0, s);
                bm.Save(fileName, ImageFormat.Png);
                bm.Dispose();
                return fileName;
            }
            catch (Exception e)
            {
                Logger.Log(e);
                return null;
            }
        }

        internal static void PrepareScreenCapture()
        {
            Common.DoSomethingInUIThread(() =>
            {
                if (!DragDrop.MouseDown && Helper.SendMessageToHelper(0x401, IntPtr.Zero, IntPtr.Zero) > 0)
                {
                    Common.MMSleep(0.2);
                    InputSimulation.SendKey(new KEYBDDATA() { wVk = (int)VK.SNAPSHOT });
                    InputSimulation.SendKey(new KEYBDDATA() { dwFlags = (int)Common.LLKHF.UP, wVk = (int)VK.SNAPSHOT });

                    Logger.LogDebug("PrepareScreenCapture: SNAPSHOT simulated.");

                    _ = NativeMethods.MoveWindow(
                        (IntPtr)NativeMethods.FindWindow(null, Helper.HELPER_FORM_TEXT),
                        MachineStuff.DesktopBounds.Left,
                        MachineStuff.DesktopBounds.Top,
                        MachineStuff.DesktopBounds.Right - MachineStuff.DesktopBounds.Left,
                        MachineStuff.DesktopBounds.Bottom - MachineStuff.DesktopBounds.Top,
                        false);

                    _ = Helper.SendMessageToHelper(0x406, IntPtr.Zero, IntPtr.Zero, false);
                }
                else
                {
                    Logger.Log("PrepareScreenCapture: Validation failed.");
                }
            });
        }

        internal static void OpenImage(string file)
        {
            // We want to run mspaint under the user account who ran explorer.exe (who logged in this current input desktop)

            // ImpersonateLoggedOnUserAndDoSomething(delegate()
            // {
            //    Process.Start("explorer", "\"" + file + "\"");
            // });
            _ = Launch.CreateProcessInInputDesktopSession(
                "\"" + Environment.ExpandEnvironmentVariables(@"%SystemRoot%\System32\Mspaint.exe") +
                "\"",
                "\"" + file + "\"",
                GetInputDesktop(),
                1);

            // CreateNormalIntegrityProcess(Environment.ExpandEnvironmentVariables(@"%SystemRoot%\System32\Mspaint.exe") +
            // " \"" + file + "\"");

            // We don't want to run mspaint as local system account
            /*
            ProcessStartInfo s = new ProcessStartInfo(
                Environment.ExpandEnvironmentVariables(@"%SystemRoot%\System32\Mspaint.exe"),
                "\"" + file + "\"");
            s.WindowStyle = ProcessWindowStyle.Maximized;
            Process.Start(s);
             * */
        }

        internal static void SendImage(string machine, string file)
        {
            LastDragDropFile = file;

            // Send ClipboardCapture
            if (machine.Equals("All", StringComparison.OrdinalIgnoreCase))
            {
                SendPackage(ID.ALL, PackageType.ClipboardCapture);
            }
            else
            {
                ID id = MachineStuff.MachinePool.ResolveID(machine);
                if (id != ID.NONE)
                {
                    SendPackage(id, PackageType.ClipboardCapture);
                }
            }
        }

        internal static void SendImage(ID src, string file)
        {
            LastDragDropFile = file;

            // Send ClipboardCapture
            SendPackage(src, PackageType.ClipboardCapture);
        }

        internal static void ShowToolTip(string tip, int timeOutInMilliseconds = 5000, ToolTipIcon icon = ToolTipIcon.Info, bool showBalloonTip = true, bool forceEvenIfHidingOldUI = false)
        {
            if (!Common.RunOnLogonDesktop && !Common.RunOnScrSaverDesktop)
            {
                DoSomethingInUIThread(() =>
                {
                    if (Setting.Values.FirstRun)
                    {
                        MachineStuff.Settings?.ShowTip(icon, tip, timeOutInMilliseconds);
                    }

                    Common.MatrixForm?.ShowTip(icon, tip, timeOutInMilliseconds);

                    if (showBalloonTip)
                    {
                        if (MainForm != null)
                        {
                            MainForm.ShowToolTip(tip, timeOutInMilliseconds, forceEvenIfHidingOldUI: forceEvenIfHidingOldUI);
                        }
                        else
                        {
                            Logger.Log(tip);
                        }
                    }
                });
            }
        }

        private static FrmMessage topMostMessageForm;

        internal static void ToggleShowTopMostMessage(string text, string bigText, int timeOut)
        {
            DoSomethingInUIThread(() =>
            {
                if (topMostMessageForm == null)
                {
                    topMostMessageForm = new FrmMessage(text, bigText, timeOut);
                    topMostMessageForm.Show();
                }
                else
                {
                    FrmMessage currentMessageForm = topMostMessageForm;
                    topMostMessageForm = null;
                    currentMessageForm.Close();
                }
            });
        }

        internal static void HideTopMostMessage()
        {
            DoSomethingInUIThread(() =>
            {
                topMostMessageForm?.Close();
            });
        }

        internal static void NullTopMostMessage()
        {
            DoSomethingInUIThread(() =>
            {
                if (topMostMessageForm != null)
                {
                    topMostMessageForm = null;
                }
            });
        }

        internal static bool IsTopMostMessageNotNull()
        {
            return topMostMessageForm != null;
        }

        private static bool TestSend(TcpSk t)
        {
            ID remoteMachineID;

            if (t.Status == SocketStatus.Connected)
            {
                try
                {
                    DATA package = new();
                    package.Type = PackageType.Hi;
                    package.Des = remoteMachineID = (ID)t.MachineId;
                    package.MachineName = MachineName;

                    _ = Sk.TcpSend(t, package);
                    t.EncryptedStream?.Flush();

                    return true;
                }
                catch (ExpectedSocketException)
                {
                    t.BackingSocket = null; // To be removed at CloseAnUnusedSocket()
                }
            }

            t.Status = SocketStatus.SendError;
            return false;
        }

        internal static bool IsConnectedTo(ID remoteMachineID)
        {
            bool updateClientSockets = false;

            if (remoteMachineID == MachineID)
            {
                return true;
            }

            SocketStuff sk = Common.Sk;

            if (sk != null)
            {
                lock (sk.TcpSocketsLock)
                {
                    if (sk.TcpSockets != null)
                    {
                        foreach (TcpSk t in sk.TcpSockets)
                        {
                            if (t.Status == SocketStatus.Connected && (uint)remoteMachineID == t.MachineId)
                            {
                                if (TestSend(t))
                                {
                                    return true;
                                }
                                else
                                {
                                    updateClientSockets = true;
                                }
                            }
                        }
                    }
                }
            }

            if (updateClientSockets)
            {
                MachineStuff.UpdateClientSockets(nameof(IsConnectedTo));
            }

            return false;
        }

#if DEBUG
        private static long minSendTime = long.MaxValue;
        private static long avgSendTime;
        private static long maxSendTime;
        private static long totalSendCount;
        private static long totalSendTime;
#endif

        internal static void SkSend(DATA data, uint? exceptDes, bool includeHandShakingSockets)
        {
            bool connected = false;

            SocketStuff sk = Sk;

            if (sk != null)
            {
#if DEBUG
                long startStop = DateTime.Now.Ticks;
                totalSendCount++;
#endif

                try
                {
                    data.Id = Interlocked.Increment(ref PackageID);

                    bool updateClientSockets = false;

                    lock (sk.TcpSocketsLock)
                    {
                        foreach (TcpSk t in sk.TcpSockets)
                        {
                            if (t != null && t.BackingSocket != null && (t.Status == SocketStatus.Connected || (t.Status == SocketStatus.Handshaking && includeHandShakingSockets)))
                            {
                                if (t.MachineId == (uint)data.Des || (data.Des == ID.ALL && t.MachineId != exceptDes && MachineStuff.InMachineMatrix(t.MachineName)))
                                {
                                    try
                                    {
                                        sk.TcpSend(t, data);

                                        if (data.Des != ID.ALL)
                                        {
                                            connected = true;
                                        }
                                    }
                                    catch (ExpectedSocketException)
                                    {
                                        t.BackingSocket = null; // To be removed at CloseAnUnusedSocket()
                                        updateClientSockets = true;
                                    }
                                    catch (Exception e)
                                    {
                                        Logger.Log(e);
                                        t.BackingSocket = null; // To be removed at CloseAnUnusedSocket()
                                        updateClientSockets = true;
                                    }
                                }
                            }
                        }
                    }

                    if (!connected && data.Des != ID.ALL)
                    {
                        Logger.LogDebug("********** No active connection found for the remote machine! **********" + data.Des.ToString());

                        if (data.Des == ID.NONE || MachineStuff.RemoveDeadMachines(data.Des))
                        {
                            // SwitchToMachine(MachineName.Trim());
                            MachineStuff.NewDesMachineID = DesMachineID = MachineID;
                            MachineStuff.SwitchLocation.X = Event.XY_BY_PIXEL + Event.myLastX;
                            MachineStuff.SwitchLocation.Y = Event.XY_BY_PIXEL + Event.myLastY;
                            MachineStuff.SwitchLocation.ResetCount();
                            EvSwitch.Set();
                        }
                    }

                    if (updateClientSockets)
                    {
                        MachineStuff.UpdateClientSockets("SkSend");
                    }
                }
                catch (Exception e)
                {
                    Logger.Log(e);
                }

#if DEBUG
                startStop = DateTime.Now.Ticks - startStop;
                totalSendTime += startStop;
                if (startStop < minSendTime)
                {
                    minSendTime = startStop;
                }

                if (startStop > maxSendTime)
                {
                    maxSendTime = startStop;
                }

                avgSendTime = totalSendTime / totalSendCount;
#endif
            }
            else
            {
                PackageSent.Nil++;
            }
        }

        internal static void CloseAnUnusedSocket()
        {
            SocketStuff sk = Common.Sk;

            if (sk != null)
            {
                lock (sk.TcpSocketsLock)
                {
                    if (sk.TcpSockets != null)
                    {
                        TcpSk tobeRemoved = null;

                        foreach (TcpSk t in sk.TcpSockets)
                        {
                            if ((t.Status != SocketStatus.Connected && t.BirthTime < GetTick() - SocketStuff.CONNECT_TIMEOUT) || t.BackingSocket == null)
                            {
                                Logger.LogDebug("CloseAnUnusedSocket: " + t.MachineName + ":" + t.MachineId + "|" + t.Status.ToString());
                                tobeRemoved = t;

                                if (t.BackingSocket != null)
                                {
                                    try
                                    {
                                        t.BackingSocket.Close();
                                    }
                                    catch (Exception e)
                                    {
                                        Logger.Log(e);
                                    }
                                }

                                break; // Each time we try to remove one socket only.
                            }
                        }

                        if (tobeRemoved != null)
                        {
                            _ = sk.TcpSockets.Remove(tobeRemoved);
                        }
                    }
                }
            }
        }

        internal static bool AtLeastOneSocketConnected()
        {
            SocketStuff sk = Common.Sk;

            if (sk != null)
            {
                lock (sk.TcpSocketsLock)
                {
                    if (sk.TcpSockets != null)
                    {
                        foreach (TcpSk t in sk.TcpSockets)
                        {
                            if (t.Status == SocketStatus.Connected)
                            {
                                Logger.LogDebug("AtLeastOneSocketConnected returning true: " + t.MachineName);
                                return true;
                            }
                        }
                    }
                }
            }

            Logger.LogDebug("AtLeastOneSocketConnected returning false.");
            return false;
        }

        internal static Socket AtLeastOneServerSocketConnected()
        {
            SocketStuff sk = Common.Sk;

            if (sk != null)
            {
                lock (sk.TcpSocketsLock)
                {
                    if (sk.TcpSockets != null)
                    {
                        foreach (TcpSk t in sk.TcpSockets)
                        {
                            if (!t.IsClient && t.Status == SocketStatus.Connected)
                            {
                                Logger.LogDebug("AtLeastOneServerSocketConnected returning true: " + t.MachineName);
                                return t.BackingSocket;
                            }
                        }
                    }
                }
            }

            Logger.LogDebug("AtLeastOneServerSocketConnected returning false.");
            return null;
        }

        internal static TcpSk GetConnectedClientSocket()
        {
            SocketStuff sk = Common.Sk;

            if (sk != null)
            {
                lock (sk.TcpSocketsLock)
                {
                    return sk.TcpSockets?.FirstOrDefault(item => item.IsClient && item.Status == SocketStatus.Connected);
                }
            }
            else
            {
                return null;
            }
        }

        internal static bool AtLeastOneSocketEstablished()
        {
            SocketStuff sk = Common.Sk;

            if (sk != null)
            {
                lock (sk.TcpSocketsLock)
                {
                    if (sk.TcpSockets != null)
                    {
                        foreach (TcpSk t in sk.TcpSockets)
                        {
                            if (t.BackingSocket != null && t.BackingSocket.Connected)
                            {
                                if (TestSend(t))
                                {
                                    Logger.LogDebug($"{nameof(AtLeastOneSocketEstablished)} returning true: {t.MachineName}");
                                    return true;
                                }
                            }
                        }
                    }
                }
            }

            Logger.LogDebug($"{nameof(AtLeastOneSocketEstablished)} returning false.");
            return false;
        }

        internal static bool IsConnectedByAClientSocketTo(string machineName)
        {
            SocketStuff sk = Common.Sk;

            if (sk != null)
            {
                lock (sk.TcpSocketsLock)
                {
                    foreach (TcpSk t in sk.TcpSockets)
                    {
                        if (t != null && t.IsClient && t.Status == SocketStatus.Connected
                            && t.BackingSocket != null && t.MachineName.Equals(machineName, StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        internal static IPAddress GetConnectedClientSocketIPAddressFor(string machineName)
        {
            SocketStuff sk = Common.Sk;

            if (sk != null)
            {
                lock (sk.TcpSocketsLock)
                {
                    return sk.TcpSockets.FirstOrDefault(t => t != null && t.IsClient && t.Status == SocketStatus.Connected
                            && t.Address != null && t.MachineName.Equals(machineName, StringComparison.OrdinalIgnoreCase))
                            ?.Address;
                }
            }

            return null;
        }

        internal static bool IsConnectingByAClientSocketTo(string machineName, IPAddress ip)
        {
            SocketStuff sk = Common.Sk;

            if (sk != null)
            {
                lock (sk.TcpSocketsLock)
                {
                    foreach (TcpSk t in sk.TcpSockets)
                    {
                        if (t != null && t.IsClient && t.Status == SocketStatus.Connecting
                            && t.BackingSocket != null && t.MachineName.Equals(machineName, StringComparison.OrdinalIgnoreCase)
                            && t.Address.ToString().Equals(ip.ToString(), StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        internal static void UpdateSetupMachineMatrix(string desMachine)
        {
            int machineCt = 0;

            foreach (string m in MachineStuff.MachineMatrix)
            {
                if (!string.IsNullOrEmpty(m.Trim()))
                {
                    machineCt++;
                }
            }

            if (machineCt < 2 && MachineStuff.Settings != null && (MachineStuff.Settings.GetCurrentPage() is SetupPage1 || MachineStuff.Settings.GetCurrentPage() is SetupPage2b))
            {
                MachineStuff.MachineMatrix = new string[MachineStuff.MAX_MACHINE] { Common.MachineName.Trim(), desMachine, string.Empty, string.Empty };
                Logger.LogDebug("UpdateSetupMachineMatrix: " + string.Join(",", MachineStuff.MachineMatrix));

                Common.DoSomethingInUIThread(
                    () =>
                    {
                        MachineStuff.Settings.SetControlPage(new SetupPage4());
                    },
                    true);
            }
        }

        internal static void ReopenSockets(bool byUser)
        {
            DoSomethingInUIThread(
                () =>
            {
                try
                {
                    SocketStuff tmpSk = Sk;

                    if (tmpSk != null)
                    {
                        Sk = null; // TODO: This looks redundant.
                        tmpSk.Close(byUser);
                    }

                    Sk = new SocketStuff(tcpPort, byUser);
                }
                catch (Exception e)
                {
                    Sk = null;
                    Logger.Log(e);
                }

                if (Sk != null)
                {
                    if (byUser)
                    {
                        SocketStuff.ClearBadIPs();
                    }

                    MachineStuff.UpdateClientSockets("ReopenSockets");
                }
            },
                true);

            if (Sk == null)
            {
                return;
            }

            Common.DoSomethingInTheInputCallbackThread(() =>
            {
                if (Common.Hook != null)
                {
                    Common.Hook.Stop();
                    Common.Hook = null;
                }

                if (byUser)
                {
                    Common.InputCallbackForm.Close();
                    Common.InputCallbackForm = null;
                    Program.StartInputCallbackThread();
                }
                else
                {
                    Common.InputCallbackForm.InstallKeyboardAndMouseHook();
                }
            });
        }

        private static string GetMyStorageDir()
        {
            string st = string.Empty;

            try
            {
                if (RunOnLogonDesktop || RunOnScrSaverDesktop)
                {
                    st = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                    if (!Directory.Exists(st))
                    {
                        _ = Directory.CreateDirectory(st);
                    }

                    st += @"\" + Common.BinaryName;
                    if (!Directory.Exists(st))
                    {
                        _ = Directory.CreateDirectory(st);
                    }

                    st += @"\ScreenCaptures\";
                    if (!Directory.Exists(st))
                    {
                        _ = Directory.CreateDirectory(st);
                    }
                }
                else
                {
                    _ = Launch.ImpersonateLoggedOnUserAndDoSomething(() =>
                    {
                        st = Environment.GetFolderPath(Environment.SpecialFolder.Desktop) + @"\" + Common.BinaryName;
                        if (!Directory.Exists(st))
                        {
                            _ = Directory.CreateDirectory(st);
                        }

                        st += @"\ScreenCaptures\";
                        if (!Directory.Exists(st))
                        {
                            _ = Directory.CreateDirectory(st);
                        }
                    });
                }

                Logger.LogDebug("GetMyStorageDir: " + st);

                // Delete old files.
                foreach (FileInfo fi in new DirectoryInfo(st).GetFiles())
                {
                    if (fi.CreationTime.AddDays(1) < DateTime.Now)
                    {
                        fi.Delete();
                    }
                }

                return st;
            }
            catch (Exception e)
            {
                Logger.Log(e);

                if (string.IsNullOrEmpty(st) || !st.Contains(Common.BinaryName))
                {
                    st = Path.GetTempPath();
                }

                return st;
            }
        }

        internal static void GetMachineName()
        {
            string machine_Name = string.Empty;

            try
            {
                machine_Name = Dns.GetHostName();
                Logger.LogDebug("GetHostName = " + machine_Name);
            }
            catch (Exception e)
            {
                Logger.Log(e);

                if (string.IsNullOrEmpty(machine_Name))
                {
                    machine_Name = "RANDOM" + Ran.Next().ToString(CultureInfo.CurrentCulture);
                }
            }

            if (machine_Name.Length > 32)
            {
                machine_Name = machine_Name[..32];
            }

            Common.MachineName = machine_Name.Trim();

            Logger.LogDebug($"========== {nameof(GetMachineName)} ended!");
        }

        private static string GetNetworkName(NetworkInterface networkInterface)
        {
            return $"{networkInterface.Name} | {networkInterface.Description.Replace(":", "-")}";
        }

        internal static string GetRemoteStringIP(Socket s, bool throwException = false)
        {
            if (s == null)
            {
                return string.Empty;
            }

            string ip;

            try
            {
                ip = (s?.RemoteEndPoint as IPEndPoint)?.Address?.ToString();

                if (string.IsNullOrEmpty(ip))
                {
                    return string.Empty;
                }
            }
            catch (ObjectDisposedException e)
            {
                Logger.Log($"{nameof(GetRemoteStringIP)}: The socket could have been disposed by other threads, error: {e.Message}");

                if (throwException)
                {
                    throw;
                }

                return string.Empty;
            }
            catch (SocketException e)
            {
                Logger.Log($"{nameof(GetRemoteStringIP)}: {e.Message}");

                if (throwException)
                {
                    throw;
                }

                return string.Empty;
            }

            return ip;
        }

        internal static void CloseAllFormsAndHooks()
        {
            if (Hook != null)
            {
                Hook.Stop();
                Hook = null;
                if (InputCallbackForm != null)
                {
                    DoSomethingInTheInputCallbackThread(() =>
                    {
                        InputCallbackForm.Close();
                        InputCallbackForm = null;
                    });
                }
            }

            if (MainForm != null)
            {
                MainForm.Destroy();
                MainForm = null;
            }

            if (MatrixForm != null)
            {
                MatrixForm.Close();
                MatrixForm = null;
            }

            if (AboutForm != null)
            {
                AboutForm.Close();
                AboutForm = null;
            }
        }

        internal static void MoveMouseToCenter()
        {
            Logger.LogDebug("+++++ MoveMouseToCenter");
            InputSimulation.MoveMouse(
                MachineStuff.PrimaryScreenBounds.Left + ((MachineStuff.PrimaryScreenBounds.Right - MachineStuff.PrimaryScreenBounds.Left) / 2),
                MachineStuff.PrimaryScreenBounds.Top + ((MachineStuff.PrimaryScreenBounds.Bottom - MachineStuff.PrimaryScreenBounds.Top) / 2));
        }

        internal static void HideMouseCursor(bool byHideMouseMessage)
        {
            Common.LastPos = new Point(
                MachineStuff.PrimaryScreenBounds.Left + ((MachineStuff.PrimaryScreenBounds.Right - MachineStuff.PrimaryScreenBounds.Left) / 2),
                Setting.Values.HideMouse ? 4 : MachineStuff.PrimaryScreenBounds.Top + ((MachineStuff.PrimaryScreenBounds.Bottom - MachineStuff.PrimaryScreenBounds.Top) / 2));

            if ((MachineStuff.desMachineID != MachineID && MachineStuff.desMachineID != ID.ALL) || byHideMouseMessage)
            {
                _ = NativeMethods.SetCursorPos(Common.LastPos.X, Common.LastPos.Y);
                _ = NativeMethods.GetCursorPos(ref Common.lastPos);
                Logger.LogDebug($"+++++ HideMouseCursor, byHideMouseMessage = {byHideMouseMessage}");
            }

            CustomCursor.ShowFakeMouseCursor(int.MinValue, int.MinValue);
        }

        internal static string GetText(IntPtr hWnd)
        {
            int length = NativeMethods.GetWindowTextLength(hWnd);
            StringBuilder sb = new(length + 1);
            int rv = NativeMethods.GetWindowText(hWnd, sb, sb.Capacity);
            Logger.LogDebug("GetWindowText returned " + rv.ToString(CultureInfo.CurrentCulture));
            return sb.ToString();
        }

        public static string GetWindowClassName(IntPtr hWnd)
        {
            StringBuilder buffer = new(128);
            _ = NativeMethods.GetClassName(hWnd, buffer, buffer.Capacity);
            return buffer.ToString();
        }

        internal static void MMSleep(double secs)
        {
            for (int i = 0; i < secs * 10; i++)
            {
                Application.DoEvents();
                Thread.Sleep(100);
            }
        }

        internal static void UpdateMultipleModeIconAndMenu()
        {
            MainForm?.UpdateMultipleModeIconAndMenu();
        }

        internal static void SendOrReceiveARandomDataBlockPerInitialIV(Stream st, bool send = true)
        {
            byte[] ranData = new byte[SymAlBlockSize];

            try
            {
                if (send)
                {
                    ranData = RandomNumberGenerator.GetBytes(SymAlBlockSize);
                    st.Write(ranData, 0, ranData.Length);
                }
                else
                {
                    int toRead = ranData.Length;
                    int read = st.ReadEx(ranData, 0, toRead);

                    if (read != toRead)
                    {
                        Logger.LogDebug("Stream has no more data after reading {0} bytes.", read);
                    }
                }
            }
            catch (IOException e)
            {
                string log = $"{nameof(SendOrReceiveARandomDataBlockPerInitialIV)}: Exception {(send ? "writing" : "reading")} to the socket stream: {e.InnerException?.GetType()}/{e.Message}. (This is expected when the remote machine closes the connection during desktop switch or reconnection.)";
                Logger.Log(log);

                if (e.InnerException is not (SocketException or ObjectDisposedException))
                {
                    throw;
                }
            }
        }
    }
}
