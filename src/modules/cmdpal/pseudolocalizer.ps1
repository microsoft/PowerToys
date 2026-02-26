#requires -Version 7.0

Set-StrictMode -Version Latest
$PlaceholderRegex = [regex]::new('\{\{|\}\}|\{[^{}]+\}')
$CultureNames = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
[System.Globalization.CultureInfo]::GetCultures([System.Globalization.CultureTypes]::AllCultures) |
    ForEach-Object { if ($_.Name) { [void]$CultureNames.Add($_.Name) } }

$DiacriticMap = [hashtable]::new([StringComparer]::Ordinal)
$DiacriticMap['A'] = 'Å'
$DiacriticMap['B'] = 'Ɓ'
$DiacriticMap['C'] = 'Č'
$DiacriticMap['D'] = 'Ď'
$DiacriticMap['E'] = 'Ē'
$DiacriticMap['F'] = 'Ƒ'
$DiacriticMap['G'] = 'Ğ'
$DiacriticMap['H'] = 'Ĥ'
$DiacriticMap['I'] = 'Ī'
$DiacriticMap['J'] = 'Ĵ'
$DiacriticMap['K'] = 'Ķ'
$DiacriticMap['L'] = 'Ĺ'
$DiacriticMap['M'] = 'Ṁ'
$DiacriticMap['N'] = 'Ń'
$DiacriticMap['O'] = 'Ō'
$DiacriticMap['P'] = 'Ṗ'
$DiacriticMap['Q'] = 'Q'
$DiacriticMap['R'] = 'Ŕ'
$DiacriticMap['S'] = 'Š'
$DiacriticMap['T'] = 'Ť'
$DiacriticMap['U'] = 'Ū'
$DiacriticMap['V'] = 'Ṽ'
$DiacriticMap['W'] = 'Ŵ'
$DiacriticMap['X'] = 'Ẋ'
$DiacriticMap['Y'] = 'Ŷ'
$DiacriticMap['Z'] = 'Ž'
$DiacriticMap['a'] = 'å'
$DiacriticMap['b'] = 'ƀ'
$DiacriticMap['c'] = 'č'
$DiacriticMap['d'] = 'ď'
$DiacriticMap['e'] = 'ē'
$DiacriticMap['f'] = 'ƒ'
$DiacriticMap['g'] = 'ğ'
$DiacriticMap['h'] = 'ĥ'
$DiacriticMap['i'] = 'ī'
$DiacriticMap['j'] = 'ĵ'
$DiacriticMap['k'] = 'ķ'
$DiacriticMap['l'] = 'ĺ'
$DiacriticMap['m'] = 'ṁ'
$DiacriticMap['n'] = 'ń'
$DiacriticMap['o'] = 'ō'
$DiacriticMap['p'] = 'ṗ'
$DiacriticMap['q'] = 'q'
$DiacriticMap['r'] = 'ŕ'
$DiacriticMap['s'] = 'š'
$DiacriticMap['t'] = 'ť'
$DiacriticMap['u'] = 'ū'
$DiacriticMap['v'] = 'ṽ'
$DiacriticMap['w'] = 'ŵ'
$DiacriticMap['x'] = 'ẋ'
$DiacriticMap['y'] = 'ŷ'
$DiacriticMap['z'] = 'ž'

function Normalize-CultureName {
    param([string]$Value)
    if ([string]::IsNullOrWhiteSpace($Value)) {
        return $null
    }

    return $Value.Trim()
}

function Assert-CultureName {
    param([string]$Value)
    if ($Value.IndexOfAny([System.IO.Path]::GetInvalidFileNameChars()) -ge 0) {
        Write-Error 'Culture name contains invalid path characters.'
        return $false
    }

    return $true
}

function Is-ResxOrResw {
    param([string]$InputPath)
    return $InputPath.EndsWith('.resx', [StringComparison]::OrdinalIgnoreCase) -or
        $InputPath.EndsWith('.resw', [StringComparison]::OrdinalIgnoreCase)
}

function Is-QpsPlocResw {
    param(
        [string]$InputPath,
        [string]$CultureName
    )

    if ([System.IO.Path]::GetFileName($InputPath).IndexOf(".$CultureName.", [StringComparison]::OrdinalIgnoreCase) -ge 0) {
        return $true
    }

    $directory = [System.IO.Path]::GetDirectoryName($InputPath)
    if ([string]::IsNullOrWhiteSpace($directory)) {
        return $false
    }

    $folderName = [System.IO.Path]::GetFileName($directory)
    return $folderName.Equals($CultureName, [StringComparison]::OrdinalIgnoreCase)
}

