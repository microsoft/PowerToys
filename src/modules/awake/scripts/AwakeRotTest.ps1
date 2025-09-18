<#
Minimal ROT test script for the runtime‑registered Awake automation object.

Usage:
  .\AwakeRotTest.ps1 [-MonikerName Awake.Automation] -Action <action>

Actions:
    ping            -> PING=pong
    status          -> Returns JSON from GetStatusJson (placeholder now)
    cancel          -> Calls Cancel
    timed:<m>       -> Calls SetTimed(int minutes)
    pingdbg         -> Diagnostic: shows type info and invocation paths

Exit codes:
  0 = success
  2 = moniker not found
  3 = (reserved for bind failure – not currently used)
  4 = method invocation failure

Notes:
  - The automation object is registered with display name pattern: !<MonikerName>
  - We late-bind via IDispatch InvokeMember because the object is surfaced as System.__ComObject.
  - Keep this script self‑contained; avoid multiple Add-Type blocks to prevent type cache issues.
#>
param(
  [string]$MonikerName = 'Awake.Automation',
  [string]$Action
)

# ----------------------------
# Constants (exit codes)
# ----------------------------
Set-Variable -Name EXIT_OK -Value 0 -Option Constant
Set-Variable -Name EXIT_NOT_FOUND -Value 2 -Option Constant
Set-Variable -Name EXIT_CALL_FAIL -Value 4 -Option Constant

Write-Host '[AwakeRotTest] Start' -ForegroundColor Cyan

# ----------------------------
# C# helper (enumerate + bind + invoke)
# ----------------------------
if (-not ('AwakeRot.Client' -as [type])) {
  $code = @'
using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Collections.Generic;
using System.Text;

public static class AwakeRotClient
{
    [DllImport("ole32.dll")] private static extern int GetRunningObjectTable(int reserved, out IRunningObjectTable rot);
    [DllImport("ole32.dll")] private static extern int CreateBindCtx(int reserved, out IBindCtx ctx);

    private static (IRunningObjectTable rot, IBindCtx ctx) Open()
    {
        GetRunningObjectTable(0, out var rot);
        CreateBindCtx(0, out var ctx);
        return (rot, ctx);
    }

    // (Enumeration removed for simplification – list action no longer supported.)

    // Direct bind using CreateItemMoniker fast-path (avoids full enumeration).
    [DllImport("ole32.dll", CharSet = CharSet.Unicode)] private static extern int CreateItemMoniker(string lpszDelim, string lpszItem, out IMoniker ppmk);

    private static object Bind(string display)
    {
        var (rot, ctx) = Open();
        if (display.Length > 1 && display[0] == '!')
        {
            string logical = display.Substring(1);
            if (CreateItemMoniker("!", logical, out var mk) == 0 && mk != null)
            {
                try
                {
                    rot.GetObject(mk, out var directObj);
                    return directObj; // may be null if not found
                }
                catch { return null; }
            }
        }
        return null; // No fallback enumeration (intentionally removed for simplicity/perf determinism)
    }

    // Strong-typed interface (early binding) – mirrors server's IAwakeAutomation definition.
    // GUIDs copied from IAwakeAutomation / AwakeAutomation server code.
    [ComImport]
    [Guid("5CA92C1D-9D7E-4F6D-9B06-5B7B28BF4E21")] // interface GUID
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAwakeAutomation
    {
        [PreserveSig] string Ping();
        void SetIndefinite();
        void SetTimed(int seconds);
        void SetExpirable(int minutes);
        void SetPassive();
        void Cancel();
        [PreserveSig] string GetStatusJson();
    }

    // Fallback late-binding helper (kept for diagnostic / if cast fails)
    private static object CallLate(object obj, string name, params object[] args)
    {
        var t = obj.GetType();
        return t.InvokeMember(
            name,
            System.Reflection.BindingFlags.InvokeMethod |
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.Instance,
            binder: null,
            target: obj,
            args: (args == null || args.Length == 0) ? null : args);
    }

    public static string Exec(string logical, string action)
    {
        string display = "!" + logical;
        var obj = Bind(display);
        if (obj == null)
            return "__NOT_FOUND__";

        var t = obj.GetType();
        try
        {
            // Try strong-typed cast first.
            IAwakeAutomation api = obj as IAwakeAutomation;
            bool typed = api != null;

            if (action == "pingdbg")
            {
                var sb = new StringBuilder();
                sb.AppendLine("TYPE=" + t.FullName);
                sb.AppendLine("StrongTypedCast=" + typed);
                if (typed)
                {
                    try { sb.AppendLine("TypedPing=" + api.Ping()); } catch (Exception ex) { sb.AppendLine("TypedPingErr=" + ex.Message); }
                }
                else
                {
                    try { sb.AppendLine("LatePing=" + CallLate(obj, "Ping")); } catch (Exception ex) { sb.AppendLine("LatePingErr=" + ex.Message); }
                }
                return sb.ToString();
            }

            if (string.IsNullOrEmpty(action) || action == "demo")
            {
                var ping = typed ? api.Ping() : (string)CallLate(obj, "Ping");
                return $"DEMO Ping={ping}";
            }
            if (action == "ping") return "PING=" + (typed ? api.Ping() : (string)CallLate(obj, "Ping"));
            if (action == "status") return typed ? api.GetStatusJson() : (string)CallLate(obj, "GetStatusJson");
            if (action == "cancel") { if (typed) api.Cancel(); else CallLate(obj, "Cancel"); return "CANCEL_OK"; }
            if (action != null && action.StartsWith("timed:"))
            {
                var m = int.Parse(action.Substring(6));
                // NOTE: Server SetTimed expects seconds (per interface). Action timed:<m> originally treated value as minutes -> semantic mismatch.
                // For now keep behavior (treat number as minutes -> convert to seconds for strong typed call) to avoid breaking existing usage.
                if (typed) api.SetTimed(m * 60); else CallLate(obj, "SetTimed", m * 60);
                return "TIMED_OK";
            }
            return "UNKNOWN_ACTION";
        }
        catch (Exception ex)
        {
            return "__CALL_ERROR__" + ex.GetType().Name + ":" + ex.Message;
        }
    }
}
'@

  Add-Type -TypeDefinition $code -ErrorAction Stop | Out-Null
}

# Quick list fast-path
if ($Action -eq 'list') {
  [AwakeRotClient]::List() | ForEach-Object { $_ }
  exit $EXIT_OK
}

$result = [AwakeRotClient]::Exec($MonikerName, $Action)

switch ($result) {
  '__NOT_FOUND__' { Write-Host "Moniker !$MonikerName not found." -ForegroundColor Red; [Environment]::Exit($EXIT_NOT_FOUND) }
  { $_ -like '__CALL_ERROR__*' } { Write-Host "Call failed: $result" -ForegroundColor Red; [Environment]::Exit($EXIT_CALL_FAIL) }
  default { Write-Host $result -ForegroundColor Green; [Environment]::Exit($EXIT_OK) }
}
