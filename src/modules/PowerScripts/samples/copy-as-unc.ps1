# @powerscript.id           copy-as-unc
# @powerscript.name         Copy as UNC path
# @powerscript.description   Resolve the selected item's mapped network drive to its UNC path (\\server\share\...) and copy it to the clipboard.
# @powerscript.kind         file
# @powerscript.extensions   *
# @powerscript.output       sideEffect
# @powerscript.capability   clipboard
#
# A "file" PowerScript surfaced on the Explorer right-click menu (contextMenu is inferred).
#
# Mirrors the native "Copy as UNC" PowerToy (PR #46056): for each selected item that lives on a
# mapped network drive (e.g. Z:\team\report.docx), it resolves the drive letter to the underlying
# UNC path (\\server\share\team\report.docx) via WNetGetUniversalNameW and copies the result to the
# clipboard. Works on files and folders, and on a multi-item selection (one UNC per line).
#
# Because the PowerScripts prototype cannot yet hide a context-menu entry based on the drive type,
# this script also handles non-network items gracefully: it copies the original local path and notes
# that it is not on a mapped network drive, so the command is always safe to invoke.

param(
    [string[]]$Files
)

# PowerScripts passes the selection both as -Files and via the POWERSCRIPTS_FILES environment
# variable (newline-separated). Fall back to the latter when the array is empty.
if (-not $Files -or $Files.Count -eq 0) {
    if ($env:POWERSCRIPTS_FILES) {
        $Files = $env:POWERSCRIPTS_FILES -split "`n"
    }
}

if (-not $Files -or $Files.Count -eq 0) {
    Write-Error 'No files provided.'
    exit 1
}

Add-Type -Namespace PowerScripts -Name Unc -MemberDefinition @'
[System.Runtime.InteropServices.DllImport("mpr.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
public static extern int WNetGetUniversalName(string lpLocalPath, int dwInfoLevel, System.IntPtr lpBuffer, ref int lpBufferSize);
'@

# Resolves a local path on a mapped drive to its UNC form. Returns $null when the path is not on a
# network drive (WNetGetUniversalName then reports ERROR_NOT_CONNECTED / ERROR_NO_NET_OR_BAD_PATH).
function Resolve-UncPath {
    param([string]$Path)

    # Already a UNC path — nothing to resolve.
    if ($Path -match '^\\\\') {
        return $Path
    }

    $UNIVERSAL_NAME_INFO_LEVEL = 1
    $ERROR_MORE_DATA = 234
    $bufferSize = 1024
    $buffer = [System.Runtime.InteropServices.Marshal]::AllocHGlobal($bufferSize)
    try {
        $rc = [PowerScripts.Unc]::WNetGetUniversalName($Path, $UNIVERSAL_NAME_INFO_LEVEL, $buffer, [ref]$bufferSize)

        # Grow the buffer once if the driver asked for more room.
        if ($rc -eq $ERROR_MORE_DATA) {
            [System.Runtime.InteropServices.Marshal]::FreeHGlobal($buffer)
            $buffer = [System.Runtime.InteropServices.Marshal]::AllocHGlobal($bufferSize)
            $rc = [PowerScripts.Unc]::WNetGetUniversalName($Path, $UNIVERSAL_NAME_INFO_LEVEL, $buffer, [ref]$bufferSize)
        }

        if ($rc -eq 0) {
            # UNIVERSAL_NAME_INFO's single field is an LPWSTR pointing into the buffer.
            $ptr = [System.Runtime.InteropServices.Marshal]::ReadIntPtr($buffer)
            return [System.Runtime.InteropServices.Marshal]::PtrToStringUni($ptr)
        }

        return $null
    }
    finally {
        [System.Runtime.InteropServices.Marshal]::FreeHGlobal($buffer)
    }
}

$clipboardLines = New-Object System.Collections.Generic.List[string]
$reportLines = New-Object System.Collections.Generic.List[string]

foreach ($f in $Files) {
    $path = $f.Trim()
    if (-not $path) { continue }

    $unc = Resolve-UncPath -Path $path
    if ($unc) {
        $clipboardLines.Add($unc)
        $reportLines.Add($unc)
    }
    else {
        # Not on a mapped network drive: fall back to the local path so the action is still useful.
        $clipboardLines.Add($path)
        $reportLines.Add("$path  (not on a mapped network drive — copied as-is)")
    }
}

if ($clipboardLines.Count -eq 0) {
    Write-Error 'Nothing to copy.'
    exit 1
}

$clipboardText = [string]::Join([Environment]::NewLine, $clipboardLines)
Set-Clipboard -Value $clipboardText

# Show a brief, on-top confirmation (MB_TOPMOST alone is unreliable, so combine
# MB_SYSTEMMODAL | MB_SETFOREGROUND | MB_TOPMOST).
Add-Type -Namespace PowerScripts -Name NativeUi -MemberDefinition @'
[System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
public static extern int MessageBox(System.IntPtr hWnd, string text, string caption, uint type);
'@
$MB_TOPMOST_FLAGS = 0x00051000
$summary = "Copied to clipboard:`r`n`r`n" + [string]::Join([Environment]::NewLine, $reportLines)
[PowerScripts.NativeUi]::MessageBox([System.IntPtr]::Zero, $summary, "PowerScripts - Copy as UNC path", $MB_TOPMOST_FLAGS) | Out-Null

# Also emit the copied text so surfaces that capture stdout (e.g. logs) see the result.
Write-Output $clipboardText
