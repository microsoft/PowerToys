# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$StandardUser,
    [string]$Description = 'PowerToys standard-user UI-test account'
)

$ErrorActionPreference = 'Stop'

if ($null -eq ('PowerToysUiTestAutoLogon' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;

public static class PowerToysUiTestAutoLogon
{
    [StructLayout(LayoutKind.Sequential)]
    private struct LsaUnicodeString
    {
        public ushort Length;
        public ushort MaximumLength;
        public IntPtr Buffer;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct LsaObjectAttributes
    {
        public int Length;
        public IntPtr RootDirectory;
        public IntPtr ObjectName;
        public uint Attributes;
        public IntPtr SecurityDescriptor;
        public IntPtr SecurityQualityOfService;
    }

    [DllImport("advapi32.dll")]
    private static extern uint LsaOpenPolicy(IntPtr systemName, ref LsaObjectAttributes attributes, uint access, out IntPtr policy);

    [DllImport("advapi32.dll")]
    private static extern uint LsaStorePrivateData(IntPtr policy, ref LsaUnicodeString key, ref LsaUnicodeString value);

    [DllImport("advapi32.dll")]
    private static extern uint LsaRetrievePrivateData(IntPtr policy, ref LsaUnicodeString key, out IntPtr value);

    [DllImport("advapi32.dll")]
    private static extern uint LsaFreeMemory(IntPtr buffer);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool LogonUser(string userName, string domain, string password, int logonType, int provider, out IntPtr token);

    [DllImport("advapi32.dll")]
    private static extern uint LsaClose(IntPtr policy);

    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(IntPtr handle);

    private static LsaUnicodeString CreateString(string value)
    {
        return new LsaUnicodeString
        {
            Length = (ushort)(value.Length * 2),
            MaximumLength = (ushort)((value.Length + 1) * 2),
            Buffer = Marshal.StringToHGlobalUni(value),
        };
    }

    public static uint StorePassword(string password)
    {
        var attributes = new LsaObjectAttributes { Length = Marshal.SizeOf(typeof(LsaObjectAttributes)) };
        IntPtr policy;
        var status = LsaOpenPolicy(IntPtr.Zero, ref attributes, 0x20, out policy);
        if (status != 0)
        {
            return status;
        }

        var key = CreateString("DefaultPassword");
        var value = CreateString(password);
        try
        {
            return LsaStorePrivateData(policy, ref key, ref value);
        }
        finally
        {
            Marshal.FreeHGlobal(key.Buffer);
            Marshal.FreeHGlobal(value.Buffer);
            LsaClose(policy);
        }
    }

    public static string ReadPassword()
    {
        var attributes = new LsaObjectAttributes { Length = Marshal.SizeOf(typeof(LsaObjectAttributes)) };
        IntPtr policy;
        var status = LsaOpenPolicy(IntPtr.Zero, ref attributes, 0x4, out policy);
        if (status != 0)
        {
            return null;
        }

        var key = CreateString("DefaultPassword");
        try
        {
            IntPtr value;
            status = LsaRetrievePrivateData(policy, ref key, out value);
            if (status != 0)
            {
                return null;
            }

            try
            {
                var secret = (LsaUnicodeString)Marshal.PtrToStructure(value, typeof(LsaUnicodeString));
                return secret.Buffer == IntPtr.Zero ? null : Marshal.PtrToStringUni(secret.Buffer, secret.Length / 2);
            }
            finally
            {
                LsaFreeMemory(value);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(key.Buffer);
            LsaClose(policy);
        }
    }

    public static bool ValidatePassword(string userName, string domain, string password, out int errorCode)
    {
        IntPtr token;
        var valid = LogonUser(userName, domain, password, 2, 0, out token);
        errorCode = valid ? 0 : Marshal.GetLastWin32Error();
        if (token != IntPtr.Zero)
        {
            CloseHandle(token);
        }

        return valid;
    }
}
'@
}

$localUser = Get-LocalUser -Name $StandardUser -ErrorAction SilentlyContinue
$plainPassword = if ($null -ne $localUser) { [PowerToysUiTestAutoLogon]::ReadPassword() } else { $null }
$credentialError = 0
$credentialValid = -not [string]::IsNullOrEmpty($plainPassword) -and
    [PowerToysUiTestAutoLogon]::ValidatePassword(
        $StandardUser,
        $env:COMPUTERNAME,
        $plainPassword,
        [ref]$credentialError)
$credentialRotated = $false
if (-not $credentialValid) {
    $passwordBytes = New-Object byte[] 12
    $random = [Security.Cryptography.RandomNumberGenerator]::Create()
    try {
        $random.GetBytes($passwordBytes)
    }
    finally {
        $random.Dispose()
    }
    $plainPassword = ([BitConverter]::ToString($passwordBytes) -replace '-', '') + 'aA1!'
    $securePassword = ConvertTo-SecureString $plainPassword -AsPlainText -Force
    if ($null -eq $localUser) {
        New-LocalUser `
            -Name $StandardUser `
            -Password $securePassword `
            -AccountNeverExpires `
            -PasswordNeverExpires `
            -Description $Description | Out-Null
    }
    else {
        Set-LocalUser -Name $StandardUser -Password $securePassword
    }

    $credentialError = 0
    if (-not [PowerToysUiTestAutoLogon]::ValidatePassword(
            $StandardUser,
            $env:COMPUTERNAME,
            $plainPassword,
            [ref]$credentialError)) {
        throw "The generated auto-logon credential did not authenticate (Win32 error $credentialError)."
    }
    $lsaStatus = [PowerToysUiTestAutoLogon]::StorePassword($plainPassword)
    if ($lsaStatus -ne 0) {
        throw "LsaStorePrivateData failed with NTSTATUS 0x$($lsaStatus.ToString('X8'))."
    }
    $credentialRotated = $true
}
Set-LocalUser -Name $StandardUser -PasswordNeverExpires $true

$winlogon = 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon'
Set-ItemProperty $winlogon AutoAdminLogon '1'
Set-ItemProperty $winlogon ForceAutoLogon '1'
Set-ItemProperty $winlogon DefaultUserName $StandardUser
Set-ItemProperty $winlogon DefaultDomainName $env:COMPUTERNAME
Remove-ItemProperty $winlogon DefaultPassword -ErrorAction SilentlyContinue
Remove-ItemProperty $winlogon AutoLogonCount -ErrorAction SilentlyContinue

[pscustomobject]@{
    StandardUser = $StandardUser
    CredentialValidated = $true
    CredentialRotated = $credentialRotated
}