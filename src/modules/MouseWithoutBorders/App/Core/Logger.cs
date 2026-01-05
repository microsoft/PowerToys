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
using System.Runtime.CompilerServices;
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
    // keep a count of unique lines of text that get logged
    private static readonly ConcurrentDictionary<string, int> LogCounter = new();

    // implements a simple ring buffer to store recent log entries in memory
    private const int MAX_LOG = 10000;
    private const int MaxLogExceptionPerHour = 1000;
    private static readonly string[] AllLogs = new string[MAX_LOG];
    private static readonly Lock AllLogsLock = new();
    private static int allLogsIndex;

    // used for throttling the number of exceptions that get logged
    // so that high-volume exceptions don't flood the logs
    private static int lastHour;
    private static int exceptionCount;

    // track some application statistics
    private static PackageMonitor lastPackageSent;
    private static PackageMonitor lastPackageReceived;

    private static List<ProcessThread> myThreads;

    internal static void TelemetryLogTrace(string log, SeverityLevel severityLevel, bool flush = false)
    {
        int logCount = LogCounter.AddOrUpdate(log, 1, (key, value) => value + 1);
        Logger.Log(log);
    }

    private static void Log(string format, params object[] args)
    {
        Logger.Log(string.Format(CultureInfo.InvariantCulture, format, args));
    }

    internal static void Log(string log, bool clearLog = false, [CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "", [CallerLineNumber] int sourceLineNumber = 0)
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

    internal static void Log(Exception e, [CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "", [CallerLineNumber] int sourceLineNumber = 0)
    {
        if (e is not KnownException)
        {
            string exText = e.ToString();

            Logger.Log($"!Exception!: {exText}", memberName, sourceFilePath, sourceLineNumber);

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

    [Conditional("DEBUG")]
    internal static void LogDebug(string format, params object[] args)
    {
        Logger.Log(format, args);
    }

    [Conditional("DEBUG")]
    internal static void LogDebug(string log, bool clearLog = false, [CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "", [CallerLineNumber] int sourceLineNumber = 0)
    {
        Logger.Log(log, clearLog, memberName, sourceFilePath, sourceLineNumber);
    }

    [Conditional("DEBUG")]
    internal static void LogStatistics()
    {
        if (!lastPackageSent.Equals(Package.PackageSent))
        {
            var log =
                $"SENT:" +
                $"Be{Package.PackageSent.Heartbeat}," +
                $"Ke{Package.PackageSent.Keyboard}," +
                $"Mo{Package.PackageSent.Mouse}," +
                $"He{Package.PackageSent.Hello}," +
                $"Mx{Package.PackageSent.Matrix}," +
                $"Tx{Package.PackageSent.ClipboardText}," +
                $"Im{Package.PackageSent.ClipboardImage}," +
                $"By{Package.PackageSent.ByeBye}," +
                $"Cl{Package.PackageSent.Clipboard}," +
                $"Dr{Package.PackageSent.ClipboardDragDrop}," +
                $"De{Package.PackageSent.ClipboardDragDropEnd}," +
                $"Ed{Package.PackageSent.ExplorerDragDrop}," +
                $"Ie{Event.inputEventCount}," +
                $"Ni{Package.PackageSent.Nil}";
            Logger.Log(log);
            lastPackageSent = Package.PackageSent;
        }

        if (!lastPackageReceived.Equals(Package.PackageReceived))
        {
            var log =
                $"RECEIVED:" +
                $"Be{Package.PackageReceived.Heartbeat}," +
                $"Ke{Package.PackageReceived.Keyboard}," +
                $"Mo{Package.PackageReceived.Mouse}," +
                $"He{Package.PackageReceived.Hello}," +
                $"Mx{Package.PackageReceived.Matrix}," +
                $"Tx{Package.PackageReceived.ClipboardText}," +
                $"Im{Package.PackageReceived.ClipboardImage}," +
                $"By{Package.PackageReceived.ByeBye}," +
                $"Cl{Package.PackageReceived.Clipboard}," +
                $"Dr{Package.PackageReceived.ClipboardDragDrop}," +
                $"De{Package.PackageReceived.ClipboardDragDropEnd}," +
                $"Ed{Package.PackageReceived.ExplorerDragDrop}," +
                $"Ie{Event.invalidPackageCount}," +
                $"Ni{Package.PackageReceived.Nil}" +
                $"Pc{Receiver.processedPackageCount}/{Receiver.skippedPackageCount}";
            Logger.Log(log);
            lastPackageReceived = Package.PackageReceived;
        }
    }

    internal static void DumpProgramLogs(StringBuilder sb, int level)
    {
        _ = Logger.PrivateDump(sb, AllLogs, "[Program logs]\r\n===============\r\n", 0, level, false);
    }

    internal static void DumpOtherLogs(StringBuilder sb, int level)
    {
        _ = Logger.PrivateDump(sb, new Common(), "[Other Logs]\r\n===============\r\n", 0, level, false);
    }

    private static string DumpObjects(int level)
    {
        StringBuilder sb = new(1000000);

        Logger.DumpProgramLogs(sb, level);
        Logger.DumpOtherLogs(sb, level);
        Logger.DumpStaticTypes(sb, level);

        string log =
            $"{Application.ProductName} {Application.ProductVersion}\r\n" +
            $"Private Mem: {Process.GetCurrentProcess().PrivateMemorySize64 / 1024}KB\r\n" +
            $"\r\n" +
            $"{sb}\r\n";

        // obfuscate the current encryption key
        if (!string.IsNullOrEmpty(Encryption.myKey))
        {
            log = log.Replace(Encryption.MyKey, Encryption.GetDebugInfo(Encryption.MyKey));
        }

        log += Thread.DumpThreadsStack();
        log += "\r\n";
        log += $"Current process session: {Process.GetCurrentProcess().SessionId}, active console session: {NativeMethods.WTSGetActiveConsoleSessionId()}.";

        return log;
    }

    private static void DumpObject(StringBuilder sb, object obj, int level, Type t, int maxLevel)
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

    private static bool PrivateDump(StringBuilder sb, object obj, string objName, int level, int maxLevel, bool stop)
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
            ? Encryption.GetDebugInfo(objString)
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

    internal static void DumpStaticTypes(StringBuilder sb, int level)
    {
        var staticTypes = new List<Type>
        {
            typeof(Clipboard),
            typeof(DragDrop),
            typeof(Encryption),
            typeof(Event),
            typeof(InitAndCleanup),
            typeof(Helper),
            typeof(Launch),
            typeof(Logger),
            typeof(MachineStuff),
            typeof(Package),
            typeof(Receiver),
            typeof(Service),
            typeof(WinAPI),
            typeof(WM),
        };
        foreach (var staticType in staticTypes)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"[{staticType.Name}]\r\n===============");
            Logger.DumpType(sb, staticType, 0, level);
        }
    }

    private static void DumpType(StringBuilder sb, Type typeToDump, int level, int maxLevel)
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
