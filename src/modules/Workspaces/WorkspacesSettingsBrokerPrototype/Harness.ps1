[CmdletBinding()]
param(
    [ValidateSet('Build', 'Install', 'Test', 'Cleanup', 'All')]
    [string]$Action = 'All'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$ServiceName = 'PTSettingsBrokerPrototype'
$ServiceAccount = "NT SERVICE\$ServiceName"
$AccountA = 'PTBrokerProtoA'
$AccountB = 'PTBrokerProtoB'
$InstallRoot = Join-Path $env:ProgramData 'Microsoft\PowerToys\SettingsBrokerPrototype'
$BinRoot = Join-Path $InstallRoot 'Bin'
$StoreRoot = Join-Path $InstallRoot 'Store'
$ArtifactRoot = Join-Path $InstallRoot 'TestArtifacts'
$AdminStateRoot = Join-Path $InstallRoot 'AdminState'
$StatePath = Join-Path $AdminStateRoot 'HarnessState.clixml'
$SentinelPath = Join-Path $InstallRoot '.pt-settings-broker-prototype'
$BuildRoot = Join-Path $PSScriptRoot 'bin\x64\Release'
$ServiceExeName = 'PTSettingsBrokerPrototype.Service.exe'
$WorkspacesExeName = 'PTSettingsBrokerPrototype.WorkspacesClient.exe'
$KeyboardExeName = 'PTSettingsBrokerPrototype.KeyboardManagerClient.exe'
$UnknownExeName = 'PTSettingsBrokerPrototype.UnknownClient.exe'
$script:Results = [System.Collections.Generic.List[object]]::new()
$script:RunSequence = 0
$script:CreatedRoot = $false
$script:CreatedService = $false
$script:CreatedAccountA = $false
$script:CreatedAccountB = $false
$script:CreatedAccountASid = $null
$script:CreatedAccountBSid = $null
$script:InvocationId = $null

function Test-IsAdministrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = [Security.Principal.WindowsPrincipal]::new($identity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Assert-Administrator {
    if (-not (Test-IsAdministrator)) {
        throw 'Install, Test, Cleanup, and All must run from an elevated PowerShell session.'
    }
}

function Invoke-Native {
    param(
        [Parameter(Mandatory)][string]$FilePath,
        [Parameter(Mandatory)][string[]]$Arguments
    )
    & $FilePath @Arguments | Out-Host
    if ($LASTEXITCODE -ne 0) {
        throw "$FilePath exited with $LASTEXITCODE."
    }
}

function Get-MSBuild {
    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
    if (Test-Path $vswhere) {
        $found = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' |
            Select-Object -First 1
        if ($found) {
            return $found
        }
    }
    $command = Get-Command msbuild.exe -ErrorAction SilentlyContinue
    if ($command) {
        return $command.Source
    }
    throw 'MSBuild.exe was not found.'
}

function Build-Prototype {
    $msbuild = Get-MSBuild
    $solution = Join-Path $PSScriptRoot 'PTSettingsBrokerPrototype.sln'
    Invoke-Native -FilePath $msbuild -Arguments @(
        $solution,
        '/m',
        '/nologo',
        '/v:minimal',
        '/p:Configuration=Release',
        '/p:Platform=x64',
        '/p:VcpkgEnableManifest=false'
    )
    foreach ($file in @($ServiceExeName, $WorkspacesExeName, $KeyboardExeName, $UnknownExeName)) {
        if (-not (Test-Path (Join-Path $BuildRoot $file))) {
            throw "Expected build output is missing: $file"
        }
    }
}

function New-HighEntropyPassword {
    $bytes = [byte[]]::new(32)
    $rng = [Security.Cryptography.RandomNumberGenerator]::Create()
    try {
        $rng.GetBytes($bytes)
    }
    finally {
        $rng.Dispose()
    }
    return ([Convert]::ToBase64String($bytes) + '!aA9')
}

function New-DirectoryAcl {
    param(
        [Parameter(Mandatory)][Security.Principal.SecurityIdentifier]$Owner,
        [Parameter(Mandatory)][object[]]$Entries
    )
    $acl = [Security.AccessControl.DirectorySecurity]::new()
    $acl.SetOwner($Owner)
    $acl.SetAccessRuleProtection($true, $false)
    foreach ($entry in $Entries) {
        $rule = [Security.AccessControl.FileSystemAccessRule]::new(
            $entry.Sid,
            $entry.Rights,
            [Security.AccessControl.InheritanceFlags]'ContainerInherit, ObjectInherit',
            [Security.AccessControl.PropagationFlags]::None,
            [Security.AccessControl.AccessControlType]::Allow)
        [void]$acl.AddAccessRule($rule)
    }
    return $acl
}

function Set-PrototypeAcls {
    $systemSid = [Security.Principal.SecurityIdentifier]::new('S-1-5-18')
    $adminSid = [Security.Principal.SecurityIdentifier]::new('S-1-5-32-544')
    $authenticatedUsersSid = [Security.Principal.SecurityIdentifier]::new('S-1-5-11')
    $serviceSid = ([Security.Principal.NTAccount]::new($ServiceAccount)).Translate(
        [Security.Principal.SecurityIdentifier])

    $full = [Security.AccessControl.FileSystemRights]::FullControl
    $readExecute = [Security.AccessControl.FileSystemRights]'ReadAndExecute, Synchronize'

    $installAcl = New-DirectoryAcl -Owner $systemSid -Entries @(
        @{ Sid = $systemSid; Rights = $full },
        @{ Sid = $adminSid; Rights = $full },
        @{ Sid = $serviceSid; Rights = $full },
        @{ Sid = $authenticatedUsersSid; Rights = $readExecute }
    )
    Set-Acl -LiteralPath $InstallRoot -AclObject $installAcl
    Set-Acl -LiteralPath $BinRoot -AclObject $installAcl

    $storeAcl = New-DirectoryAcl -Owner $systemSid -Entries @(
        @{ Sid = $systemSid; Rights = $full },
        @{ Sid = $adminSid; Rights = $full },
        @{ Sid = $serviceSid; Rights = $full }
    )
    Set-Acl -LiteralPath $StoreRoot -AclObject $storeAcl

    $artifactAcl = New-DirectoryAcl -Owner $systemSid -Entries @(
        @{ Sid = $systemSid; Rights = $full },
        @{ Sid = $adminSid; Rights = $full },
        @{ Sid = $authenticatedUsersSid; Rights = $readExecute }
    )
    Set-Acl -LiteralPath $ArtifactRoot -AclObject $artifactAcl

    $adminStateAcl = New-DirectoryAcl -Owner $systemSid -Entries @(
        @{ Sid = $systemSid; Rights = $full },
        @{ Sid = $adminSid; Rights = $full }
    )
    Set-Acl -LiteralPath $AdminStateRoot -AclObject $adminStateAcl
}

function Install-Prototype {
    param([switch]$TrackCreation)

    Assert-Administrator
    if (Get-Service -Name $ServiceName -ErrorAction SilentlyContinue) {
        throw "Service $ServiceName already exists. Run explicit Cleanup after confirming it is prototype state."
    }
    foreach ($name in @($AccountA, $AccountB)) {
        if (Get-LocalUser -Name $name -ErrorAction SilentlyContinue) {
            throw "Local account $name already exists. Run explicit Cleanup after confirming it is prototype state."
        }
    }
    if (Test-Path $InstallRoot) {
        throw "Prototype install root already exists: $InstallRoot. Run explicit Cleanup first."
    }
    foreach ($file in @($ServiceExeName, $WorkspacesExeName, $KeyboardExeName, $UnknownExeName)) {
        if (-not (Test-Path (Join-Path $BuildRoot $file))) {
            throw "Build output missing: $file. Run -Action Build first."
        }
    }

    $invocationId = [Guid]::NewGuid().ToString('D')
    if ($TrackCreation) {
        $script:InvocationId = $invocationId
    }
    New-Item -ItemType Directory -Path $BinRoot, $StoreRoot, $ArtifactRoot, $AdminStateRoot -Force | Out-Null
    try {
        [pscustomobject]@{
            ServiceName = $ServiceName
            InstallRoot = [IO.Path]::GetFullPath($InstallRoot).TrimEnd('\')
            InvocationId = $invocationId
        } | ConvertTo-Json -Compress | Set-Content -LiteralPath $SentinelPath -Encoding ASCII
    }
    catch {
        Remove-Item -LiteralPath $InstallRoot -Recurse -Force -ErrorAction SilentlyContinue
        throw
    }
    if ($TrackCreation) {
        $script:CreatedRoot = $true
    }
    foreach ($file in @($ServiceExeName, $WorkspacesExeName, $KeyboardExeName, $UnknownExeName)) {
        Copy-Item -LiteralPath (Join-Path $BuildRoot $file) -Destination (Join-Path $BinRoot $file)
    }

    $serviceExe = Join-Path $BinRoot $ServiceExeName
    Invoke-Native -FilePath "$env:SystemRoot\System32\sc.exe" -Arguments @(
        'create', $ServiceName,
        'binPath=', "`"$serviceExe`"",
        'type=', 'own',
        'start=', 'demand',
        'obj=', $ServiceAccount,
        'DisplayName=', 'PowerToys Settings Broker Prototype'
    )
    if ($TrackCreation) {
        $script:CreatedService = $true
    }
    Invoke-Native -FilePath "$env:SystemRoot\System32\sc.exe" -Arguments @(
        'sidtype', $ServiceName, 'unrestricted'
    )
    Invoke-Native -FilePath "$env:SystemRoot\System32\sc.exe" -Arguments @(
        'failure', $ServiceName,
        'reset=', '86400',
        'actions=', 'restart/5000/restart/15000/restart/30000'
    )
    Invoke-Native -FilePath "$env:SystemRoot\System32\sc.exe" -Arguments @(
        'failureflag', $ServiceName, '1'
    )

    Set-PrototypeAcls

    $passwordA = New-HighEntropyPassword
    $passwordB = New-HighEntropyPassword
    $secureA = ConvertTo-SecureString $passwordA -AsPlainText -Force
    $secureB = ConvertTo-SecureString $passwordB -AsPlainText -Force
    $state = [pscustomobject]@{
        ServiceName = $ServiceName
        ServiceStartName = $ServiceAccount
        ServiceBinPath = "`"$(Join-Path $BinRoot $ServiceExeName)`""
        InstallRoot = [IO.Path]::GetFullPath($InstallRoot).TrimEnd('\')
        InvocationId = $invocationId
        AccountAName = $AccountA
        AccountASid = $null
        AccountBName = $AccountB
        AccountBSid = $null
        CredentialA = $null
        CredentialB = $null
    }
    $state | Export-Clixml -LiteralPath $StatePath

    $createdA = New-LocalUser -Name $AccountA -Password $secureA -PasswordNeverExpires -AccountNeverExpires
    if ($TrackCreation) {
        $script:CreatedAccountA = $true
        $script:CreatedAccountASid = $createdA.Sid.Value
    }
    $state.AccountASid = $createdA.Sid.Value
    $state.CredentialA = [PSCredential]::new("$env:COMPUTERNAME\$AccountA", $secureA)
    $state | Export-Clixml -LiteralPath $StatePath

    $createdB = New-LocalUser -Name $AccountB -Password $secureB -PasswordNeverExpires -AccountNeverExpires
    if ($TrackCreation) {
        $script:CreatedAccountB = $true
        $script:CreatedAccountBSid = $createdB.Sid.Value
    }
    $state.AccountBSid = $createdB.Sid.Value
    $state.CredentialB = [PSCredential]::new("$env:COMPUTERNAME\$AccountB", $secureB)
    $state | Export-Clixml -LiteralPath $StatePath

    Start-Service -Name $ServiceName
    (Get-Service -Name $ServiceName).WaitForStatus(
        [System.ServiceProcess.ServiceControllerStatus]::Running,
        [TimeSpan]::FromSeconds(15))
}

function Quote-ProcessArgument {
    param([Parameter(Mandatory)][string]$Value)
    return '"' + $Value.Replace('\', '\').Replace('"', '\"') + '"'
}

function New-UserWritableArtifactFile {
    param(
        [Parameter(Mandatory)][PSCredential]$Credential,
        [Parameter(Mandatory)][string]$Path
    )
    $fullPath = [IO.Path]::GetFullPath($Path)
    $parent = [IO.Path]::GetFullPath((Split-Path -Parent $fullPath)).TrimEnd('\')
    $expectedParent = [IO.Path]::GetFullPath($ArtifactRoot).TrimEnd('\')
    if ($parent -ine $expectedParent) {
        throw "Refusing to grant a writable artifact outside TestArtifacts: $fullPath"
    }
    if (Test-Path -LiteralPath $fullPath) {
        throw "Writable artifact already exists: $fullPath"
    }

    $stream = [IO.File]::Open(
        $fullPath,
        [IO.FileMode]::CreateNew,
        [IO.FileAccess]::ReadWrite,
        [IO.FileShare]::Read)
    $stream.Dispose()

    $systemSid = [Security.Principal.SecurityIdentifier]::new('S-1-5-18')
    $adminSid = [Security.Principal.SecurityIdentifier]::new('S-1-5-32-544')
    $userSid = ([Security.Principal.NTAccount]::new($Credential.UserName)).Translate(
        [Security.Principal.SecurityIdentifier])
    $acl = [Security.AccessControl.FileSecurity]::new()
    $acl.SetOwner($systemSid)
    $acl.SetAccessRuleProtection($true, $false)
    foreach ($entry in @(
        @{ Sid = $systemSid; Rights = [Security.AccessControl.FileSystemRights]::FullControl },
        @{ Sid = $adminSid; Rights = [Security.AccessControl.FileSystemRights]::FullControl },
        @{ Sid = $userSid; Rights = [Security.AccessControl.FileSystemRights]'Modify, Synchronize' })) {
        $rule = [Security.AccessControl.FileSystemAccessRule]::new(
            $entry.Sid,
            $entry.Rights,
            [Security.AccessControl.InheritanceFlags]::None,
            [Security.AccessControl.PropagationFlags]::None,
            [Security.AccessControl.AccessControlType]::Allow)
        [void]$acl.AddAccessRule($rule)
    }
    Set-Acl -LiteralPath $fullPath -AclObject $acl

    $item = Get-Item -LiteralPath $fullPath -Force
    if ($item.PSIsContainer -or
        (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0)) {
        throw "Writable artifact is not a regular file: $fullPath"
    }
}

function Start-AsUser {
    param(
        [Parameter(Mandatory)][PSCredential]$Credential,
        [Parameter(Mandatory)][string]$FilePath,
        [Parameter(Mandatory)][string[]]$Arguments
    )
    $script:RunSequence++
    $base = Join-Path $ArtifactRoot ("run-{0:D4}" -f $script:RunSequence)
    $stdout = "$base.stdout.txt"
    $stderr = "$base.stderr.txt"
    New-UserWritableArtifactFile -Credential $Credential -Path $stdout
    New-UserWritableArtifactFile -Credential $Credential -Path $stderr
    for ($index = 0; $index -lt $Arguments.Count; $index++) {
        if ($Arguments[$index] -eq '--out-file') {
            if ($index + 1 -ge $Arguments.Count) {
                throw '--out-file is missing its path.'
            }
            New-UserWritableArtifactFile -Credential $Credential -Path $Arguments[$index + 1]
        }
    }
    $argumentLine = ($Arguments | ForEach-Object { Quote-ProcessArgument $_ }) -join ' '
    $process = Start-Process -FilePath $FilePath `
        -ArgumentList $argumentLine `
        -WorkingDirectory (Split-Path -Parent $FilePath) `
        -Credential $Credential `
        -LoadUserProfile `
        -RedirectStandardOutput $stdout `
        -RedirectStandardError $stderr `
        -PassThru
    return [pscustomobject]@{
        Process = $process
        Stdout = $stdout
        Stderr = $stderr
        Started = [DateTime]::UtcNow
    }
}

function Complete-AsUser {
    param(
        [Parameter(Mandatory)]$Run,
        [int]$TimeoutSeconds = 20
    )
    if (-not $Run.Process.WaitForExit($TimeoutSeconds * 1000)) {
        Stop-Process -Id $Run.Process.Id -Force -ErrorAction SilentlyContinue
        throw "Process $($Run.Process.Id) timed out."
    }
    $Run.Process.WaitForExit()
    $stdout = ''
    if (Test-Path $Run.Stdout) {
        $content = Get-Content -LiteralPath $Run.Stdout -Raw -ErrorAction SilentlyContinue
        if ($null -ne $content) {
            $stdout = [string]$content
        }
    }
    $stderr = ''
    if (Test-Path $Run.Stderr) {
        $content = Get-Content -LiteralPath $Run.Stderr -Raw -ErrorAction SilentlyContinue
        if ($null -ne $content) {
            $stderr = [string]$content
        }
    }
    $stdout = $stdout.Trim()
    $stderr = $stderr.Trim()
    $json = $null
    if ($stdout) {
        $lastLine = ($stdout -split "`r?`n" | Where-Object { $_.Trim() } | Select-Object -Last 1)
        try {
            $json = $lastLine | ConvertFrom-Json
        }
        catch {
            $json = $null
        }
    }
    return [pscustomobject]@{
        ExitCode = $Run.Process.ExitCode
        Json = $json
        Stdout = $stdout
        Stderr = $stderr
        ElapsedMs = ([DateTime]::UtcNow - $Run.Started).TotalMilliseconds
    }
}

function Invoke-AsUser {
    param(
        [Parameter(Mandatory)][PSCredential]$Credential,
        [Parameter(Mandatory)][string]$FilePath,
        [Parameter(Mandatory)][string[]]$Arguments,
        [int]$TimeoutSeconds = 20
    )
    return Complete-AsUser -Run (Start-AsUser -Credential $Credential -FilePath $FilePath -Arguments $Arguments) `
        -TimeoutSeconds $TimeoutSeconds
}

function Assert-True {
    param(
        [Parameter(Mandatory)][bool]$Condition,
        [Parameter(Mandatory)][string]$Message
    )
    if (-not $Condition) {
        throw $Message
    }
}

function Assert-Status {
    param(
        [Parameter(Mandatory)]$Result,
        [Parameter(Mandatory)][string]$Expected
    )
    Assert-True ($null -ne $Result.Json) "No JSON response. stdout='$($Result.Stdout)' stderr='$($Result.Stderr)'"
    Assert-True ($Result.Json.status -eq $Expected) "Expected $Expected, got '$($Result.Json.status)'."
}

function Invoke-TestCase {
    param(
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][scriptblock]$Body
    )
    try {
        $detail = & $Body
        $script:Results.Add([pscustomobject]@{ Test = $Name; Result = 'PASS'; Detail = [string]$detail })
    }
    catch {
        $detail = $_.Exception.Message
        if ($_.ScriptStackTrace) {
            $detail += " [$($_.ScriptStackTrace -replace "`r?`n", ' <- ')]"
        }
        Write-Warning "${Name}: $detail"
        $script:Results.Add([pscustomobject]@{ Test = $Name; Result = 'FAIL'; Detail = $detail })
    }
}

function Test-Prototype {
    Assert-Administrator
    if (-not (Test-Path $StatePath)) {
        throw "Harness state missing: $StatePath. Run -Action Install first."
    }
    $state = Import-Clixml -LiteralPath $StatePath
    $credentialA = $state.CredentialA
    $credentialB = $state.CredentialB
    $workspaces = Join-Path $BinRoot $WorkspacesExeName
    $keyboard = Join-Path $BinRoot $KeyboardExeName
    $unknown = Join-Path $BinRoot $UnknownExeName
    $script:Results.Clear()

    Invoke-TestCase '1. Both users ping one singleton' {
        $a = Invoke-AsUser $credentialA $workspaces @('ping', '--minor', '1')
        $b = Invoke-AsUser $credentialB $workspaces @('ping', '--minor', '1')
        Assert-Status $a 'Ok'
        Assert-Status $b 'Ok'
        $services = @(Get-Service -Name $ServiceName -ErrorAction Stop)
        Assert-True ($services.Count -eq 1) 'Expected exactly one singleton service.'
        $servicePid = (Get-CimInstance Win32_Service -Filter "Name='$ServiceName'").ProcessId
        Assert-True ($a.Json.serverPidVerified -eq $true -and $b.Json.serverPidVerified -eq $true) `
            'Client did not report active broker PID verification.'
        Assert-True ($a.Json.serverPid -eq $servicePid -and $b.Json.serverPid -eq $servicePid) `
            'Pipe server PID did not match the exact running service PID.'
        'Both users authenticated the same exact running service PID.'
    }

    Invoke-TestCase '2. User A target 1 roundtrip' {
        $put = Invoke-AsUser $credentialA $workspaces @(
            'put', '--target', '1', '--data', 'user-A-original', '--minor', '1')
        Assert-Status $put 'Ok'
        $get = Invoke-AsUser $credentialA $workspaces @('get', '--target', '1', '--minor', '1')
        Assert-Status $get 'Ok'
        Assert-True ($get.Json.payloadUtf8 -eq 'user-A-original') 'User A payload mismatch.'
        'Put/Get returned the original payload.'
    }

    Invoke-TestCase '3. Per-user isolation' {
        $initialB = Invoke-AsUser $credentialB $workspaces @('get', '--target', '1')
        Assert-Status $initialB 'NotFound'
        Assert-Status (Invoke-AsUser $credentialB $workspaces @(
            'put', '--target', '1', '--data', 'user-B-original')) 'Ok'
        $getB = Invoke-AsUser $credentialB $workspaces @('get', '--target', '1')
        $getA = Invoke-AsUser $credentialA $workspaces @('get', '--target', '1')
        Assert-Status $getB 'Ok'
        Assert-Status $getA 'Ok'
        Assert-True ($getB.Json.payloadUtf8 -eq 'user-B-original') 'User B payload mismatch.'
        Assert-True ($getA.Json.payloadUtf8 -eq 'user-A-original') 'User A data changed.'
        'SID-derived stores remained isolated.'
    }

    Invoke-TestCase '4. Executable-to-target confinement' {
        Assert-Status (Invoke-AsUser $credentialA $workspaces @('get', '--target', '2')) 'TargetDenied'
        Assert-Status (Invoke-AsUser $credentialA $keyboard @('get', '--target', '1')) 'TargetDenied'
        Assert-Status (Invoke-AsUser $credentialA $unknown @('ping')) 'AuthRejected'
        $spoofLauncher = Join-Path $ArtifactRoot 'writable-directory-spoof.ps1'
        @'
param([string]$Source, [string]$Destination)
$ErrorActionPreference = 'Stop'
$exitCode = 1
try {
    Copy-Item -LiteralPath $Source -Destination $Destination -Force
    & $Destination ping
    $exitCode = $LASTEXITCODE
}
finally {
    Remove-Item -LiteralPath $Destination -Force -ErrorAction SilentlyContinue
}
exit $exitCode
'@ | Set-Content -LiteralPath $spoofLauncher -Encoding UTF8
        $spoofRoot = Join-Path (Split-Path -Parent $InstallRoot) (
            "SettingsBrokerPrototypeSpoof-$($state.InvocationId)")
        Assert-True (-not (Test-Path -LiteralPath $spoofRoot)) `
            "Writable spoof directory already exists: $spoofRoot"
        New-Item -ItemType Directory -Path $spoofRoot | Out-Null
        $systemSid = [Security.Principal.SecurityIdentifier]::new('S-1-5-18')
        $adminSid = [Security.Principal.SecurityIdentifier]::new('S-1-5-32-544')
        $userSid = [Security.Principal.SecurityIdentifier]::new([string]$state.AccountASid)
        $spoofAcl = New-DirectoryAcl -Owner $systemSid -Entries @(
            @{ Sid = $systemSid; Rights = [Security.AccessControl.FileSystemRights]::FullControl },
            @{ Sid = $adminSid; Rights = [Security.AccessControl.FileSystemRights]::FullControl },
            @{ Sid = $userSid; Rights = [Security.AccessControl.FileSystemRights]'Modify, Synchronize' }
        )
        Set-Acl -LiteralPath $spoofRoot -AclObject $spoofAcl
        $spoofPath = Join-Path $spoofRoot $WorkspacesExeName
        try {
            $spoof = Invoke-AsUser $credentialA "$env:SystemRoot\System32\WindowsPowerShell\v1.0\powershell.exe" @(
                '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $spoofLauncher,
                '-Source', $unknown, '-Destination', $spoofPath)
            Assert-Status $spoof 'AuthRejected'
        }
        finally {
            Remove-Item -LiteralPath $spoofPath -Force -ErrorAction SilentlyContinue
            $remaining = @(Get-ChildItem -LiteralPath $spoofRoot -Force -ErrorAction SilentlyContinue)
            if ($remaining.Count -eq 0) {
                Remove-Item -LiteralPath $spoofRoot -Force -ErrorAction SilentlyContinue
            }
        }
        Assert-True (-not (Test-Path -LiteralPath $spoofRoot)) `
            'Temporary writable spoof directory was not removed non-recursively.'
        'Cross-target, unknown basename, and user-writable-directory allowed-basename spoof were rejected.'
    }

    Invoke-TestCase '5. Protocol negotiation window' {
        $minor0 = Invoke-AsUser $credentialA $workspaces @('ping', '--minor', '0')
        $minor1 = Invoke-AsUser $credentialA $workspaces @('ping', '--minor', '1')
        $minor2 = Invoke-AsUser $credentialA $workspaces @('ping', '--minor', '2')
        Assert-Status $minor0 'Ok'
        Assert-Status $minor1 'Ok'
        Assert-Status $minor2 'UnsupportedMinor'
        Assert-True ($minor0.Json.capabilities -eq 0) 'Minor 0 must not advertise capabilities.'
        Assert-True (($minor1.Json.capabilities -band 3) -eq 3) 'Minor 1 capabilities missing.'
        'Minor 0/1 accepted; minor 2 rejected; capabilities=multi-target+per-user-quota.'
    }

    Invoke-TestCase '6. Malformed and oversized requests' {
        Assert-Status (Invoke-AsUser $credentialA $workspaces @('malformed')) 'BadRequest'
        Assert-Status (Invoke-AsUser $credentialA $workspaces @('oversized')) 'PayloadTooLarge'
        'Bad magic and >1 MiB declared payload were rejected.'
    }

    Invoke-TestCase '7. Pipe instance creation denied to standard users' {
        $probe = Invoke-AsUser $credentialA $workspaces @('create-instance')
        Assert-True ($probe.ExitCode -eq 0) "CreateNamedPipe probe exited $($probe.ExitCode)."
        Assert-True ($probe.Json.createNamedPipeError -eq 5) `
            "Expected ERROR_ACCESS_DENIED, got $($probe.Json.createNamedPipeError)."
        'Authenticated Users can read/write pipe data but cannot create another instance.'
    }

    Invoke-TestCase '8. Slow response readers retain SID quota' {
        $large = Join-Path $ArtifactRoot 'slow-read-large.bin'
        [IO.File]::WriteAllBytes($large, [Text.Encoding]::UTF8.GetBytes(('L' * 900000)))
        Assert-Status (Invoke-AsUser $credentialA $workspaces @(
            'put', '--target', '1', '--data-file', $large)) 'Ok'
        $slow1 = Start-AsUser $credentialA $workspaces @(
            'slow-read', '--target', '1', '--milliseconds', '3000')
        $slow2 = Start-AsUser $credentialA $workspaces @(
            'slow-read', '--target', '1', '--milliseconds', '3000')
        Start-Sleep -Milliseconds 900
        Assert-True (-not $slow1.Process.HasExited -and -not $slow2.Process.HasExited) `
            'Slow response readers did not remain active.'
        $userB = Invoke-AsUser $credentialB $workspaces @('ping')
        $thirdA = Invoke-AsUser $credentialA $workspaces @('ping')
        Assert-Status $userB 'Ok'
        Assert-True ($userB.ElapsedMs -lt 3000) "User B ping took $([int]$userB.ElapsedMs) ms."
        $thirdFast = $thirdA.ElapsedMs -lt 3000
        $thirdRejected = $null -ne $thirdA.Json -and
            $thirdA.Json.PSObject.Properties.Name -contains 'status' -and
            $thirdA.Json.status -eq 'Busy'
        $thirdTransportFailed = $null -ne $thirdA.Json -and
            $thirdA.Json.PSObject.Properties.Name -contains 'transport'
        Assert-True ($thirdFast -and ($thirdRejected -or $thirdTransportFailed)) 'Third user A connection was not rejected quickly.'
        Assert-Status (Complete-AsUser $slow1 12) 'Ok'
        Assert-Status (Complete-AsUser $slow2 12) 'Ok'
        'Two A slow readers retained quota through ACK; B stayed prompt and third A failed quickly.'
    }

    Invoke-TestCase '9. Concurrent complete writes stay atomic' {
        $fileA = Join-Path $ArtifactRoot 'concurrent-A.bin'
        $fileB = Join-Path $ArtifactRoot 'concurrent-B.bin'
        $out = Join-Path $ArtifactRoot 'concurrent-result.bin'
        [IO.File]::WriteAllBytes($fileA, [Text.Encoding]::UTF8.GetBytes(
            ('BEGIN-A|' + ('A' * 262144) + '|END-A')))
        [IO.File]::WriteAllBytes($fileB, [Text.Encoding]::UTF8.GetBytes(
            ('BEGIN-B|' + ('B' * 262144) + '|END-B')))
        $writeA = Start-AsUser $credentialA $workspaces @(
            'put', '--target', '1', '--data-file', $fileA, '--delay-before-send', '1200')
        $writeB = Start-AsUser $credentialA $workspaces @(
            'put', '--target', '1', '--data-file', $fileB, '--delay-before-send', '1200')
        Assert-Status (Complete-AsUser $writeA 20) 'Ok'
        Assert-Status (Complete-AsUser $writeB 20) 'Ok'
        Assert-Status (Invoke-AsUser $credentialA $workspaces @(
            'get', '--target', '1', '--out-file', $out)) 'Ok'
        $hashA = (Get-FileHash -Algorithm SHA256 -LiteralPath $fileA).Hash
        $hashB = (Get-FileHash -Algorithm SHA256 -LiteralPath $fileB).Hash
        $hashOut = (Get-FileHash -Algorithm SHA256 -LiteralPath $out).Hash
        Assert-True ($hashOut -eq $hashA -or $hashOut -eq $hashB) 'Final payload was mixed or truncated.'
        'Final SHA-256 exactly matched one complete concurrent writer.'
    }

    Invoke-TestCase '10. Request stalls cancel and service stop is prompt' {
        $stall1 = Start-AsUser $credentialA $workspaces @('slow', '--milliseconds', '7000')
        $stall2 = Start-AsUser $credentialA $workspaces @('slow', '--milliseconds', '7000')
        Start-Sleep -Milliseconds 750
        $timer = [Diagnostics.Stopwatch]::StartNew()
        Stop-Service -Name $ServiceName
        (Get-Service -Name $ServiceName).WaitForStatus(
            [System.ServiceProcess.ServiceControllerStatus]::Stopped,
            [TimeSpan]::FromSeconds(15))
        $timer.Stop()
        Assert-True ($timer.ElapsedMilliseconds -lt 3000) `
            "Service stop took $($timer.ElapsedMilliseconds) ms with pending reads."
        [void](Complete-AsUser $stall1 12)
        [void](Complete-AsUser $stall2 12)
        Start-Service -Name $ServiceName
        (Get-Service -Name $ServiceName).WaitForStatus(
            [System.ServiceProcess.ServiceControllerStatus]::Running,
            [TimeSpan]::FromSeconds(15))
        "Pending reads canceled; service stopped in $($timer.ElapsedMilliseconds) ms."
    }

    Invoke-TestCase '11. Restart persistence' {
        $getA = Invoke-AsUser $credentialA $workspaces @('get', '--target', '1')
        $getB = Invoke-AsUser $credentialB $workspaces @('get', '--target', '1')
        Assert-Status $getA 'Ok'
        Assert-Status $getB 'Ok'
        Assert-True ($getB.Json.payloadUtf8 -eq 'user-B-original') 'User B data did not survive restart.'
        Assert-True ($getA.Json.payloadBytes -gt 262144) 'User A concurrent payload did not survive restart.'
        'Both SID stores survived service restart.'
    }

    Invoke-TestCase '12. Identity and protected DACLs' {
        $service = Get-CimInstance Win32_Service -Filter "Name='$ServiceName'"
        Assert-True ($service.StartName -eq $ServiceAccount) "Unexpected service identity: $($service.StartName)"
        $sidType = (& "$env:SystemRoot\System32\sc.exe" qsidtype $ServiceName | Out-String)
        Assert-True ($sidType -match 'UNRESTRICTED') 'Service SID type is not unrestricted.'

        $acl = Get-Acl -LiteralPath $StoreRoot
        Assert-True $acl.AreAccessRulesProtected 'Store DACL still inherits from ProgramData.'
        $serviceSid = ([Security.Principal.NTAccount]::new($ServiceAccount)).Translate(
            [Security.Principal.SecurityIdentifier]).Value
        $ruleSids = @($acl.Access | ForEach-Object {
            try { $_.IdentityReference.Translate([Security.Principal.SecurityIdentifier]).Value }
            catch { $_.IdentityReference.Value }
        })
        Assert-True ($ruleSids -contains 'S-1-5-18') 'Store lacks SYSTEM ACE.'
        Assert-True ($ruleSids -contains 'S-1-5-32-544') 'Store lacks Administrators ACE.'
        Assert-True ($ruleSids -contains $serviceSid) 'Store lacks exact service-account ACE.'
        Assert-True ($ruleSids -notcontains 'S-1-5-11') 'Authenticated Users unexpectedly have Store access.'

        $binAcl = Get-Acl -LiteralPath $BinRoot
        $artifactAcl = Get-Acl -LiteralPath $ArtifactRoot
        $adminStateAcl = Get-Acl -LiteralPath $AdminStateRoot
        Assert-True $binAcl.AreAccessRulesProtected 'Bin DACL is not protected.'
        Assert-True $artifactAcl.AreAccessRulesProtected 'TestArtifacts DACL is not protected.'
        Assert-True $adminStateAcl.AreAccessRulesProtected 'AdminState DACL is not protected.'

        $probeScript = Join-Path $ArtifactRoot 'direct-write-probe.ps1'
        $probeFile = Join-Path $StoreRoot 'ordinary-user-write-probe.txt'
        $artifactProbeFile = Join-Path $ArtifactRoot 'ordinary-user-create-probe.txt'
        $artifactProbeDirectory = Join-Path $ArtifactRoot 'ordinary-user-create-probe'
        $artifactProbeJunction = Join-Path $ArtifactRoot 'ordinary-user-junction-probe'
        @'
param(
    [string]$StoreTarget,
    [string]$ArtifactFile,
    [string]$ArtifactDirectory,
    [string]$ArtifactJunction
)
$result = [ordered]@{
    storeWrite = $false
    artifactFileCreate = $false
    artifactDirectoryCreate = $false
    artifactJunctionCreate = $false
}
try {
    [IO.File]::WriteAllText($StoreTarget, 'unexpected')
    $result.storeWrite = $true
}
catch {}
try {
    [IO.File]::WriteAllText($ArtifactFile, 'unexpected')
    $result.artifactFileCreate = $true
}
catch {}
finally {
    if (Test-Path -LiteralPath $ArtifactFile) {
        [IO.File]::Delete($ArtifactFile)
    }
}
try {
    [void][IO.Directory]::CreateDirectory($ArtifactDirectory)
    $result.artifactDirectoryCreate = $true
}
catch {}
finally {
    if (Test-Path -LiteralPath $ArtifactDirectory) {
        [IO.Directory]::Delete($ArtifactDirectory)
    }
}
try {
    New-Item -ItemType Junction -Path $ArtifactJunction -Target $env:SystemRoot -ErrorAction Stop | Out-Null
    $result.artifactJunctionCreate = $true
}
catch {}
finally {
    if (Test-Path -LiteralPath $ArtifactJunction) {
        [IO.Directory]::Delete($ArtifactJunction)
    }
}
[pscustomobject]$result | ConvertTo-Json -Compress
'@ | Set-Content -LiteralPath $probeScript -Encoding UTF8
        $probe = Invoke-AsUser $credentialA "$env:SystemRoot\System32\WindowsPowerShell\v1.0\powershell.exe" @(
            '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $probeScript,
            '-StoreTarget', $probeFile,
            '-ArtifactFile', $artifactProbeFile,
            '-ArtifactDirectory', $artifactProbeDirectory,
            '-ArtifactJunction', $artifactProbeJunction)
        Assert-True ($probe.ExitCode -eq 0) "Ordinary user ACL probe exited $($probe.ExitCode)."
        Assert-True ($null -ne $probe.Json) 'Ordinary user ACL probe returned no JSON.'
        Assert-True (-not $probe.Json.storeWrite) 'Ordinary user modified Store directly.'
        Assert-True (-not $probe.Json.artifactFileCreate) 'Ordinary user created a file under TestArtifacts.'
        Assert-True (-not $probe.Json.artifactDirectoryCreate) 'Ordinary user created a directory under TestArtifacts.'
        Assert-True (-not $probe.Json.artifactJunctionCreate) 'Ordinary user created a junction under TestArtifacts.'
        Assert-True (-not (Test-Path $probeFile)) 'Ordinary user modified Store directly.'
        Assert-True (-not (Test-Path $artifactProbeFile) -and
            -not (Test-Path $artifactProbeDirectory) -and
            -not (Test-Path $artifactProbeJunction)) 'Ordinary user ACL probe left a child entry behind.'

        $authenticatedUsersSid = 'S-1-5-11'
        $dangerousDirectoryRights =
            [Security.AccessControl.FileSystemRights]::CreateFiles -bor
            [Security.AccessControl.FileSystemRights]::CreateDirectories -bor
            [Security.AccessControl.FileSystemRights]::DeleteSubdirectoriesAndFiles -bor
            [Security.AccessControl.FileSystemRights]::Delete
        $authenticatedUserRules = @($artifactAcl.Access | Where-Object {
            try {
                $_.IdentityReference.Translate(
                    [Security.Principal.SecurityIdentifier]).Value -eq $authenticatedUsersSid
            }
            catch {
                $_.IdentityReference.Value -eq $authenticatedUsersSid
            }
        })
        Assert-True ($authenticatedUserRules.Count -gt 0) 'TestArtifacts lacks its Authenticated Users read ACE.'
        foreach ($rule in $authenticatedUserRules) {
            Assert-True (($rule.FileSystemRights -band $dangerousDirectoryRights) -eq 0) `
                'TestArtifacts grants Authenticated Users directory create/delete rights.'
        }
        'Protected Bin/AdminState/Store/TestArtifacts DACLs deny direct Store writes and new artifact children.'
    }

    Invoke-TestCase '13. Cleanup preflight is non-destructive' {
        $validated = Get-ValidatedCleanupState
        Assert-True ($validated.InvocationId -eq $state.InvocationId) `
            'Cleanup preflight did not validate this install invocation.'
        Assert-True ($null -ne (Get-Service -Name $ServiceName -ErrorAction Stop)) `
            'Cleanup preflight unexpectedly changed the service.'
        Assert-True ((Test-Path $InstallRoot) -and
            (Get-LocalUser -Name $AccountA -ErrorAction SilentlyContinue) -and
            (Get-LocalUser -Name $AccountB -ErrorAction SilentlyContinue)) `
            'Cleanup preflight unexpectedly removed resources.'
        'Exact root/sentinel, protected state, service configuration, and recorded account SIDs validated without deletion.'
    }

    $script:Results | Format-Table -AutoSize | Out-Host
    $failures = @($script:Results | Where-Object Result -eq 'FAIL')
    if ($failures.Count -gt 0) {
        throw "$($failures.Count) prototype test(s) failed."
    }
}

function Get-CanonicalPrototypeRoot {
    return [IO.Path]::GetFullPath(
        (Join-Path $env:ProgramData 'Microsoft\PowerToys\SettingsBrokerPrototype')).TrimEnd('\')
}

function Get-ExpectedServiceBinPath {
    return "`"$(Join-Path $BinRoot $ServiceExeName)`""
}

function Assert-AdminStateProtected {
    if (-not (Test-Path $AdminStateRoot)) {
        throw "Protected harness state directory is missing: $AdminStateRoot"
    }
    $acl = Get-Acl -LiteralPath $AdminStateRoot
    if (-not $acl.AreAccessRulesProtected) {
        throw 'Refusing cleanup because AdminState inherits permissions.'
    }
    $allowed = @('S-1-5-18', 'S-1-5-32-544')
    foreach ($rule in $acl.Access) {
        if ($rule.AccessControlType -ne [Security.AccessControl.AccessControlType]::Allow) {
            continue
        }
        try {
            $sid = $rule.IdentityReference.Translate(
                [Security.Principal.SecurityIdentifier]).Value
        }
        catch {
            $sid = $rule.IdentityReference.Value
        }
        if ($allowed -notcontains $sid) {
            throw "Refusing cleanup because AdminState grants access to $sid."
        }
    }
}

function Get-ValidatedCleanupState {
    $expectedRoot = Get-CanonicalPrototypeRoot
    $actualRoot = [IO.Path]::GetFullPath($InstallRoot).TrimEnd('\')
    if ($actualRoot -ine $expectedRoot) {
        throw "Refusing cleanup of unexpected root: $actualRoot"
    }
    if (-not (Test-Path $InstallRoot) -or -not (Test-Path $SentinelPath)) {
        throw 'Refusing cleanup without the exact prototype root and sentinel.'
    }

    try {
        $sentinel = Get-Content -LiteralPath $SentinelPath -Raw | ConvertFrom-Json
    }
    catch {
        throw 'Refusing cleanup because the sentinel is invalid.'
    }
    if ($sentinel.ServiceName -cne $ServiceName -or
        $sentinel.InstallRoot -ine $expectedRoot -or
        [string]::IsNullOrWhiteSpace([string]$sentinel.InvocationId)) {
        throw 'Refusing cleanup because the sentinel identity does not match.'
    }

    Assert-AdminStateProtected
    if (-not (Test-Path $StatePath)) {
        throw "Refusing cleanup without protected state: $StatePath"
    }
    $state = Import-Clixml -LiteralPath $StatePath
    if ($state.ServiceName -cne $ServiceName -or
        $state.ServiceStartName -ine $ServiceAccount -or
        $state.ServiceBinPath -ine (Get-ExpectedServiceBinPath) -or
        $state.InstallRoot -ine $expectedRoot -or
        $state.InvocationId -cne $sentinel.InvocationId -or
        $state.AccountAName -cne $AccountA -or
        $state.AccountBName -cne $AccountB) {
        throw 'Refusing cleanup because protected state metadata does not match.'
    }

    $service = Get-CimInstance Win32_Service -Filter "Name='$ServiceName'" -ErrorAction SilentlyContinue
    if ($service) {
        if ($service.StartName -ine $ServiceAccount -or
            $service.PathName.Trim() -ine (Get-ExpectedServiceBinPath)) {
            throw 'Refusing cleanup because service StartName or binPath is not the exact prototype value.'
        }
    }

    foreach ($entry in @(
        @{ Name = $AccountA; Sid = [string]$state.AccountASid },
        @{ Name = $AccountB; Sid = [string]$state.AccountBSid })) {
        $account = Get-LocalUser -Name $entry.Name -ErrorAction SilentlyContinue
        if ($account -and
            ([string]::IsNullOrWhiteSpace($entry.Sid) -or $account.Sid.Value -cne $entry.Sid)) {
            throw "Refusing cleanup because recorded SID does not match account $($entry.Name)."
        }
    }
    return $state
}

function Assert-TrackedCleanupPreflight {
    $expectedRoot = Get-CanonicalPrototypeRoot
    if ($script:CreatedRoot) {
        if (-not (Test-Path $InstallRoot) -or -not (Test-Path $SentinelPath)) {
            throw 'Tracked cleanup refused: invocation root or sentinel is missing.'
        }
        $sentinel = Get-Content -LiteralPath $SentinelPath -Raw | ConvertFrom-Json
        if ($sentinel.ServiceName -cne $ServiceName -or
            $sentinel.InstallRoot -ine $expectedRoot -or
            $sentinel.InvocationId -cne $script:InvocationId) {
            throw 'Tracked cleanup refused: invocation sentinel does not match.'
        }
    }
    if ($script:CreatedService) {
        $service = Get-CimInstance Win32_Service -Filter "Name='$ServiceName'" -ErrorAction Stop
        if ($service.StartName -ine $ServiceAccount -or
            $service.PathName.Trim() -ine (Get-ExpectedServiceBinPath)) {
            throw 'Tracked cleanup refused: service configuration changed.'
        }
    }
    foreach ($entry in @(
        @{ Created = $script:CreatedAccountA; Name = $AccountA; Sid = $script:CreatedAccountASid },
        @{ Created = $script:CreatedAccountB; Name = $AccountB; Sid = $script:CreatedAccountBSid })) {
        if ($entry.Created) {
            $account = Get-LocalUser -Name $entry.Name -ErrorAction Stop
            if ($account.Sid.Value -cne $entry.Sid) {
                throw "Tracked cleanup refused: account SID changed for $($entry.Name)."
            }
        }
    }
}

function Remove-ValidatedService {
    $service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
    if (-not $service) {
        return
    }
    if ($service.Status -ne [System.ServiceProcess.ServiceControllerStatus]::Stopped) {
        Stop-Service -Name $ServiceName -Force
        $service.WaitForStatus(
            [System.ServiceProcess.ServiceControllerStatus]::Stopped,
            [TimeSpan]::FromSeconds(15))
    }
    Invoke-Native -FilePath "$env:SystemRoot\System32\sc.exe" -Arguments @('delete', $ServiceName)
    for ($attempt = 0; $attempt -lt 40; $attempt++) {
        if (-not (Get-Service -Name $ServiceName -ErrorAction SilentlyContinue)) {
            return
        }
        Start-Sleep -Milliseconds 250
    }
    throw "Prototype service $ServiceName remained after deletion."
}

function Remove-ValidatedAccount {
    param(
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][string]$Sid
    )
    $account = Get-LocalUser -Name $Name -ErrorAction SilentlyContinue
    if (-not $account) {
        return
    }
    if ($account.Sid.Value -cne $Sid) {
        throw "Refusing to remove $Name because its SID changed."
    }
    Remove-LocalUser -SID ([Security.Principal.SecurityIdentifier]::new($Sid))
}

function Cleanup-Prototype {
    param([switch]$CreatedOnly)

    Assert-Administrator
    if ($CreatedOnly) {
        if (-not ($script:CreatedRoot -or $script:CreatedService -or
            $script:CreatedAccountA -or $script:CreatedAccountB)) {
            return
        }
        Assert-TrackedCleanupPreflight
        if ($script:CreatedService) {
            Remove-ValidatedService
        }
        if ($script:CreatedAccountA) {
            Remove-ValidatedAccount -Name $AccountA -Sid $script:CreatedAccountASid
        }
        if ($script:CreatedAccountB) {
            Remove-ValidatedAccount -Name $AccountB -Sid $script:CreatedAccountBSid
        }
        if ($script:CreatedRoot) {
            Remove-Item -LiteralPath $InstallRoot -Recurse -Force
        }
    }
    else {
        $hasAny = (Test-Path $InstallRoot) -or
            ($null -ne (Get-Service -Name $ServiceName -ErrorAction SilentlyContinue)) -or
            ($null -ne (Get-LocalUser -Name $AccountA -ErrorAction SilentlyContinue)) -or
            ($null -ne (Get-LocalUser -Name $AccountB -ErrorAction SilentlyContinue))
        if (-not $hasAny) {
            return
        }
        $state = Get-ValidatedCleanupState
        Remove-ValidatedService
        if (-not [string]::IsNullOrWhiteSpace([string]$state.AccountASid)) {
            Remove-ValidatedAccount -Name $AccountA -Sid $state.AccountASid
        }
        if (-not [string]::IsNullOrWhiteSpace([string]$state.AccountBSid)) {
            Remove-ValidatedAccount -Name $AccountB -Sid $state.AccountBSid
        }
        Remove-Item -LiteralPath $InstallRoot -Recurse -Force
    }
}

switch ($Action) {
    'Build' {
        Build-Prototype
    }
    'Install' {
        Install-Prototype
    }
    'Test' {
        Test-Prototype
    }
    'Cleanup' {
        Cleanup-Prototype
    }
    'All' {
        Build-Prototype
        try {
            Install-Prototype -TrackCreation
            Test-Prototype
        }
        finally {
            Cleanup-Prototype -CreatedOnly
        }
    }
}