function Is-QpsPlocFile {
    param(
        [string]$InputPath,
        [string]$CultureName
    )

    if ($InputPath.EndsWith('.resw', [StringComparison]::OrdinalIgnoreCase)) {
        return Is-QpsPlocResw $InputPath $CultureName
    }

    return [System.IO.Path]::GetFileName($InputPath).IndexOf(".$CultureName.", [StringComparison]::OrdinalIgnoreCase) -ge 0
}

function Get-ReswOutputDirectory {
    param(
        [string]$InputDirectory,
        [string]$CultureName
    )

    $directoryName = [System.IO.Path]::GetFileName($InputDirectory)
    $isCulture = $false
    if (-not [string]::IsNullOrWhiteSpace($directoryName)) {
        if ($CultureNames.Contains($directoryName) -or $directoryName.Equals($CultureName, [StringComparison]::OrdinalIgnoreCase)) {
            $isCulture = $true
        }
    }

    $baseDirectory = $InputDirectory
    if ($isCulture) {
        $parent = [System.IO.Path]::GetDirectoryName($InputDirectory)
        if (-not [string]::IsNullOrWhiteSpace($parent)) {
            $baseDirectory = $parent
        }
    }

    return [System.IO.Path]::Combine($baseDirectory, $CultureName)
}

function Get-OutputPath {
    param(
        [string]$InputPath,
        [string]$CultureName
    )

    $directory = [System.IO.Path]::GetDirectoryName($InputPath)
    if ($null -eq $directory) {
        $directory = ''
    }

    $extension = [System.IO.Path]::GetExtension($InputPath)
    if ($extension.Equals('.resw', [StringComparison]::OrdinalIgnoreCase)) {
        $fileName = [System.IO.Path]::GetFileName($InputPath)
        $outputDirectory = Get-ReswOutputDirectory $directory $CultureName
        return [System.IO.Path]::Combine($outputDirectory, $fileName)
    }

    $baseName = [System.IO.Path]::GetFileNameWithoutExtension($InputPath)
    return [System.IO.Path]::Combine($directory, "$baseName.$CultureName$extension")
}

function Transform-WithPlaceholders {
    param(
        [string]$InputText,
        [scriptblock]$Transform
    )

    $builder = [System.Text.StringBuilder]::new($InputText.Length)
    $lastIndex = 0
    foreach ($match in $PlaceholderRegex.Matches($InputText)) {
        if ($match.Index -gt $lastIndex) {
            $segment = $InputText.Substring($lastIndex, $match.Index - $lastIndex)
            [void]$builder.Append((& $Transform $segment))
        }

        [void]$builder.Append($match.Value)
        $lastIndex = $match.Index + $match.Length
    }

    if ($lastIndex -lt $InputText.Length) {
        $segment = $InputText.Substring($lastIndex)
        [void]$builder.Append((& $Transform $segment))
    }

    return $builder.ToString()
}

function Transform-Chars {
    param(
        [string]$InputText,
        [scriptblock]$Map
    )

    $chars = $InputText.ToCharArray()
    for ($i = 0; $i -lt $chars.Length; $i++) {
        $chars[$i] = & $Map $chars[$i]
    }

    return -join $chars
}

function Add-Diacritics {
    param([string]$InputText)
    return Transform-Chars $InputText {
        param($char)
        $key = [string]$char
        if ($DiacriticMap.ContainsKey($key)) {
            return $DiacriticMap[$key]
        }

        return $char
    }
}

function Replace-WithXs {
    param([string]$InputText)
    return Transform-Chars $InputText {
        param($char)
        if ([char]::IsLetter($char)) {
            return [char]::IsUpper($char) ? 'X' : 'x'
        }

        return $char
    }
}

function Pseudo-Localize {
    param(
        [string]$InputText,
        [string]$ModeName
    )

    if ([string]::IsNullOrEmpty($InputText)) {
        return $InputText
    }

    switch ($ModeName) {
        'brackets' { return "[$InputText]" }
        'diacritics' { return Transform-WithPlaceholders $InputText { param($segment) Add-Diacritics $segment } }
        'xs' { return Transform-WithPlaceholders $InputText { param($segment) Replace-WithXs $segment } }
        default { return $InputText }
    }
}

