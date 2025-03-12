// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows.Forms;

using MouseWithoutBorders.Class;
using MouseWithoutBorders.Exceptions;

// <summary>
//     Logging.
// </summary>
// <history>
//     2008 created by Truong Do (ductdo).
//     2009-... modified by Truong Do (TruongDo).
//     2023- Included in PowerToys.
// </history>
namespace MouseWithoutBorders.Core;

internal static class Logger
{
    internal static readonly string[] AllLogs = new string[MAX_LOG];
    private static readonly Lock AllLogsLock = new();
    internal static readonly ConcurrentDictionary<string, int> LogCounter = new();
    private const int MAX_LOG = 10000;
    private static int allLogsIndex;

    private const int MaxLogExceptionPerHour = 1000;
    private static int lastHour;
    private static int exceptionCount;

    internal static void TelemetryLogTrace(string log, SeverityLevel severityLevel, bool flush = false)
    {
        int logCount = LogCounter.AddOrUpdate(log, 1, (key, value) => value + 1);
        Logger.Log(log);
    }

    internal static void Log(Exception e, [System.Runtime.CompilerServices.CallerMemberName] string memberName = "", [System.Runtime.CompilerServices.CallerFilePath] string sourceFilePath = "", [System.Runtime.CompilerServices.CallerLineNumber] int sourceLineNumber = 0)
    {
        if (e is not KnownException)
        {
            string exText = e.ToString();

            Log($"!Exception!: {exText}", memberName, sourceFilePath, sourceLineNumber);

            if (DateTime.UtcNow.Hour != lastHour)
            {
                lastHour = DateTime.UtcNow.Hour;
                exceptionCount = 0;
            }

            if (exceptionCount < MaxLogExceptionPerHour)
            {
                exceptionCount++;
            }
            else if (exceptionCount != short.MaxValue)
            {
                exceptionCount = short.MaxValue;
            }
        }
    }

    private const string HeaderSENT =
        "Be{0},Ke{1},Mo{2},He{3},Mx{4},Tx{5},Im{6},By{7},Cl{8},Dr{9},De{10},Ed{11},Ie{12},Ni{13}";

    private const string HeaderRECEIVED =
        "Be{0},Ke{1},Mo{2},He{3},Mx{4},Tx{5},Im{6},By{7},Cl{8},Dr{9},De{10},Ed{11},In{12},Ni{13},Pc{14}/{15}";

    internal static void LogDebug(string log, bool clearLog = false, [System.Runtime.CompilerServices.CallerMemberName] string memberName = "", [System.Runtime.CompilerServices.CallerFilePath] string sourceFilePath = "", [System.Runtime.CompilerServices.CallerLineNumber] int sourceLineNumber = 0)
    {
#if DEBUG
        Log(log, clearLog, memberName, sourceFilePath, sourceLineNumber);
#endif
    }

    internal static void Log(string log, bool clearLog = false, [System.Runtime.CompilerServices.CallerMemberName] string memberName = "", [System.Runtime.CompilerServices.CallerFilePath] string sourceFilePath = "", [System.Runtime.CompilerServices.CallerLineNumber] int sourceLineNumber = 0)
    {
        log = DateTime.Now.ToString("MM/dd HH:mm:ss.fff", CultureInfo.InvariantCulture) + $"({Thread.CurrentThread.ManagedThreadId})" + log;

        ManagedCommon.Logger.LogInfo(log, memberName, sourceFilePath, sourceLineNumber);
        lock (AllLogsLock)
        {
            if (clearLog)
            {
                allLogsIndex = 0;
            }

            AllLogs[allLogsIndex] = log;
            allLogsIndex = (allLogsIndex + 1) % MAX_LOG;
        }
    }

    internal static void LogDebug(string format, params object[] args)
    {
#if DEBUG
        Logger.Log(format, args);
#endif
    }

