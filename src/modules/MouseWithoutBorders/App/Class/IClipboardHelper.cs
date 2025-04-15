// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Pipes;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

using Microsoft.VisualStudio.Threading;
using Newtonsoft.Json;
using StreamJsonRpc;

#if !MM_HELPER
using MouseWithoutBorders.Class;
using MouseWithoutBorders.Core;
#endif

using SystemClipboard = System.Windows.Forms.Clipboard;
#if !MM_HELPER
using Thread = MouseWithoutBorders.Core.Thread;
#endif

namespace MouseWithoutBorders
{
    [JsonObject(MemberSerialization.OptIn)]
    public struct ByteArrayOrString
    {
        private enum ValueType
        {
            ByteArray,
            String,
        }

        [JsonProperty]
        private ValueType _type;

        private byte[] _byteArrayValue;
        private string _stringValue;

        public ByteArrayOrString(byte[] byteArray)
        {
            _type = ValueType.ByteArray;
            _byteArrayValue = byteArray;
            _stringValue = null;
        }

        public ByteArrayOrString(string str)
        {
            _type = ValueType.String;
            _byteArrayValue = null;
            _stringValue = str;
        }

        public bool IsByteArray => _type == ValueType.ByteArray;

        public bool IsString => _type == ValueType.String;

        [JsonProperty("byteArray")]
        public byte[] ByteArray
        {
            get => _byteArrayValue;
            set
            {
                _byteArrayValue = value;
                _type = ValueType.ByteArray;
            }
        }

        [JsonProperty("string")]
        public string TheString
        {
            get => _stringValue;
            set
            {
                _stringValue = value;
                _type = ValueType.String;
            }
        }

        public bool ShouldSerializeByteArray() => IsByteArray;

        public bool ShouldSerializeString() => IsString;

        public byte[] GetByteArray()
        {
            if (IsByteArray)
            {
                return _byteArrayValue;
            }

            throw new InvalidOperationException("The value is not a byte array.");
        }

        public string GetString()
        {
            if (IsString)
            {
                return _stringValue;
            }

            throw new InvalidOperationException("The value is not a string.");
        }

        public static implicit operator ByteArrayOrString(byte[] byteArray) => new ByteArrayOrString(byteArray);

        public static implicit operator ByteArrayOrString(string str) => new ByteArrayOrString(str);
    }

    public interface IClipboardHelper
    {
        void SendLog(string log);

        void SendDragFile(string fileName);

        void SendClipboardData(ByteArrayOrString data, bool isFilePath);
    }

#if !MM_HELPER
    public class ClipboardHelper : IClipboardHelper
    {
        public void SendLog(string log)
        {
            Logger.LogDebug("FROM HELPER: " + log);

            if (!string.IsNullOrEmpty(log))
            {
                if (log.StartsWith("Screen capture ended", StringComparison.InvariantCulture))
                {
                    /* TODO: Telemetry for screen capture. */

                    if (Setting.Values.FirstCtrlShiftS)
                    {
                        Setting.Values.FirstCtrlShiftS = false;
                        Common.ShowToolTip("Selective screen capture has been triggered, you can change the hotkey on the Settings form.", 10000);
                    }
                }
                else if (log.StartsWith("Trace:", StringComparison.InvariantCulture))
                {
                    Logger.TelemetryLogTrace(log, SeverityLevel.Information);
                }
            }
        }

        public void SendDragFile(string fileName)
        {
            DragDrop.DragDropStep05Ex(fileName);
        }

        public void SendClipboardData(ByteArrayOrString data, bool isFilePath)
        {
            _ = Common.CheckClipboardEx(data, isFilePath);
        }
    }
#endif

    internal sealed class IpcChannel<T>
        where T : new()
    {
        public static T StartIpcServer(string pipeName, CancellationToken cancellationToken)
        {
            SecurityIdentifier securityIdentifier = new SecurityIdentifier(
WellKnownSidType.AuthenticatedUserSid, null);

            PipeSecurity pipeSecurity = new PipeSecurity();
            pipeSecurity.AddAccessRule(new PipeAccessRule(
                securityIdentifier,
                PipeAccessRights.ReadWrite | PipeAccessRights.CreateNewInstance,
                AccessControlType.Allow));

            _ = Task.Factory.StartNew(
                async () =>
                {
                    try
                    {
                        while (!cancellationToken.IsCancellationRequested)
                        {
                            using (var serverChannel = NamedPipeServerStreamAcl.Create(pipeName, PipeDirection.InOut, NamedPipeServerStream.MaxAllowedServerInstances, PipeTransmissionMode.Byte, PipeOptions.Asynchronous, 0, 0, pipeSecurity))
                            {
                                await serverChannel.WaitForConnectionAsync();
                                var taskRpc = JsonRpc.Attach(serverChannel, new T());
                                await taskRpc.Completion;
                            }
                        }
                    }
                    catch (OperationCanceledException)
                    {
                    }
                    catch (Exception e)
                    {
#if MM_HELPER
                        _ = e;
#else
                        Logger.Log(e);
#endif
                    }
                },
                cancellationToken,
                TaskCreationOptions.None,
                TaskScheduler.Default);

            return default(T);
        }
    }

