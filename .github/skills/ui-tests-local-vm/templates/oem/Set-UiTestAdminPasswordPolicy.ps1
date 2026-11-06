# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

<#
.SYNOPSIS
Prevents password expiry for the disposable guest's existing administrator account.

.DESCRIPTION
Changes only the configured account's PasswordNeverExpires flag. Does not reset its password,
enable the account, change group membership, or change machine-wide password policy.
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string]$AdminUserName,
    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string]$StandardUser
)

$ErrorActionPreference = 'Stop'

foreach ($userName in @($AdminUserName, $StandardUser)) {
    if ([string]::IsNullOrWhiteSpace($userName) -or $userName -match '[\\/\x00-\x1f]') {
        throw 'AdminUserName and StandardUser must be unqualified local account names.'
    }
}
if ($AdminUserName -eq $StandardUser) {
    throw 'AdminUserName and StandardUser must refer to different local accounts.'
}

$administrators = @(Get-LocalUser -Name $AdminUserName -ErrorAction Stop)
if ($administrators.Count -ne 1 -or $administrators[0].Name -ne $AdminUserName) {
    throw "The configured guest administrator '$AdminUserName' was not found as an exact local account."
}
$administrator = $administrators[0]
$members = @(Get-LocalGroupMember -SID 'S-1-5-32-544' -ErrorAction Stop)
if (@($members | Where-Object { $_.SID -eq $administrator.SID }).Count -eq 0) {
    throw "The configured guest administrator '$AdminUserName' is not a member of Administrators."
}

# Resolve the name once, then target its SID so wildcard-like names cannot affect another account.
Set-LocalUser -SID $administrator.SID -PasswordNeverExpires $true -ErrorAction Stop
$updatedAdministrator = Get-LocalUser -SID $administrator.SID -ErrorAction Stop
if ($null -eq $updatedAdministrator -or $null -ne $updatedAdministrator.PasswordExpires) {
    throw "Password expiry is still enabled for the configured guest administrator '$AdminUserName'."
}

[pscustomobject]@{
    AdminUserName = $updatedAdministrator.Name
    AdminPasswordNeverExpires = $true
}