    private static void Log(string format, params object[] args)
    {
        Logger.Log(string.Format(CultureInfo.InvariantCulture, format, args));
    }

    private static PackageMonitor lastPackageSent;
    private static PackageMonitor lastPackageReceived;

    [Conditional("DEBUG")]
    internal static void LogAll()
    {
        string log;

        if (!lastPackageSent.Equals(Common.PackageSent))
        {
            log = string.Format(
                CultureInfo.CurrentCulture,
                "SENT:" + HeaderSENT,
                Common.PackageSent.Heartbeat,
                Common.PackageSent.Keyboard,
                Common.PackageSent.Mouse,
                Common.PackageSent.Hello,
                Common.PackageSent.Matrix,
                Common.PackageSent.ClipboardText,
                Common.PackageSent.ClipboardImage,
                Common.PackageSent.ByeBye,
                Common.PackageSent.Clipboard,
                Common.PackageSent.ClipboardDragDrop,
                Common.PackageSent.ClipboardDragDropEnd,
                Common.PackageSent.ExplorerDragDrop,
                Common.inputEventCount,
                Common.PackageSent.Nil);
            Log(log);
            lastPackageSent = Common.PackageSent; // Copy data
        }

        if (!lastPackageReceived.Equals(Common.PackageReceived))
        {
            log = string.Format(
                CultureInfo.CurrentCulture,
                "RECEIVED:" + HeaderRECEIVED,
                Common.PackageReceived.Heartbeat,
                Common.PackageReceived.Keyboard,
                Common.PackageReceived.Mouse,
                Common.PackageReceived.Hello,
                Common.PackageReceived.Matrix,
                Common.PackageReceived.ClipboardText,
                Common.PackageReceived.ClipboardImage,
                Common.PackageReceived.ByeBye,
                Common.PackageReceived.Clipboard,
                Common.PackageReceived.ClipboardDragDrop,
                Common.PackageReceived.ClipboardDragDropEnd,
                Common.PackageReceived.ExplorerDragDrop,
                Common.invalidPackageCount,
                Common.PackageReceived.Nil,
                Receiver.processedPackageCount,
                Receiver.skippedPackageCount);
            Log(log);
            lastPackageReceived = Common.PackageReceived;
        }
    }

    internal static void GenerateLog()
    {
        int l = Setting.Values.DumpObjectsLevel;
        if (l is > 0 and < 10)
        {
            Logger.DumpObjects(l);
        }
    }

    private static List<ProcessThread> myThreads;

    internal static void DumpObjects(int level)
    {
        try
        {
            string logFile = Path.Combine(Common.RunWithNoAdminRight ? Path.GetTempPath() : Path.GetDirectoryName(Application.ExecutablePath), "MagicMouse.log");

            StringBuilder sb = new(1000000);
            string log;

            myThreads = new List<ProcessThread>();

            foreach (ProcessThread t in Process.GetCurrentProcess().Threads)
            {
                myThreads.Add(t);
            }

            _ = Logger.PrivateDump(sb, AllLogs, "[Program logs]\r\n===============\r\n", 0, level, false);
            _ = Logger.PrivateDump(sb, new Common(), "[Other Logs]\r\n===============\r\n", 0, level, false);
            Logger.DumpType(sb, typeof(Logger), 0, level);
            sb.AppendLine("[DragDrop]\r\n===============");
            Logger.DumpType(sb, typeof(DragDrop), 0, level);
            sb.AppendLine("[MachineStuff]\r\n===============");
            Logger.DumpType(sb, typeof(MachineStuff), 0, level);
            sb.AppendLine("[Receiver]\r\n===============");
            Logger.DumpType(sb, typeof(Receiver), 0, level);

            log = string.Format(
                CultureInfo.CurrentCulture,
                "{0} {1}\r\n{2}\r\n\r\n{3}",
                Application.ProductName,
                Application.ProductVersion,
                "Private Mem: " + (Process.GetCurrentProcess().PrivateMemorySize64 / 1024).ToString(CultureInfo.CurrentCulture) + "KB",
                sb.ToString());

            if (!string.IsNullOrEmpty(Common.myKey))
            {
                log = log.Replace(Common.MyKey, Common.GetDebugInfo(Common.MyKey));
            }

            log += Thread.DumpThreadsStack();
            log += $"\r\nCurrent process session: {Process.GetCurrentProcess().SessionId}, active console session: {NativeMethods.WTSGetActiveConsoleSessionId()}.";

            File.WriteAllText(logFile, log);

            if (Common.RunOnLogonDesktop || Common.RunOnScrSaverDesktop)
            {
                _ = MessageBox.Show("Dump file created: " + logFile, Application.ProductName);
            }
            else
            {
                Common.ShowToolTip("Dump file created: " + logFile + " and placed in the Clipboard.", 10000);
                Clipboard.SetText(logFile);
            }
        }
        catch (Exception e)
        {
            _ = MessageBox.Show(e.Message + "\r\n" + e.StackTrace, Application.ProductName);
        }
    }

