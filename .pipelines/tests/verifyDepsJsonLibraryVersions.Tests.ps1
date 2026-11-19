<#
.SYNOPSIS
	Exercises dependency version auditing with isolated deps.json manifests.

.DESCRIPTION
	Run directly with PowerShell 7+, without Pester or a product build:
	pwsh -NoProfile -File .pipelines\tests\verifyDepsJsonLibraryVersions.Tests.ps1
#>
#Requires -Version 7
[CmdletBinding()]
Param()

$ErrorActionPreference = 'Stop'
# Expected audit failures must reach the exit-code assertions rather than terminate the harness.
$PSNativeCommandUseErrorActionPreference = $false
$scriptPath = Join-Path $PSScriptRoot '..\verifyDepsJsonLibraryVersions.ps1'
$pwshPath = (Get-Process -Id $PID).Path
$testRoot = Join-Path ([System.IO.Path]::GetTempPath()) ([System.Guid]::NewGuid().ToString())
$passed = 0

function New-Manifest {
	Param([hashtable]$Runtime = @{})

	return @{
		targets = @{
			'net10.0' = @{
				'Example.Package/1.0.0' = @{ runtime = $Runtime }
			}
		}
	}
}

function Invoke-VerificationCase {
	Param(
		[string]$Name,
		[hashtable]$Manifests,
		[int]$ExpectedExitCode,
		[string[]]$ExpectedMessages,
		[string[]]$UnexpectedMessages = @()
	)

	$root = Join-Path $testRoot $Name
	[void][System.IO.Directory]::CreateDirectory($root)
	foreach ($manifest in $Manifests.GetEnumerator()) {
		$path = Join-Path $root $manifest.Key
		[void][System.IO.Directory]::CreateDirectory([System.IO.Path]::GetDirectoryName($path))

		# Raw strings let failure cases bypass JSON serialization and supply invalid input.
		if ($manifest.Value -is [string]) {
			Set-Content -LiteralPath $path -Value $manifest.Value
		}
		else {
			$manifest.Value | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $path
		}

	}

	# A fresh process isolates Add-Type state and prevents the audited script's exit from
	# ending the harness.
	$output = @(& $pwshPath -NoProfile -File $scriptPath -targetDir $root 2>&1)
	$exitCode = $LASTEXITCODE
	$text = $output -join [Environment]::NewLine

	# Ignore wrapping and indentation differences, but retain message and report ordering.
	$normalizedText = [regex]::Replace($text, '\s+', ' ')

	if ($exitCode -ne $ExpectedExitCode) {
		throw "${Name}: expected exit $ExpectedExitCode, got ${exitCode}. $text"
	}

	foreach ($message in $ExpectedMessages) {

		if (!$normalizedText.Contains([regex]::Replace($message, '\s+', ' '))) {
			throw "${Name}: missing '$message'. $text"
		}
	}

	# A failed audit must never claim that its potentially incomplete inventory passed.
	if ($ExpectedExitCode -ne 0) {
		$UnexpectedMessages += 'libraries are mentioned with the same version'
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
	$empty = @{ targets = @{} }
	$matching = New-Manifest @{ 'Example.dll' = @{ fileVersion = '9.0.0.0' } }
	$conflicting = New-Manifest @{ 'Example.dll' = @{ fileVersion = '10.0.0.0' } }
	$noLibraries = 'All 0 libraries are mentioned with the same version'
	$oneLibrary = 'All 1 libraries are mentioned with the same version'

	# Discovery counts manifests, not DLL references: empty valid manifests are not an
	# empty target.
	Invoke-VerificationCase 'no manifests fails' @{} 1 @('No *.deps.json files found')

	Invoke-VerificationCase 'unrelated JSON does not count' @{
		'other.json' = $empty
	} 1 @('No *.deps.json files found')

	Invoke-VerificationCase 'empty targets passes' @{
		'Empty.deps.json' = $empty
	} 0 @($noLibraries) -UnexpectedMessages @('No *.deps.json files found')
	
	Invoke-VerificationCase 'empty runtime passes' @{
		'Empty.deps.json' = New-Manifest
	} 0 @($noLibraries)

	Invoke-VerificationCase 'recursive discovery and paths with spaces' @{
		'one\Application One.deps.json' = $matching
		'two\nested\Application Two.deps.json' = $matching
	} 0 @($oneLibrary)

	# Lexical sorting would put 10 before 9; deliberately unsorted filenames also exercise
	# report stability.
	$expectedReport = "Example.dll`n`t9.0.0.0`n`t`ta.deps.json`n`t`tz.deps.json`n`t10.0.0.0`n`t`tb.deps.json"
	Invoke-VerificationCase 'conflict report sorts versions and filenames' @{
		'z.deps.json' = $matching
		'b.deps.json' = $conflicting
		'a.deps.json' = $matching
	} 1 @($expectedReport, 'Detected 1 libraries')

	# Repeated references must not inflate the number of distinct libraries reported as
	# conflicts.
	$twoLibraries = New-Manifest @{
		'Example.dll' = @{ fileVersion = '9.0.0.0' }
		'Another.dll' = @{ fileVersion = '1.0.0.0' }
	}

	$twoConflicts = New-Manifest @{
		'Example.dll' = @{ fileVersion = '10.0.0.0' }
		'Another.dll' = @{ fileVersion = '2.0.0.0' }
	}

	Invoke-VerificationCase 'counts conflicting libraries not references' @{
		'a.deps.json' = $twoLibraries
		'b.deps.json' = $twoConflicts
		'c.deps.json' = $twoConflicts
	} 1 @('Detected 2 libraries', 'Another.dll', 'Example.dll')

	$excludedPaths = @(
		'Example.UITest.deps.json',
		'MouseJump.Common.UnitTests.deps.json',
		'EnvironmentVariablesUILib.UnitTests.deps.json',
		'Example.FuzzTests.deps.json',
		'CmdPal\Example.deps.json',
		'CommandPalette\Example.deps.json'
	)

	# Regression: filtering before counting made excluded-only targets look undiscovered
	# and fail. Invalid JSON also proves that exclusions are applied before parsing, not
	# just before reporting.
	foreach ($excludedPath in $excludedPaths) {
		Invoke-VerificationCase "excluded only $excludedPath" @{
			$excludedPath = 'invalid JSON'
		} 0 @($noLibraries) -UnexpectedMessages @('No *.deps.json files found', 'Failed to parse')
	}

	$mixed = @{ 'Product.deps.json' = $matching }
	foreach ($excludedPath in $excludedPaths) {
		$mixed[$excludedPath] = $conflicting
	}

	Invoke-VerificationCase 'excluded conflicts do not affect product audit' $mixed 0 @($oneLibrary)
	
	Invoke-VerificationCase 'exclusions are case insensitive' @{
		'example.uitest.deps.json' = 'invalid JSON'
		'mousejump.common.unittests.deps.json' = 'invalid JSON'
		'environmentvariablesuilib.unittests.deps.json' = 'invalid JSON'
		'example.fuzztests.deps.json' = 'invalid JSON'
		'cmdpal\Example.deps.json' = 'invalid JSON'
		'commandpalette\Example.deps.json' = 'invalid JSON'
	} 0 @($noLibraries)

	Invoke-VerificationCase 'other unit tests remain audited' @{
		'Product.deps.json' = $matching
		'Other.UnitTests.deps.json' = $conflicting
	} 1 @('Detected 1 libraries', 'Other.UnitTests.deps.json')

	# Clone before adding a target so later cases keep their original single-version
	# fixture.
	$multipleTargets = $matching | ConvertTo-Json -Depth 20 | ConvertFrom-Json -AsHashtable
	$multipleTargets.targets['net10.0-windows'] = $conflicting.targets['net10.0']
	Invoke-VerificationCase 'all target frameworks are audited' @{
		'MultiTarget.deps.json' = $multipleTargets
	} 1 @('Detected 1 libraries', '9.0.0.0', '10.0.0.0', 'MultiTarget.deps.json')

	Invoke-VerificationCase 'DLL paths and case share one identity' @{
		'a.deps.json' = $matching
		'b.deps.json' = New-Manifest @{ 'lib\net10.0\EXAMPLE.DLL' = @{ fileVersion = '9.0.0.0' } }
	} 0 @($oneLibrary)

	# Compile-only packages and assets without file versions legitimately contribute no
	# audited DLLs.
	Invoke-VerificationCase 'package without runtime is valid' @{
		'CompileOnly.deps.json' = @{
			targets = @{ 'net10.0' = @{ 'CompileOnly/1.0.0' = @{ compile = @{} } } }
		}
	} 0 @($noLibraries)

	Invoke-VerificationCase 'missing fileVersion and non-DLL assets are skipped' @{
		'Assets.deps.json' = New-Manifest @{
			'Example.dll' = @{ assemblyVersion = '1.0.0.0' }
			'Example.pdb' = @{ fileVersion = '1.0.0.0' }
			'Example.exe' = @{ fileVersion = '1.0.0.0' }
		}
	} 0 @($noLibraries)

	# Preserve the VS-generated PowerToys version workaround without extending it to
	# third-party DLLs.
	Invoke-VerificationCase 'PowerToys empty and zero versions are skipped' @{
		'Empty.deps.json' = New-Manifest @{ 'PowerToys.Example.dll' = @{ fileVersion = '' } }
		'Zero.deps.json' = New-Manifest @{ 'PowerToys.Example.dll' = @{ fileVersion = '0.0.0.0' } }
		'Missing.deps.json' = New-Manifest @{ 'PowerToys.Example.dll' = @{} }
		'Versioned.deps.json' = New-Manifest @{ 'PowerToys.Example.dll' = @{ fileVersion = '1.0.0.0' } }
	} 0 @($oneLibrary)

	Invoke-VerificationCase 'third party zero version is not skipped' @{
		'Zero.deps.json' = New-Manifest @{ 'Example.dll' = @{ fileVersion = '0.0.0.0' } }
		'Versioned.deps.json' = $matching
	} 1 @('Detected 1 libraries', '0.0.0.0', '9.0.0.0')

	Invoke-VerificationCase 'third party empty version is not skipped' @{
		'Empty.deps.json' = New-Manifest @{ 'Example.dll' = @{ fileVersion = '' } }
		'Versioned.deps.json' = $matching
	} 1 @('Detected 1 libraries', 'Empty.deps.json', 'Versioned.deps.json')

	# One unreadable manifest invalidates the whole audit, even when another worker parses valid data.
	Invoke-VerificationCase 'malformed JSON fails' @{
		'Broken.deps.json' = 'invalid JSON'
	} 1 @('Failed to parse', 'Broken.deps.json')

	Invoke-VerificationCase 'malformed JSON prevents partial success' @{
		'Good.deps.json' = $matching
		'Broken.deps.json' = 'invalid JSON'
	} 1 @('Failed to parse', 'Broken.deps.json')

	# Valid JSON is not necessarily a valid manifest; no structural error may silently
	# turn missing audit data into a successful zero-library result.
	$rootDiagnostic = "expected an object with an object-valued 'targets' property"
	Invoke-VerificationCase 'missing targets fails' @{
		'Broken.deps.json' = @{}
	} 1 @('Broken.deps.json', $rootDiagnostic)

	$invalidObjects = @(
		@{ Name = 'null'; Json = 'null' },
		@{ Name = 'array'; Json = '[]' },
		@{ Name = 'string'; Json = '"invalid"' },
		@{ Name = 'number'; Json = '42' },
		@{ Name = 'boolean'; Json = 'false' }
	)

	foreach ($invalidObject in $invalidObjects) {
		$value = ConvertFrom-Json -InputObject $invalidObject.Json -AsHashtable -NoEnumerate

		Invoke-VerificationCase "$($invalidObject.Name) root fails" @{
			'Broken.deps.json' = $invalidObject.Json
		} 1 @('Broken.deps.json', $rootDiagnostic)

		Invoke-VerificationCase "$($invalidObject.Name) targets fails" @{
			'Broken.deps.json' = @{ targets = $value }
		} 1 @('Broken.deps.json', $rootDiagnostic)

		Invoke-VerificationCase "$($invalidObject.Name) target fails" @{
			'Broken.deps.json' = @{ targets = @{ 'net10.0' = $value } }
		} 1 @('Broken.deps.json', "target 'net10.0' must be an object")

		Invoke-VerificationCase "$($invalidObject.Name) package fails" @{
			'Broken.deps.json' = @{
				targets = @{ 'net10.0' = @{ 'Broken.Package/1.0.0' = $value } }
			}
		} 1 @('Broken.deps.json', "package 'Broken.Package/1.0.0' must be an object")

		Invoke-VerificationCase "$($invalidObject.Name) runtime fails" @{
			'Broken.deps.json' = @{
				targets = @{ 'net10.0' = @{ 'Broken.Package/1.0.0' = @{ runtime = $value } } }
			}
		} 1 @('Broken.deps.json', "package 'Broken.Package/1.0.0' has a non-object runtime value")
	}

	# Valid siblings must not mask invalid entries, regardless of enumeration order.
	Invoke-VerificationCase 'malformed target prevents partial manifest success' @{
		'Broken.deps.json' = @{
			targets = @{ 'net10.0' = $matching.targets['net10.0']; 'broken-target' = $null }
		}
	} 1 @('Broken.deps.json', "target 'broken-target' must be an object")

	Invoke-VerificationCase 'malformed runtime prevents partial manifest success' @{
		'Broken.deps.json' = @{
			targets = @{
				'net10.0' = @{
					'Example.Package/1.0.0' = $matching.targets['net10.0']['Example.Package/1.0.0']
					'Broken.Package/1.0.0' = @{ runtime = $null }
				}
			}
		}
	} 1 @('Broken.deps.json', "package 'Broken.Package/1.0.0' has a non-object runtime value")

	Invoke-VerificationCase 'malformed structure prevents partial audit success' @{
		'Good.deps.json' = $matching
		'Broken.deps.json' = @{ targets = $null }
	} 1 @('Broken.deps.json', $rootDiagnostic)

	Write-Host "PASSED: $passed verification cases."
}
finally {
	# Remove only this run's isolated fixtures and expose cleanup failures instead of
	# leaving them silently.
	if (Test-Path -LiteralPath $testRoot) {
		try {
			Remove-Item -LiteralPath $testRoot -Recurse -Force
		} catch [System.Management.Automation.RuntimeException] {
			Write-Warning "Failed to clean up test directory '${testRoot}': $_"
			throw
		}
	}
}