    internal sealed class IpcHelper
    {
        private const string ChannelName = "MouseWithoutBorders";
        private const string RemoteObjectName = "ClipboardHelper";

#if !MM_HELPER
        private static void CleanupStream()
        {
            if (_serverTaskCancellationSource != null)
            {
                _serverTaskCancellationSource.Cancel();
                _serverTaskCancellationSource.Dispose();
                _serverTaskCancellationSource = null;
            }
        }

        private static CancellationTokenSource _serverTaskCancellationSource;

        internal static void CreateIpcServer(bool cleanup)
        {
            try
            {
                if (cleanup)
                {
                    CleanupStream();
                    return;
                }

                _serverTaskCancellationSource = new CancellationTokenSource();
                CancellationToken cancellationToken = _serverTaskCancellationSource.Token;

                IpcChannel<ClipboardHelper>.StartIpcServer(ChannelName + "/" + RemoteObjectName, cancellationToken);
                Common.IpcChannelCreated = true;
            }
            catch (Exception e)
            {
                Common.IpcChannelCreated = false;
                Common.ShowToolTip("Error setting up clipboard sharing, clipboard sharing will not work!", 5000, ToolTipIcon.Error);
                Logger.Log(e);
            }
        }
#else
        internal static IClipboardHelper CreateIpcClient()
        {
            try
            {
                var stream = new NamedPipeClientStream(".", ChannelName + "/" + RemoteObjectName, PipeDirection.InOut, PipeOptions.Asynchronous);

                stream.Connect();

                return JsonRpc.Attach<IClipboardHelper>(stream);
            }
            catch (Exception e)
            {
                EventLogger.LogEvent(e.Message, EventLogEntryType.Error);
            }

            return null;
        }
#endif

    }

    internal static class EventLogger
    {
#if MM_HELPER
        private const string EventSourceName = "MouseWithoutBordersHelper";
#else
        private const string EventSourceName = "MouseWithoutBorders";
#endif

        internal static void LogEvent(string message, EventLogEntryType logType = EventLogEntryType.Information)
        {
            try
            {
                if (!EventLog.SourceExists(EventSourceName))
                {
                    EventLog.CreateEventSource(EventSourceName, "Application");
                }

                EventLog.WriteEntry(EventSourceName, message, logType);
            }
            catch (Exception e)
            {
                Debug.WriteLine(message + ": " + e.Message);
            }
        }
    }
#if MM_HELPER

    internal static class ClipboardMMHelper
    {
        internal static IntPtr NextClipboardViewer = IntPtr.Zero;
        private static FormHelper helperForm;
        private static bool addClipboardFormatListenerResult;

        private static void Log(string log)
        {
            helperForm.SendLog(log);
        }

        private static void Log(Exception e)
        {
            Log($"Trace: {e}");
        }

        internal static void HookClipboard(FormHelper f)
        {
            helperForm = f;

            try
            {
                addClipboardFormatListenerResult = NativeMethods.AddClipboardFormatListener(f.Handle);

                int err = addClipboardFormatListenerResult ? 0 : Marshal.GetLastWin32Error();

                if (err != 0)
                {
                    Log($"Trace: {nameof(NativeMethods.AddClipboardFormatListener)}: GetLastError = {err}");
                }
            }
            catch (EntryPointNotFoundException e)
            {
                Log($"{nameof(NativeMethods.AddClipboardFormatListener)} is unavailable in this version of Windows.");
                Log(e);
            }
            catch (Exception e)
            {
                Log(e);
            }

            // Fallback
            if (!addClipboardFormatListenerResult)
            {
                NextClipboardViewer = NativeMethods.SetClipboardViewer(f.Handle);
                int err = NextClipboardViewer == IntPtr.Zero ? Marshal.GetLastWin32Error() : 0;

                if (err != 0)
                {
                    Log($"Trace: {nameof(NativeMethods.SetClipboardViewer)}: GetLastError = {err}");
                }
            }

            Log($"Trace: Clipboard monitor method {(addClipboardFormatListenerResult ? nameof(NativeMethods.AddClipboardFormatListener) : NextClipboardViewer != IntPtr.Zero ? nameof(NativeMethods.SetClipboardViewer) : "(none)")} is used.");
        }

        internal static void UnhookClipboard()
        {
            if (addClipboardFormatListenerResult)
            {
                addClipboardFormatListenerResult = false;
                _ = NativeMethods.RemoveClipboardFormatListener(helperForm.Handle);
            }
            else
            {
                _ = NativeMethods.ChangeClipboardChain(helperForm.Handle, NextClipboardViewer);
                NextClipboardViewer = IntPtr.Zero;
            }
        }

