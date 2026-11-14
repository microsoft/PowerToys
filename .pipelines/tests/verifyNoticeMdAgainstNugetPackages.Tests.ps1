<#
.SYNOPSIS
	Exercises NOTICE verification with isolated managed and native restore inventories.
#>
[CmdletBinding()]
Param()

$ErrorActionPreference = 'Stop'
$scriptPath = Join-Path $PSScriptRoot '../verifyNoticeMdAgainstNugetPackages.ps1'
$testRoot = Join-Path ([System.IO.Path]::GetTempPath()) ([System.Guid]::NewGuid().ToString())
$passed = 0

function New-Inventory {
	Param([hashtable]$Dependencies = @{}, [string]$Framework = 'net10.0')
	return @{
		version = 3
		project = @{
			restore = @{}
			frameworks = @{ $Framework = @{ dependencies = $Dependencies } }
		}
	}
}

function Write-Inventory {
	Param([object]$Inventory, [string]$AssetPath, [string]$ProjectPath)
	[void][System.IO.Directory]::CreateDirectory([System.IO.Path]::GetDirectoryName($AssetPath))
	if ($Inventory -is [string]) {
		Set-Content -LiteralPath $AssetPath -Value $Inventory
		return
	}
	$clone = $Inventory | ConvertTo-Json -Depth 20 | ConvertFrom-Json -AsHashtable
	if ($clone.project -and $clone.project.ContainsKey('restore')) {
		if (!$clone.project.restore.ContainsKey('projectPath')) {
			$clone.project.restore.projectPath = $ProjectPath
		}
	}
	$clone | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $AssetPath
}

function Invoke-VerificationCase {
	Param(
		[string]$Name,
		[AllowNull()][object]$Notice,
		[hashtable]$Projects,
		[int]$ExpectedExitCode,
		[string[]]$ExpectedMessages,
		[hashtable]$ExtraAssets = @{},
		[switch]$VerboseOutput,
		[string[]]$UnexpectedMessages = @()
	)

	$root = Join-Path $testRoot $Name
	[void][System.IO.Directory]::CreateDirectory($root)
	if ($null -ne $Notice) {
		Set-Content -LiteralPath (Join-Path $root 'NOTICE.md') -Value $Notice
	}
	foreach ($project in $Projects.GetEnumerator()) {
		$projectPath = Join-Path $root $project.Key
		[void][System.IO.Directory]::CreateDirectory([System.IO.Path]::GetDirectoryName($projectPath))
		Set-Content -LiteralPath $projectPath -Value '<Project />'
		$assetPath = Join-Path ([System.IO.Path]::GetDirectoryName($projectPath)) 'obj/project.assets.json'
		Write-Inventory $project.Value $assetPath $projectPath
	}
	foreach ($asset in $ExtraAssets.GetEnumerator()) {
		Write-Inventory $asset.Value (Join-Path $root $asset.Key) (Join-Path $root 'source/project.csproj')
	}

	$arguments = @('-NoProfile', '-File', $scriptPath, '-path', $root)
	if ($VerboseOutput) {
		$arguments += '-Verbose'
	}
	$output = @(& pwsh @arguments 2>&1)
	$exitCode = $LASTEXITCODE
	$text = $output -join [Environment]::NewLine
	$normalizedText = [regex]::Replace($text, '\s+', ' ')
	if ($exitCode -ne $ExpectedExitCode) {
		throw "${Name}: expected exit $ExpectedExitCode, got ${exitCode}. $text"
	}
	foreach ($message in $ExpectedMessages) {
		if (!$normalizedText.Contains([regex]::Replace($message, '\s+', ' '))) {
			throw "${Name}: missing '$message'. $text"
		}
	}
	foreach ($message in $UnexpectedMessages) {
		if ($normalizedText.Contains([regex]::Replace($message, '\s+', ' '))) {
			throw "${Name}: unexpected '$message'. $text"
		}
	}
	Write-Host "PASS: $Name"
	$script:passed++
}

