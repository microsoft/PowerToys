# scripts/pt-a11y-events.ps1
# Records the UIA events a screen reader reacts to, so "is it announced?" and "where does focus go?"
# become assertable instead of needing a manual Narrator pass:
#   Focus         UIA_AutomationFocusChangedEventId - what Narrator/NVDA read when focus moves
#   Notification  IUIAutomation5 notification events  - what the app asks the screen reader to speak
#
# Usage (dot-source, then call):
#   . .\pt-a11y-events.ps1
#   $trace = Start-PtA11yEventTrace -ProcessName PowerToys.KeyboardManagerEditorUI
#   ... drive the scenario (winapp ui invoke, SendInput, ...) ...
#   $events = Stop-PtA11yEventTrace -Trace $trace -OutFile .\events.json
#   $events | Format-Table T, Kind, Type, Name, Text
#
#   # Keyboard order: press Tab N times and record each focus stop.
#   Invoke-PtA11yTabWalk -ProcessName PowerToys.KeyboardManagerEditorUI -Count 15 -OutFile .\tab.json
#
# Limitations: the recorder only sees events raised in the target process. A missing Notification
# proves nothing was requested; it cannot prove a screen reader would have spoken an existing one
# (screen readers may drop or merge events), so still do a final pass with Narrator or NVDA.
# Notifications are recorded even on a locked or RDP-minimized desktop. Focus events and the Tab
# walk need an unlocked desktop with the target window in the foreground; otherwise expect none.

