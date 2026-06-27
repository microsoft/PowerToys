# scripts/pt-clipboard-diff.ps1
# Multi-format clipboard inspection. Used to assert that AdvancedPaste plain-paste actually strips
# rich formats while preserving UnicodeText (and similar before/after assertions).

Add-Type -AssemblyName System.Windows.Forms

function Get-PtClipboardFormats {
    <#
    .SYNOPSIS
    Return the list of format names currently on the clipboard (e.g. UnicodeText, HTML Format,
    Rich Text Format, FileDrop, DeviceIndependentBitmap, etc.).
    #>
    $obj = [System.Windows.Forms.Clipboard]::GetDataObject()
    if (-not $obj) { return @() }
    return $obj.GetFormats()
}

function Get-PtClipboardText {
    [System.Windows.Forms.Clipboard]::GetText()
}

function Compare-PtClipboardFormatDiff {
    <#
    .SYNOPSIS
    Diff helper. Given a 'before' formats list (from Get-PtClipboardFormats), return:
      - Added:   formats present in current clipboard but not in before
      - Removed: formats present in before but not in current
      - Common:  formats present in both
    .EXAMPLE
    $before = Get-PtClipboardFormats   # e.g. UnicodeText + HTML Format + RTF
    # ... user/script triggers AP plain-paste ...
    $diff = Compare-PtClipboardFormatDiff -Before $before
    # $diff.Removed should contain 'HTML Format' and 'Rich Text Format'
    # $diff.Common should still contain 'UnicodeText'
    #>
    param([Parameter(Mandatory)][string[]]$Before)
    $current = Get-PtClipboardFormats
    [pscustomobject]@{
        Before  = $Before
        Current = $current
        Added   = @($current | Where-Object { $_ -notin $Before })
        Removed = @($Before | Where-Object { $_ -notin $current })
        Common  = @($current | Where-Object { $_ -in $Before })
    }
}

function Set-PtClipboardRich {
    <#
    .SYNOPSIS
    Put HTML + UnicodeText on the clipboard so plain-paste detection has something to strip.
    Useful as test fixture before invoking AdvancedPaste.PasteAsPlainText.
    #>
    param(
        [string]$Text = 'Hello world',
        [string]$Html = '<html><body><b>Hello</b> <i>world</i></body></html>'
    )
    $obj = New-Object System.Windows.Forms.DataObject
    $obj.SetText($Text, [System.Windows.Forms.TextDataFormat]::UnicodeText)
    $obj.SetText($Html, [System.Windows.Forms.TextDataFormat]::Html)
    [System.Windows.Forms.Clipboard]::SetDataObject($obj, $true)
}
