# scripts/pt-a11y-probe.ps1
# Screen-reader-oriented UIA audit of a running window. Unlike `winapp ui inspect`, this reads the
# properties a screen reader actually announces (Name, HelpText, HeadingLevel, ItemStatus) and flags
# the defects that an AutomationId alone cannot catch:
#   A11Y-NAME-MISSING   interactive/focusable control with an empty Name
#   A11Y-NAME-TYPENAME  Name is a CLR type name or record ToString() ("Ns.Type", "Rec { X = 1 }")
#   A11Y-NAME-IS-ID     Name equals a code-style AutomationId (likely fallback, not real text)
#   A11Y-NO-HEADINGS    window has no element with a HeadingLevel (no heading navigation)
#
# Usage (dot-source, then call):
#   . .\pt-a11y-probe.ps1
#   Get-PtA11yAudit -ProcessName PowerToys.KeyboardManagerEditorUI -OutFile .\editor-a11y.json
#
# Limitations: only realized elements are visited (scroll virtualized lists/pickers first), and the
# audit cannot judge whether a name is *meaningful* or whether announcements/focus moves happen —
# those still need UIA event tracing and a real screen reader pass.

Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

# System.Windows.Automation predates UIA_HeadingLevelPropertyId (30173) and rejects it, so headings are
# read through a minimal native IUIAutomation shim. Unused vtable slots are placeholders (_N).
if (-not ('PtA11yUia.Native' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
namespace PtA11yUia {
    [ComImport, Guid("352ffba8-0973-437c-a61f-f64cafd81df9"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface ICondition {}
    [ComImport, Guid("14314595-b4bc-4055-95f2-58f2e42c9855"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IElementArray { int Length { get; } IElement GetElement(int index); }
    [ComImport, Guid("d22108aa-8ac5-49a5-837b-37bbb3d7591e"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IElement {
        void _0(); void _1(); void _2();
        IElementArray FindAll(int scope, ICondition condition);
        void _4(); void _5(); void _6();
        [return: MarshalAs(UnmanagedType.Struct)] object GetCurrentPropertyValue(int propertyId);
    }
    [ComImport, Guid("30cbe57d-d9d0-452a-ab13-7ac5ac4825ee"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IAutomation {
        void _0(); void _1(); void _2();
        IElement ElementFromHandle(IntPtr hwnd);
        void _4(); void _5(); void _6(); void _7(); void _8(); void _9(); void _10(); void _11(); void _12(); void _13(); void _14(); void _15(); void _16(); void _17();
        ICondition CreateTrueCondition();
    }
    [ComImport, Guid("ff48dba4-60ef-4201-aa87-54103eef594e")]
    public class CUIAutomation {}
    public static class Native {
        // Returns { Name, AutomationId, Level } for every descendant whose HeadingLevel is 1-9.
        public static List<object[]> GetHeadings(IntPtr hwnd) {
            var uia = (IAutomation)new CUIAutomation();
            var all = uia.ElementFromHandle(hwnd).FindAll(4 /* TreeScope_Descendants */, uia.CreateTrueCondition());
            var result = new List<object[]>();
            for (int i = 0; i < all.Length; i++) {
                var e = all.GetElement(i);
                object v = e.GetCurrentPropertyValue(30173);
                int level = (v is int) ? (int)v - 80050 : 0; // HeadingLevel_None = 80050
                if (level >= 1 && level <= 9) {
                    result.Add(new object[] { e.GetCurrentPropertyValue(30005), e.GetCurrentPropertyValue(30011), level });
                }
            }
            return result;
        }
    }
}
'@
}

$script:PtA11yInteractiveTypes = @(
    'Button', 'CheckBox', 'ComboBox', 'Edit', 'Hyperlink', 'List', 'ListItem', 'MenuItem',
    'RadioButton', 'Slider', 'Spinner', 'SplitButton', 'TabItem', 'Tree', 'TreeItem', 'DataGrid', 'DataItem')

function Test-PtA11yTypeName {
    param([string]$Name)
    return ($Name -cmatch '^[A-Za-z_]\w*(\.[A-Z_]\w*){2,}$') -or ($Name -cmatch '^\w+ \{ \w+ = ')
}

function Get-PtA11yAudit {
    <#
    .SYNOPSIS
    Walk the UIA control view of a process's top-level window and report screen-reader findings.
    Returns an object with Elements, Findings and Summary; optionally writes it as JSON.
    #>
    param(
        [Parameter(Mandatory)] [string]$ProcessName,
        [string]$WindowName,
        [string]$OutFile,
        [int]$MaxElements = 3000
    )

    $procIds = @(Get-Process -Name $ProcessName -ErrorAction Stop | ForEach-Object Id)
    $root = [System.Windows.Automation.AutomationElement]::RootElement
    $windows = $root.FindAll([System.Windows.Automation.TreeScope]::Children, [System.Windows.Automation.Condition]::TrueCondition) |
        Where-Object { $procIds -contains $_.Current.ProcessId -and (-not $WindowName -or $_.Current.Name -eq $WindowName) }
    if (-not $windows) { throw "No top-level window found for process '$ProcessName'." }

    $walker = [System.Windows.Automation.TreeWalker]::ControlViewWalker
    $elements = [System.Collections.Generic.List[object]]::new()
    $findings = [System.Collections.Generic.List[object]]::new()

    foreach ($window in $windows) {
        $headingMap = @{}
        try {
            foreach ($h in [PtA11yUia.Native]::GetHeadings([IntPtr]$window.Current.NativeWindowHandle)) { $headingMap["$($h[0])|$($h[1])"] = $h[2] }
        } catch { Write-Warning "HeadingLevel read failed: $_" }
        $stack = [System.Collections.Generic.Stack[object]]::new()
        $stack.Push(@($window, 0, $window.Current.Name))
        while ($stack.Count -gt 0 -and $elements.Count -lt $MaxElements) {
            $el, $depth, $path = $stack.Pop()
            try { $c = $el.Current } catch { continue }
            $type = $c.ControlType.ProgrammaticName -replace '^ControlType\.', ''
            $record = [pscustomobject]@{
                Window       = $window.Current.Name
                Depth        = $depth
                Type         = $type
                Name         = $c.Name
                AutomationId = $c.AutomationId
                ClassName    = $c.ClassName
                HelpText     = $c.HelpText
                ItemStatus   = $c.ItemStatus
                HeadingLevel = [int]$headingMap["$($c.Name)|$($c.AutomationId)"]
                Focusable    = $c.IsKeyboardFocusable
                Enabled      = $c.IsEnabled
                Offscreen    = $c.IsOffscreen
                Bounds       = if ($c.BoundingRectangle.IsEmpty) { $null } else { '{0},{1},{2},{3}' -f [int]$c.BoundingRectangle.X, [int]$c.BoundingRectangle.Y, [int]$c.BoundingRectangle.Width, [int]$c.BoundingRectangle.Height }
            }
            $elements.Add($record)

            $interactive = ($script:PtA11yInteractiveTypes -contains $type) -or ($c.IsKeyboardFocusable -and $type -notin @('Window', 'Pane', 'Group', 'Text'))
            $where = if ($c.AutomationId) { "$type#$($c.AutomationId)" } else { "$type '$($c.ClassName)'" }
            if ($interactive -and [string]::IsNullOrWhiteSpace($c.Name)) {
                $findings.Add([pscustomobject]@{ Rule = 'A11Y-NAME-MISSING'; Severity = 'High'; Element = $where; Name = $c.Name; Detail = 'Interactive control has no accessible Name.' })
            } elseif (Test-PtA11yTypeName $c.Name) {
                $findings.Add([pscustomobject]@{ Rule = 'A11Y-NAME-TYPENAME'; Severity = 'High'; Element = $where; Name = $c.Name; Detail = 'Name is an implementation type/record string, not user text.' })
            } elseif ($interactive -and $c.AutomationId -and $c.Name -eq $c.AutomationId -and $c.Name -cmatch '^[A-Za-z][a-z0-9]*[A-Z0-9_]\w*$') {
                $findings.Add([pscustomobject]@{ Rule = 'A11Y-NAME-IS-ID'; Severity = 'Medium'; Element = $where; Name = $c.Name; Detail = 'Name equals a code-style AutomationId.' })
            }

            $children = [System.Collections.Generic.List[object]]::new()
            $child = $walker.GetFirstChild($el)
            while ($child) { $children.Add($child); $child = $walker.GetNextSibling($child) }
            for ($i = $children.Count - 1; $i -ge 0; $i--) { $stack.Push(@($children[$i], ($depth + 1), $path)) }
        }

        if (-not ($elements | Where-Object { $_.Window -eq $window.Current.Name -and $_.HeadingLevel -gt 0 })) {
            $findings.Add([pscustomobject]@{ Rule = 'A11Y-NO-HEADINGS'; Severity = 'Medium'; Element = "Window '$($window.Current.Name)'"; Name = ''; Detail = 'No element exposes a HeadingLevel.' })
        }
    }

    $result = [pscustomobject]@{
        ProcessName = $ProcessName
        CapturedAt  = (Get-Date).ToString('o')
        Summary     = [pscustomobject]@{
            Elements = $elements.Count
            Headings = @($elements | Where-Object HeadingLevel -gt 0).Count
            Findings = $findings.Count
            ByRule   = @($findings | Group-Object Rule | ForEach-Object { [pscustomobject]@{ Rule = $_.Name; Count = $_.Count } })
        }
        Findings    = $findings
        Elements    = $elements
    }

    if ($OutFile) { $result | ConvertTo-Json -Depth 6 | Set-Content -Path $OutFile -Encoding utf8 }
    return $result
}