    internal static bool PrivateDump(StringBuilder sb, object obj, string objName, int level, int maxLevel, bool stop)
    {
        Type t;
        string padStr = string.Empty;
        string[] strArr;
        string objString;

        if (obj == null || (maxLevel >= 0 && level >= maxLevel) || obj is Cursor)
        {
            return false;
        }

        for (int i = 0; i < level; i++)
        {
            padStr += i < level - 1 ? "-" : padStr += string.Empty;
        }

        objString = obj.ToString();
        t = obj.GetType();
        strArr = new string[7];
        strArr[0] = padStr;
        strArr[1] = objName;

        // strArr[2] = " ";
        // strArr[3] = t.FullName;
        strArr[4] = " = ";
        strArr[5] = objName.Equals("myKey", StringComparison.OrdinalIgnoreCase)
            ? Common.GetDebugInfo(objString)
            : objName.Equals("lastClipboardObject", StringComparison.OrdinalIgnoreCase)
                ? string.Empty
                : objString
                    .Replace("System.Windows.Forms.", string.Empty)
                    .Replace("System.Net.Sockets.", string.Empty)
                    .Replace("System.Security.Cryptography.", string.Empty)
                    .Replace("System.Threading.", string.Empty)
                    .Replace("System.ComponentModel.", string.Empty)
                    .Replace("System.Runtime.", string.Empty)
                    .Replace("System.Drawing.", string.Empty)
                    .Replace("System.Object", "O")
                    .Replace("System.Diagnostics.", string.Empty)
                    .Replace("System.Collections.", string.Empty)
                    .Replace("System.Drawing.", string.Empty)
                    .Replace("System.Int", string.Empty)
                    .Replace("System.EventHandler.", string.Empty);
        strArr[6] = "\r\n";
        _ = sb.Append(string.Concat(strArr).Replace(Common.BinaryName, "MM"));

        if (stop || t.IsPrimitive)
        {
            return false;
        }

        Logger.DumpObject(sb, obj, level, t, maxLevel);
        return true;
    }

    internal static void DumpObject(StringBuilder sb, object obj, int level, Type t, int maxLevel)
    {
        int i;
        bool stop;
        if (t == typeof(Delegate))
        {
            return;
        }

        FieldInfo[] fi;
        string type;

        if (obj is PackageType or string or AddressFamily or ID or IPAddress)
        {
            return;
        }

        type = obj.GetType().ToString();

        if (type.EndsWith("type", StringComparison.CurrentCultureIgnoreCase) || type.Contains("Cryptography")
            || type.EndsWith("AsyncEventBits", StringComparison.CurrentCultureIgnoreCase))
        {
            return;
        }

        stop = obj == null || obj is DATA || obj.GetType().BaseType == typeof(ValueType)
            || obj.GetType().Namespace.Contains("System.Windows");
        fi = t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);