if (-not ('PtA11yEvents.Recorder' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace PtA11yEvents {
    [ComImport, Guid("d22108aa-8ac5-49a5-837b-37bbb3d7591e"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IElement {
        void _0(); void _1(); void _2(); void _3(); void _4(); void _5(); void _6();
        [return: MarshalAs(UnmanagedType.Struct)] object GetCurrentPropertyValue(int propertyId);
    }

    [ComVisible(true), Guid("c270f6b5-5c69-4290-9745-7a7f97169468"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IFocusChangedHandler {
        void HandleFocusChangedEvent(IElement sender);
    }

    [ComVisible(true), Guid("c7cb2637-e6c2-4d0c-85de-4948c02175c7"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface INotificationHandler {
        void HandleNotificationEvent(IElement sender, int kind, int processing,
            [MarshalAs(UnmanagedType.BStr)] string displayString, [MarshalAs(UnmanagedType.BStr)] string activityId);
    }

    // IUIAutomation5: methods are declared in vtable order; unused slots are placeholders (_N).
    [ComImport, Guid("25f700c8-d816-4057-a9dc-3cbdee77e256"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IAutomation5 {
        void _0(); void _1();
        IElement GetRootElement();
        void _3(); void _4();
        IElement GetFocusedElement();
        void _6(); void _7(); void _8(); void _9(); void _10(); void _11(); void _12(); void _13(); void _14(); void _15();
        void _16(); void _17(); void _18(); void _19(); void _20(); void _21(); void _22(); void _23(); void _24(); void _25();
        void _26(); void _27(); void _28(); void _29(); void _30(); void _31(); void _32(); void _33(); void _34(); void _35();
        void AddFocusChangedEventHandler(IntPtr cacheRequest, IFocusChangedHandler handler);
        void _37();
        void RemoveAllEventHandlers();
        void _39(); void _40(); void _41(); void _42(); void _43(); void _44(); void _45(); void _46(); void _47(); void _48();
        void _49(); void _50(); void _51(); void _52(); void _53(); void _54(); void _55(); void _56(); void _57(); void _58();
        void _59(); void _60(); void _61(); void _62(); void _63(); void _64();
        void AddNotificationEventHandler(IElement element, int scope, IntPtr cacheRequest, INotificationHandler handler);
    }

    [ComImport, Guid("e22ad333-b25f-460c-83d0-0581107395c9")]
    public class CUIAutomation8 {}

    public sealed class EventRecord {
        public long T;
        public string Kind;
        public string Type;
        public string Name;
        public string AutomationId;
        public string Text;
        public string NotificationKind;
        public string Processing;
        public string ActivityId;
    }

    public sealed class Recorder : IFocusChangedHandler, INotificationHandler {
        static readonly string[] Kinds = { "ItemAdded", "ItemRemoved", "ActionCompleted", "ActionAborted", "Other" };
        static readonly string[] Processings = { "ImportantAll", "ImportantMostRecent", "All", "MostRecent", "CurrentThenMostRecent" };

        readonly HashSet<int> _pids;
        readonly ConcurrentQueue<EventRecord> _events = new ConcurrentQueue<EventRecord>();
        readonly Stopwatch _clock = new Stopwatch();
        readonly ManualResetEventSlim _ready = new ManualResetEventSlim();
        readonly ManualResetEventSlim _stop = new ManualResetEventSlim();
        Thread _thread;
        Exception _startError;

        public Recorder(int[] pids) { _pids = new HashSet<int>(pids); }

        // Events received before the process filter; non-zero with an empty result means a PID mismatch.
        public int RawEventCount;

        public void Start() {
            // UIA clients must not register handlers from an STA UI thread; use a dedicated MTA thread.
            _thread = new Thread(Run) { IsBackground = true };
            _thread.SetApartmentState(ApartmentState.MTA);
            _thread.Start();
            _ready.Wait(10000);
            if (_startError != null) throw _startError;
        }

        public EventRecord[] Stop() {
            _stop.Set();
            if (_thread != null) _thread.Join(10000);
            return _events.ToArray();
        }

        public EventRecord ReadFocus() {
            var uia = (IAutomation5)new CUIAutomation8();
            return Describe(uia.GetFocusedElement(), "Focus");
        }

        void Run() {
            IAutomation5 uia = null;
            try {
                uia = (IAutomation5)new CUIAutomation8();
                uia.AddFocusChangedEventHandler(IntPtr.Zero, this);
                uia.AddNotificationEventHandler(uia.GetRootElement(), 7 /* TreeScope_Subtree */, IntPtr.Zero, this);
                _clock.Start();
            } catch (Exception ex) {
                _startError = ex;
            }
            _ready.Set();
            if (_startError != null) return;
            _stop.Wait();
            try { uia.RemoveAllEventHandlers(); } catch { }
        }

        bool IsTarget(IElement e) {
            try { return _pids.Contains((int)e.GetCurrentPropertyValue(30002)); } catch { return false; }
        }

        EventRecord Describe(IElement e, string kind) {
            var r = new EventRecord { T = _clock.ElapsedMilliseconds, Kind = kind };
            try {
                r.Name = e.GetCurrentPropertyValue(30005) as string;
                r.AutomationId = e.GetCurrentPropertyValue(30011) as string;
                r.Type = e.GetCurrentPropertyValue(30004) as string; // LocalizedControlType
            } catch { }
            return r;
        }

        public void HandleFocusChangedEvent(IElement sender) {
            Interlocked.Increment(ref RawEventCount);
            if (sender == null || !IsTarget(sender)) return;
            var r = Describe(sender, "Focus");
            r.Text = r.Name;
            _events.Enqueue(r);
        }

        public void HandleNotificationEvent(IElement sender, int kind, int processing, string displayString, string activityId) {
            Interlocked.Increment(ref RawEventCount);
            if (sender == null || !IsTarget(sender)) return;
            var r = Describe(sender, "Notification");
            r.Text = displayString;
            r.NotificationKind = kind >= 0 && kind < Kinds.Length ? Kinds[kind] : kind.ToString();
            r.Processing = processing >= 0 && processing < Processings.Length ? Processings[processing] : processing.ToString();
            r.ActivityId = activityId;
            _events.Enqueue(r);
        }
    }

    public static class Keys {
        [DllImport("user32.dll")] static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
        public static void Press(byte vk, bool shift) {
            if (shift) keybd_event(0x10, 0, 0, UIntPtr.Zero);
            keybd_event(vk, 0, 0, UIntPtr.Zero);
            keybd_event(vk, 0, 2 /* KEYEVENTF_KEYUP */, UIntPtr.Zero);
            if (shift) keybd_event(0x10, 0, 2, UIntPtr.Zero);
        }
    }
}
'@
}

function Start-PtA11yEventTrace {
    <#
    .SYNOPSIS
    Start recording focus and notification events raised by a process. Returns the trace handle.
    #>
    param([Parameter(Mandatory)] [string]$ProcessName)

    $pids = [int[]]@(Get-Process -Name $ProcessName -ErrorAction Stop | ForEach-Object Id)
    $recorder = [PtA11yEvents.Recorder]::new($pids)
    $recorder.Start()
    return $recorder
}

function Stop-PtA11yEventTrace {
    <#
    .SYNOPSIS
    Stop a trace and return its events (T = ms since start). Optionally write them as JSON.
    #>
    param(
        [Parameter(Mandatory)] [PtA11yEvents.Recorder]$Trace,
        [string]$OutFile
    )

    # Let in-flight UIA events arrive before the handlers are removed.
    Start-Sleep -Milliseconds 800
    $events = @($Trace.Stop() | ForEach-Object {
        [pscustomobject]@{
            T = $_.T; Kind = $_.Kind; Type = $_.Type; Name = $_.Name; AutomationId = $_.AutomationId
            Text = $_.Text; NotificationKind = $_.NotificationKind; Processing = $_.Processing; ActivityId = $_.ActivityId
        }
    })
    if ($OutFile) { ConvertTo-Json -InputObject $events -Depth 4 | Set-Content -Path $OutFile -Encoding utf8 }
    return $events
}

function Invoke-PtA11yTabWalk {
    <#
    .SYNOPSIS
    Press Tab (or Shift+Tab) -Count times in the foreground window and record each focus stop.
    The target window must already be foreground (see Force-PtForeground in pt-foreground-guard.ps1).
    #>
    param(
        [Parameter(Mandatory)] [string]$ProcessName,
        [int]$Count = 15,
        [switch]$Reverse,
        [int]$DelayMs = 350,
        [string]$OutFile
    )

    $pids = @(Get-Process -Name $ProcessName -ErrorAction Stop | ForEach-Object Id)
    $recorder = [PtA11yEvents.Recorder]::new([int[]]$pids)
    $stops = [System.Collections.Generic.List[object]]::new()
    for ($i = 1; $i -le $Count; $i++) {
        [PtA11yEvents.Keys]::Press(0x09, [bool]$Reverse)
        Start-Sleep -Milliseconds $DelayMs
        $f = $recorder.ReadFocus()
        $stops.Add([pscustomobject]@{ Step = $i; Type = $f.Type; Name = $f.Name; AutomationId = $f.AutomationId })
    }
    if ($OutFile) { ConvertTo-Json -InputObject $stops.ToArray() -Depth 3 | Set-Content -Path $OutFile -Encoding utf8 }
    return $stops.ToArray()
}