function Process-File {
    param(
        [string]$InputPath,
        [string]$ModeName,
        [string]$CultureName
    )

    if (-not (Is-ResxOrResw $InputPath)) {
        Write-Error "Unsupported file extension: $InputPath"
        return 1
    }

    if (Is-QpsPlocFile $InputPath $CultureName) {
        Write-Host "Skipping $CultureName file: $InputPath"
        return 0
    }

    try {
        $document = [System.Xml.Linq.XDocument]::Load($InputPath, [System.Xml.Linq.LoadOptions]::PreserveWhitespace)
    } catch [System.Xml.XmlException] {
        Write-Error "Invalid XML document: $InputPath"
        return 1
    } catch [System.IO.IOException] {
        Write-Error "Failed to read file: $InputPath"
        return 1
    }
    if ($null -eq $document.Root) {
        Write-Error "Invalid XML document: $InputPath"
        return 1
    }

    foreach ($dataElement in $document.Root.Descendants('data')) {
        if ($dataElement.Attribute('type') -or $dataElement.Attribute('mimetype')) {
            continue
        }

        $valueElement = $dataElement.Element('value')
        if ($null -eq $valueElement) {
            continue
        }

        $valueElement.Value = Pseudo-Localize $valueElement.Value $ModeName
    }

    $outputPath = Get-OutputPath $InputPath $CultureName
    $outputDirectory = [System.IO.Path]::GetDirectoryName($outputPath)
    if (-not [string]::IsNullOrWhiteSpace($outputDirectory)) {
        [System.IO.Directory]::CreateDirectory($outputDirectory) | Out-Null
    }

    $document.Save($outputPath, [System.Xml.Linq.SaveOptions]::DisableFormatting)
    Write-Host "Generated $outputPath"
    return 0
}

function Process-Directory {
    param(
        [string]$InputDirectory,
        [string]$ModeName,
        [string]$CultureName
    )

    Write-Host "Processing $InputDirectory"

    $files = @(Get-ChildItem -LiteralPath $InputDirectory -Recurse -File |
        Where-Object { Is-ResxOrResw $_.FullName } |
        Where-Object { -not (Is-QpsPlocFile $_.FullName $CultureName) })

    if (-not $files) {
        Write-Host 'No resx/resw files found.'
        return 0
    }

    $failures = 0
    foreach ($file in $files) {
        $result = Process-File $file.FullName $ModeName $CultureName
        if ($result -ne 0) {
            $failures++
        }
    }

    Write-Host "Processed $($files.Count) file(s)."
    return $failures -eq 0 ? 0 : 1
}

function Clear-Directory {
    param(
        [string]$InputDirectory,
        [string]$CultureName
    )

    Write-Host "Processing $InputDirectory"

    $files = @(Get-ChildItem -LiteralPath $InputDirectory -Recurse -File |
        Where-Object { Is-QpsPlocFile $_.FullName $CultureName })

    if (-not $files) {
        Write-Host "No $CultureName files found."
        return 0
    }

    foreach ($file in $files) {
        Remove-Item -LiteralPath $file.FullName -Force
        Write-Host "Deleted $($file.FullName)"
    }

    Write-Host "Removed $($files.Count) file(s)."
    return 0
}

function Clear-File {
    param(
        [string]$InputPath,
        [string]$CultureName
    )

    if (Is-QpsPlocFile $InputPath $CultureName) {
        Remove-Item -LiteralPath $InputPath -Force
        Write-Host "Deleted $InputPath"
        return 0
    }

    if (-not (Is-ResxOrResw $InputPath)) {
        Write-Error "Unsupported file extension: $InputPath"
        return 1
    }

    $outputPath = Get-OutputPath $InputPath $CultureName
    if (Test-Path -LiteralPath $outputPath -PathType Leaf) {
        Remove-Item -LiteralPath $outputPath -Force
        Write-Host "Deleted $outputPath"
    } else {
        Write-Host "No $CultureName file found for $InputPath"
    }

    return 0
}

function Resolve-InputPath {
    param([string]$InputPath)

    try {
        $resolved = Resolve-Path -LiteralPath $InputPath -ErrorAction Stop | Select-Object -First 1
        return $resolved.ProviderPath
    } catch {
        Write-Error "Path not found: $InputPath"
        return $null
    }
}