        private static void ReHookClipboard()
        {
            UnhookClipboard();
            HookClipboard(helperForm);
        }

        internal static bool UpdateNextClipboardViewer(Message m)
        {
            if (m.WParam == NextClipboardViewer)
            {
                NextClipboardViewer = m.LParam;
                return true;
            }

            return false;
        }

        internal static void PassMessageToTheNextViewer(Message m)
        {
            if (NextClipboardViewer != IntPtr.Zero && NextClipboardViewer != helperForm.Handle)
            {
                _ = NativeMethods.SendMessage(NextClipboardViewer, m.Msg, m.WParam, m.LParam);
            }
        }

        public static bool ContainsFileDropList()
        {
            bool rv = false;

            try
            {
                rv = Common.Retry(nameof(SystemClipboard.ContainsFileDropList), () => { return SystemClipboard.ContainsFileDropList(); }, (log) => Log(log));
            }
            catch (ExternalException e)
            {
                Log(e);
                ReHookClipboard();
            }
            catch (ThreadStateException e)
            {
                Log(e);
                ReHookClipboard();
            }

            return rv;
        }

        public static bool ContainsImage()
        {
            bool rv = false;

            try
            {
                rv = Common.Retry(nameof(SystemClipboard.ContainsImage), () => { return SystemClipboard.ContainsImage(); }, (log) => Log(log));
            }
            catch (ExternalException e)
            {
                Log(e);
                ReHookClipboard();
            }
            catch (ThreadStateException e)
            {
                Log(e);
                ReHookClipboard();
            }

            return rv;
        }

        public static bool ContainsText()
        {
            bool rv = false;

            try
            {
                rv = Common.Retry(nameof(SystemClipboard.ContainsText), () => { return SystemClipboard.ContainsText(); }, (log) => Log(log));
            }
            catch (ExternalException e)
            {
                Log(e);
                ReHookClipboard();
            }
            catch (ThreadStateException e)
            {
                Log(e);
                ReHookClipboard();
            }

            return rv;
        }

        public static StringCollection GetFileDropList()
        {
            StringCollection rv = null;

            try
            {
                rv = Common.Retry(nameof(SystemClipboard.GetFileDropList), () => { return SystemClipboard.GetFileDropList(); }, (log) => Log(log));
            }
            catch (ExternalException e)
            {
                Log(e);
                ReHookClipboard();
            }
            catch (ThreadStateException e)
            {
                Log(e);
                ReHookClipboard();
            }

            return rv;
        }

        public static Image GetImage()
        {
            Image rv = null;

            try
            {
                rv = Common.Retry(nameof(SystemClipboard.GetImage), () => { return SystemClipboard.GetImage(); }, (log) => Log(log));
            }
            catch (ExternalException e)
            {
                Log(e);
                ReHookClipboard();
            }
            catch (ThreadStateException e)
            {
                Log(e);
                ReHookClipboard();
            }

            return rv;
        }

        public static string GetText(TextDataFormat format)
        {
            string rv = null;

            try
            {
                rv = Common.Retry(nameof(SystemClipboard.GetText), () => { return SystemClipboard.GetText(format); }, (log) => Log(log));
            }
            catch (ExternalException e)
            {
                Log(e);
                ReHookClipboard();
            }
            catch (ThreadStateException e)
            {
                Log(e);
                ReHookClipboard();
            }
            catch (System.ComponentModel.InvalidEnumArgumentException e)
            {
                Log(e);
            }

            return rv;
        }

        public static void SetImage(Image image)
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
                    (log) => Log(log));
            }
            catch (ExternalException e)
            {
                Log(e);
                ReHookClipboard();
            }
            catch (ThreadStateException e)
            {
                Log(e);
                ReHookClipboard();
            }
            catch (ArgumentNullException e)
            {
                Log(e);
            }
        }

        public static void SetText(string text)
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
                    (log) => Log(log));
            }
            catch (ExternalException e)
            {
                Log(e);
                ReHookClipboard();
            }
            catch (ThreadStateException e)
            {
                Log(e);
                ReHookClipboard();
            }
            catch (ArgumentNullException e)
            {
                Log(e);
            }
        }
    }

#endif

    internal sealed class SharedConst
    {
        internal const int QUIT_CMD = 0x409;
    }

    internal sealed partial class Common
    {
        internal static bool IpcChannelCreated { get; set; }

        internal static T Retry<T>(string name, Func<T> func, Action<string> log, Action preRetry = null)
        {
            int count = 0;

            do
            {
                try
                {
                    T rv = func();

                    if (count > 0)
                    {
                        log($"Trace: {name} has been successful after {count} retry.");
                    }

                    return rv;
                }
                catch (Exception)
                {
                    count++;

                    preRetry?.Invoke();

                    if (count > 10)
                    {
                        throw;
                    }

                    Application.DoEvents();
                    Thread.Sleep(200);
                }
            }
            while (true);
        }
    }
}