        foreach (FieldInfo f in fi)
        {
            if (f.GetValue(obj) != AllLogs)
            {
                _ = PrivateDump(sb, f.GetValue(obj), f.Name, level + 1, maxLevel, stop);
            }
        }

        if (obj is Dictionary<string, List<IPAddress>>)
        {
            Dictionary<string, List<IPAddress>> d = obj as Dictionary<string, List<IPAddress>>;

            foreach (string k in d.Keys)
            {
                if (d.TryGetValue(k, out List<IPAddress> l))
                {
                    foreach (IPAddress ip in l)
                    {
                        _ = PrivateDump(sb, ip, "[" + k + "]", level + 1, maxLevel, false);
                    }
                }
            }
        }

        if (obj is Array)
        {
            try
            {
                if (obj is MachineInf[])
                {
                    MachineInf[] os = (MachineInf[])obj;

                    for (i = 0; i < os.GetLength(0); i++)
                    {
                        _ = PrivateDump(sb, os[i], "[" + i + "]", level + 1, maxLevel, false);
                    }
                }
                else if (obj is int[] || obj is uint[])
                {
                    int[] os = (int[])obj;

                    for (i = 0; i < os.GetLength(0); i++)
                    {
                        _ = PrivateDump(sb, os[i], "[" + i + "]", level + 1, maxLevel, false);
                    }
                }
                else if (obj is short[] || obj is ushort[])
                {
                    short[] os = (short[])obj;

                    for (i = 0; i < os.GetLength(0); i++)
                    {
                        _ = PrivateDump(sb, os[i], "[" + i + "]", level + 1, maxLevel, false);
                    }
                }
                else if (obj is TcpClient[] || obj is IPAddress[] || obj is TcpSk[] || obj is string[]
                    || obj is TcpServer[]
                    || obj is ProcessThread[] || obj is Thread[])
                {
                    object[] os = (object[])obj;

                    for (i = 0; i < os.GetLength(0); i++)
                    {
                        _ = PrivateDump(sb, os[i], "[" + i + "]", level + 1, maxLevel, false);
                    }
                }
                else
                {
                    _ = PrivateDump(sb, obj.GetType().ToString() + ": N/A", obj.GetType().ToString(), level + 1, maxLevel, false);
                }
            }
            catch (Exception)
            {
            }
        }
    }

    internal static void DumpType(StringBuilder sb, Type typeToDump, int level, int maxLevel)
    {
        if ((typeToDump == typeof(Delegate))
            || (typeToDump == typeof(PackageType))
            || (typeToDump == typeof(string))
            || (typeToDump == typeof(AddressFamily))
            || (typeToDump == typeof(ID))
            || (typeToDump == typeof(IPAddress)))
        {
            return;
        }

        var typeFullName = typeToDump.ToString();
        if (typeFullName.EndsWith("type", StringComparison.CurrentCultureIgnoreCase)
            || typeFullName.Contains("Cryptography")
            || typeFullName.EndsWith("AsyncEventBits", StringComparison.CurrentCultureIgnoreCase))
        {
            return;
        }

        var stop = (typeToDump == null)
            || (typeToDump == typeof(DATA))
            || (typeToDump.BaseType == typeof(ValueType))
            || typeToDump.Namespace.Contains("System.Windows");

        var fieldInfos = typeToDump.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
        foreach (var fieldInfo in fieldInfos)
        {
            if (fieldInfo.GetValue(null) != AllLogs)
            {
                _ = Logger.PrivateDump(sb, fieldInfo.GetValue(null), fieldInfo.Name, level + 1, maxLevel, stop);
            }
        }
    }

    internal static string GetStackTrace(StackTrace st)
    {
        string rv = string.Empty;

        for (int i = 0; i < st.FrameCount; i++)
        {
            StackFrame sf = st.GetFrame(i);
            rv += sf.GetMethod() + " <= ";
        }

        return rv;
    }
}
