function ConvertFrom-WebResponseContent {
    [CmdletBinding()]
    param(
        [AllowNull()]
        $Content
    )

    if ($null -eq $Content) {
        return ""
    }
    if ($Content -is [byte[]]) {
        return [System.Text.Encoding]::UTF8.GetString($Content)
    }
    return [string]$Content
}