function Invoke-PseudoLocalization {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory, Position = 0)]
        [string]$Path,

        [Parameter()]
        [ValidateSet('brackets', 'diacritics', 'xs')]
        [string]$Mode = 'brackets',

        [Parameter()]
        [string]$Culture = 'qps-ploc',

        [Parameter(ValueFromRemainingArguments = $true)]
        $UnusedArgs
    )

    $normalizedCulture = Normalize-CultureName $Culture
    if (-not $normalizedCulture) {
        Write-Error 'Culture name is required.'
        return 1
    }

    if (-not (Assert-CultureName $normalizedCulture)) {
        return 1
    }

    Write-Host "Generating pseudo-localized resources for '$Path' (mode: $Mode, culture: $normalizedCulture)."

    $result = 0
    $fullPath = Resolve-InputPath $Path
    if (-not $fullPath) {
        $result = 1
    } elseif (Test-Path -LiteralPath $fullPath -PathType Leaf) {
        $result = Process-File $fullPath $Mode $normalizedCulture
    } elseif (Test-Path -LiteralPath $fullPath -PathType Container) {
        $result = Process-Directory $fullPath $Mode $normalizedCulture
    } else {
        Write-Error "Path not found: $fullPath"
        $result = 1
    }

    Write-Host "Completed with exit code $result."
    return $result
}

function Clear-PseudoLocalization {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory, Position = 0)]
        [string]$Path,

        [Parameter()]
        [string]$Culture = 'qps-ploc',

        [Parameter(ValueFromRemainingArguments = $true)]
        $UnusedArgs
    )

    $normalizedCulture = Normalize-CultureName $Culture
    if (-not $normalizedCulture) {
        Write-Error 'Culture name is required.'
        return 1
    }

    if (-not (Assert-CultureName $normalizedCulture)) {
        return 1
    }

    Write-Host "Clearing pseudo-localized resources for '$Path' (culture: $normalizedCulture)."

    $result = 0
    $fullPath = Resolve-InputPath $Path
    if (-not $fullPath) {
        $result = 1
    } elseif (Test-Path -LiteralPath $fullPath -PathType Leaf) {
        $result = Clear-File $fullPath $normalizedCulture
    } elseif (Test-Path -LiteralPath $fullPath -PathType Container) {
        $result = Clear-Directory $fullPath $normalizedCulture
    } else {
        Write-Error "Path not found: $fullPath"
        $result = 1
    }

    Write-Host "Completed with exit code $result."
    return $result
}

function Invoke-PseudoLocalizer {
    [CmdletBinding(DefaultParameterSetName = 'Generate')]
    param(
        [Parameter(Mandatory, Position = 0)]
        [ValidateSet('generate', 'clear')]
        [string]$Command,

        [Parameter(Mandatory, Position = 1)]
        [string]$Path,

        [Parameter(ParameterSetName = 'Generate')]
        [ValidateSet('brackets', 'diacritics', 'xs')]
        [string]$Mode = 'brackets',

        [Parameter()]
        [string]$Culture = 'qps-ploc',

        [Parameter(ValueFromRemainingArguments = $true)]
        $UnusedArgs
    )

    switch ($Command) {
        'generate' { return Invoke-PseudoLocalization -Path $Path -Mode $Mode -Culture $Culture }
        'clear' { return Clear-PseudoLocalization -Path $Path -Culture $Culture }
    }
}

if ($MyInvocation.InvocationName -ne '.') {
    if ($args.Length -eq 0) {
        Write-Host 'Usage:'
        Write-Host '  pwsh .\pseudolocalizer.ps1 generate <path> -Mode brackets -Culture qps-ploc'
        Write-Host '  pwsh .\pseudolocalizer.ps1 clear <path> -Culture qps-ploc'
        Write-Host '  pwsh .\pseudolocalizer.ps1 Invoke-PseudoLocalization -Path <path> -Mode brackets -Culture qps-ploc'
        Write-Host '  pwsh .\pseudolocalizer.ps1 Clear-PseudoLocalization -Path <path> -Culture qps-ploc'
        exit 1
    }

    $commandName = $args[0]
    $commandArgs = if ($args.Length -gt 1) { $args[1..($args.Length - 1)] } else { @() }
    if ($commandName -in @('generate', 'clear')) {
        exit (Invoke-PseudoLocalizer -Command $commandName @commandArgs)
    }

    if ($commandName -in @('Invoke-PseudoLocalization', 'Clear-PseudoLocalization')) {
        exit (& $commandName @commandArgs)
    }

    Write-Error "Unknown command: $commandName"
    exit 1
}