try {
	$heading = "## NuGet Packages used by PowerToys`n`n"
	$inventory = New-Inventory @{ 'Example.Package' = @{ target = 'Package' } }

	Invoke-VerificationCase 'matching packages' ($heading + '- Example.Package') @{
		'source/project with spaces.csproj' = $inventory
	} 0 @('PASSED:', 'Restored packages found: 1')

	$diagnosticNotice = "NOTICE_VERBOSE_SENTINEL`n" + $heading + '- Example.Package'
	Invoke-VerificationCase 'default output stays concise' $diagnosticNotice @{
		'source/project.csproj' = $inventory
	} 0 @('Restored packages found: 1', 'Summary:', 'PASSED:') -UnexpectedMessages @(
		'NOTICE_VERBOSE_SENTINEL', 'Restored package inventory with owning projects:', 'Package: Example.Package'
	)
	Invoke-VerificationCase 'verbose inventory and notice' $diagnosticNotice @{
		'source/project.csproj' = $inventory
	} 0 @('NOTICE_VERBOSE_SENTINEL', 'Restored package inventory with owning projects:', 'Package: Example.Package',
		('Project: ' + [System.IO.Path]::Combine('source', 'project.csproj'))) -VerboseOutput
	Invoke-VerificationCase 'missing package lists all owners' $heading @{
		'one/project.csproj' = $inventory
		'two/project.csproj' = $inventory
	} 1 @('Restored packages found: 1', '  - Example.Package',
		('Project: ' + [System.IO.Path]::Combine('one', 'project.csproj')),
		('Project: ' + [System.IO.Path]::Combine('two', 'project.csproj'))) -UnexpectedMessages @('Restored package inventory with owning projects:')

	Invoke-VerificationCase 'allowed extras' ($heading + "- Example.Package`n- Moq`n- MSTest") @{
		'source/project.csproj' = $inventory
	} 0 @('ExtraInNotice (allowed): 2', '  - Moq', '  - MSTest')

	$filtered = New-Inventory @{
		'EXAMPLE.PACKAGE' = @{ target = 'Package'; autoReferenced = $false }
		'microsoft.Excluded' = @{ target = 'Package' }
		'system.Excluded' = @{ target = 'Package' }
		'Automatic.Package' = @{ target = 'Package'; autoReferenced = $true }
		'Project.Reference' = @{ target = 'Project' }
	}
	Invoke-VerificationCase 'identity and filtering' ($heading + '- Example.Package') @{
		'one/project.csproj' = $filtered
		'two/project.csproj' = $inventory
	} 0 @('Restored packages found: 1')

	$multiTarget = New-Inventory @{ 'Example.Package' = @{ target = 'Package' } }
	$multiTarget.project.frameworks['net10.0-windows'] = @{
		dependencies = @{ 'Second.Package' = @{ target = 'Package' } }
	}
	Invoke-VerificationCase 'multiple frameworks' ($heading + "- Second.Package`n- Example.Package") @{
		'source/project.csproj' = $multiTarget
	} 0 @('Restored packages found: 2')

	$native = New-Inventory @{
		'boost' = @{ target = 'Package' }
		'boost_regex-vc143' = @{ target = 'Package' }
	} 'native'
	Invoke-VerificationCase 'native Boost packages retained' ($heading + "- boost`n- boost_regex-vc143") @{
		'powerrename/project.vcxproj' = $native
	} 0 @('Restored packages found: 2')
	Invoke-VerificationCase 'native Boost missing notices detected' $heading @{
		'powerrename/project.vcxproj' = $native
	} 1 @('  - boost', '  - boost_regex-vc143', 'MissingFromNotice (ERROR',
		('Project: ' + [System.IO.Path]::Combine('powerrename', 'project.vcxproj')))

	Invoke-VerificationCase 'missing package' ($heading + '- Other.Package') @{
		'source/project.csproj' = $inventory
	} 1 @('MissingFromNotice (ERROR', '  - Example.Package', 'FAILED:')

	Invoke-VerificationCase 'unexpected extra' ($heading + "- Example.Package`n- Other.Package") @{
		'source/project.csproj' = $inventory
	} 1 @('ExtraInNotice (ERROR', '  - Other.Package')

	Invoke-VerificationCase 'partial inventory fails' ($heading + '- Example.Package') @{
		'good/project.csproj' = $inventory
		'failed/project.csproj' = 'invalid JSON'
	} 1 @('failed', 'complete restored package inventory', 'FAILED:')

	Invoke-VerificationCase 'malformed JSON fails' ($heading + '- Example.Package') @{
		'source/project.csproj' = 'invalid JSON'
	} 1 @('project.assets.json', 'FAILED:')

	$noFrameworks = New-Inventory
	$noFrameworks.project.Remove('frameworks')
	Invoke-VerificationCase 'missing frameworks fails' ($heading + '- Example.Package') @{
		'source/project.csproj' = $noFrameworks
	} 1 @('target-framework metadata', 'FAILED:')

	$noOwner = New-Inventory
	$noOwner.project.Remove('restore')
	Invoke-VerificationCase 'missing ownership fails' $heading @{
		'source/project.csproj' = $noOwner
	} 1 @('project ownership', 'FAILED:')

	$invalidDependency = New-Inventory @{ 'Example.Package' = @{ version = '[1.0, )' } }
	Invoke-VerificationCase 'missing dependency target fails' $heading @{
		'source/project.csproj' = $invalidDependency
	} 1 @('Invalid dependency metadata', 'FAILED:')

	$unsupportedTarget = New-Inventory @{ 'Example.Package' = @{ target = 'Unknown' } }
	Invoke-VerificationCase 'unknown dependency target fails' $heading @{
		'source/project.csproj' = $unsupportedTarget
	} 1 @('Unsupported dependency target', 'FAILED:')

	$restoreError = New-Inventory
	$restoreError.logs = @(@{ level = 'Error'; message = 'Restore failed.' })
	Invoke-VerificationCase 'failed restore output rejected' $heading @{
		'source/project.csproj' = $restoreError
	} 1 @('restore error', 'Restore failed.', 'FAILED:')

	$deletedOwner = New-Inventory
	$deletedOwner.project.restore.projectPath = Join-Path $testRoot 'deleted.csproj'
	Invoke-VerificationCase 'out of scope ownership fails' $heading @{
		'source/project.csproj' = $deletedOwner
	} 1 @('outside the audit root', 'FAILED:')

	$staleRoot = Join-Path $testRoot 'stale ownership fails'
	$stale = New-Inventory
	$stale.project.restore.projectPath = Join-Path $staleRoot 'deleted.csproj'
	Invoke-VerificationCase 'stale ownership fails' $heading @{
		'source/project.csproj' = $stale
	} 1 @('no longer exists', 'Remove the stale obj folder containing this assets file', 'rerun the audit', 'FAILED:')

	Invoke-VerificationCase 'identical inventories deduplicated' ($heading + '- Example.Package') @{
		'source/project.csproj' = $inventory
	} 0 @('Restored packages found: 1') -ExtraAssets @{
		'copy/project.assets.json' = $inventory
	}
	$conflict = New-Inventory @{ 'Other.Package' = @{ target = 'Package' } }
	Invoke-VerificationCase 'conflicting inventories rejected' ($heading + '- Example.Package') @{
		'source/project.csproj' = $inventory
	} 1 @('Conflicting restored package inventories', 'FAILED:') -ExtraAssets @{
		'copy/project.assets.json' = $conflict
	}

	Invoke-VerificationCase 'missing section fails' '# Other section' @{
		'source/project.csproj' = $inventory
	} 1 @('Expected exactly one', 'FAILED:')

	Invoke-VerificationCase 'invalid notice entry fails' ($heading + 'not a package bullet') @{
		'source/project.csproj' = $inventory
	} 1 @('Invalid package entry', 'FAILED:')

	Invoke-VerificationCase 'duplicate section fails' ($heading + "- Example.Package`n" + $heading) @{
		'source/project.csproj' = $inventory
	} 1 @('Expected exactly one', 'FAILED:')

	Invoke-VerificationCase 'section need not end document' ($heading + "- Example.Package`n`n## Other section`nText") @{
		'source/project.csproj' = $inventory
	} 0 @('PASSED:')

	$empty = New-Inventory
	Invoke-VerificationCase 'empty package inventory' $heading @{
		'source/project.csproj' = $empty
	} 0 @('Restored packages found: 0', 'PASSED:')

	Invoke-VerificationCase 'no assets fails' $heading @{} 1 @('No project.assets.json files', 'FAILED:')
	Invoke-VerificationCase 'missing notice fails' $null @{
		'source/project.csproj' = $inventory
	} 1 @('NOTICE.md', 'FAILED:')

	Write-Host "PASSED: $passed verification cases."
} finally {
	Remove-Item -LiteralPath $testRoot -Recurse -Force -ErrorAction SilentlyContinue
}
