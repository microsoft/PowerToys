<#
  AwakeRotMini.ps1  —  Minimal ROT client for the Awake COM automation object.

  Supported actions:
    ping            -> Calls Ping(), prints  PING=pong
    status          -> Calls GetStatusJson(), prints JSON
    cancel          -> Calls Cancel(), prints CANCEL_OK
    timed:<m>       -> Calls SetTimed(<m * 60 seconds>), prints TIMED_OK  (minutes input)

  Assumptions:
    - Server registered object in ROT via CreateItemMoniker("!", logicalName)
    - We only need late binding (IDispatch InvokeMember) – no type library.

  Exit codes:
    0 = success
    2 = object not found in ROT
    4 = call/parse error

  NOTE: This script intentionally stays dependency‑light and fast to start.
#>
param(
  [string]$MonikerName = 'Awake.Automation',
  [string]$Action = 'ping'
)

# ---------------------------------------------------------------------------
# Inline C# (single public class) – handles:
#   * ROT lookup via CreateItemMoniker("!", logicalName)
#   * Late bound InvokeMember calls
#   * Lightweight action dispatch
# ---------------------------------------------------------------------------
if (-not ('AwakeRotMiniClient' -as [type])) {
  $code = @'
using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

public static class AwakeRotMiniClient
{
    // P/Invoke -------------------------------------------------------------
    [DllImport("ole32.dll")] private static extern int GetRunningObjectTable(int reserved, out IRunningObjectTable rot);
    [DllImport("ole32.dll")] private static extern int CreateBindCtx(int reserved, out IBindCtx ctx);
    [DllImport("ole32.dll", CharSet = CharSet.Unicode)] private static extern int CreateItemMoniker(string delimiter, string item, out IMoniker mk);

    // Internal helpers -----------------------------------------------------
    private static void Open(out IRunningObjectTable rot, out IBindCtx ctx)
    {
        GetRunningObjectTable(0, out rot);
        CreateBindCtx(0, out ctx);
    }

    private static object BindLogical(string logical)
    {
        Open(out var rot, out var ctx);
        if (CreateItemMoniker("!", logical, out var mk) == 0)
        {
            try
            {
                rot.GetObject(mk, out var obj);
                return obj;
            }
            catch
            {
                // Swallow – treated as not found below.
            }
        }
        return null;
    }

    private static object Call(object obj, string name, params object[] args)
    {
        var t = obj.GetType(); // System.__ComObject
        return t.InvokeMember(
            name,
            System.Reflection.BindingFlags.InvokeMethod |
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.Instance,
            null,
            obj,
            (args == null || args.Length == 0) ? null : args);
    }

    // Public entry ---------------------------------------------------------
    public static string Exec(string logical, string action)
    {
        var obj = BindLogical(logical);
        if (obj == null)
            return "__NOT_FOUND__";

        if (string.IsNullOrEmpty(action) || action == "ping")
        {
            try { return "PING=" + Call(obj, "Ping"); } catch (Exception ex) { return Err(ex); }
        }
        try
        {
            if (action == "status")
                return (string)Call(obj, "GetStatusJson");
            if (action == "cancel")
            {
                Call(obj, "Cancel");
                return "CANCEL_OK";
            }
            if (action.StartsWith("timed:", StringComparison.OrdinalIgnoreCase))
            {
                var slice = action.Substring(6);
                if (!int.TryParse(slice, out var minutes) || minutes < 0)
                    return "__ERR=Format:Invalid minutes";
                Call(obj, "SetTimed", minutes * 60);
                return "TIMED_OK";
            }
            return "UNKNOWN_ACTION";
        }
        catch (Exception ex)
        {
            return Err(ex);
        }
    }

    private static string Err(Exception ex) => "__ERR=" + ex.GetType().Name + ":" + ex.Message;
}
'@
  Add-Type -TypeDefinition $code -ErrorAction Stop | Out-Null
}

$result = [AwakeRotMiniClient]::Exec($MonikerName, $Action)

switch ($result) {
  '__NOT_FOUND__' { exit 2 }
  { $_ -like '__ERR=*' } { $host.UI.WriteErrorLine($result); exit 4 }
  default { Write-Output $result; exit 0 }
}
