# This script is used to move the resources from all the resx files in the directory (args[0]) to a .rc and .h file for use in C++ projects.

# Root directory which contains the resx files
$parentDirectory = $args[0]

# File name of the base resource.h which contains all the non-localized resource definitions
$baseHeaderFileName = $args[1]

# Target file name of the resource header file, which will be used in code - Example: resource.h
$generatedHeaderFileName = $args[2]

# File name of the base ProjectName.rc which contains all the non-localized resources
$baseRCFileName = $args[3]

# Target file name of the resource rc file, which will be used in code - Example: ProjectName.rc
$generatedRCFileName = $args[4]

# Optional argument: Initial resource id in the resource header file. By default it is 101
if ($args.Count -eq 6)
{
    $initResourceID = $args[5]
}
else
{    
    $initResourceID = 101
}

# Flags to check if the first updated has occurred
$headerFileUpdated = $false
$rcFileUpdated = $false

# Output folder for the new resource files. It will be in ProjectDir\Generated Files so that the files are ignored by .gitignore
$generatedFilesFolder = $parentDirectory + "\Generated Files"

# Create Generated Files folder if it doesn't exist
if (!(Test-Path -Path $generatedFilesFolder))
{
    $paramNewItem = @{
        Path      = $generatedFilesFolder
        ItemType  = 'Directory'
        Force     = $true
    }

    New-Item @paramNewItem
}

# Hash table to get the language codes from the code used in the file name
$languageHashTable = @{ "en" = @("ENU", "ENGLISH", "ENGLISH_US", "English (United States)");
                        "zh-Hans" =  @("CHS", "CHINESE", "NEUTRAL", "Chinese (Simplified)");
                        "zh-CN" =  @("CHS", "CHINESE", "NEUTRAL", "Chinese (Simplified)");
                        "cs" = @("CSY", "CZECH", "NEUTRAL", "Czech");
                        "hu" = @("HUN", "HUNGARIAN", "NEUTRAL", "Hungarian");
                        "pl" = @("PLK", "POLISH", "NEUTRAL", "Polish");
                        "ro" = @("ROM", "ROMANIAN", "NEUTRAL", "Romanian");
                        "sk" = @("SKY", "SLOVAK", "NEUTRAL", "Slovak");
                        "bg" = @("BGR", "BULGARIAN", "NEUTRAL", "Bulgarian");
                        "ru" = @("RUS", "RUSSIAN", "NEUTRAL", "Russian");
                        "ca" = @("CAT", "CATALAN", "NEUTRAL", "Catalan");
                        "de" = @("DEU", "GERMAN", "NEUTRAL", "German");
                        "es" = @("ESN", "SPANISH", "NEUTRAL", "Spanish");
                        "fr" = @("FRA", "FRENCH", "NEUTRAL", "French");
                        "it" = @("ITA", "ITALIAN", "NEUTRAL", "Italian");
                        "nl" = @("NLD", "DUTCH", "NEUTRAL", "Dutch");
                        "nb-NO" = @("NOR", "NORWEGIAN", "NORWEGIAN_BOKMAL", "Norwegian Bokmål (Norway)");
                        "pt-BR" = @("PTB", "PORTUGUESE", "PORTUGUESE_BRAZILIAN", "Portuguese (Brazil)");
                        "eu-ES" = @("EUQ", "BASQUE", "DEFAULT", "Basque (Basque)");
                        "tr" = @("TRK", "TURKISH", "NEUTRAL", "Turkish");
                        "he" = @("HEB", "HEBREW", "NEUTRAL", "Hebrew");
                        "ar" = @("ARA", "ARABIC", "NEUTRAL", "Arabic");
                        "ja" = @("JPN", "JAPANESE", "NEUTRAL", "Japanese");
                        "ko" = @("KOR", "KOREAN", "NEUTRAL", "Korean");
                        "sv" = @("SVE", "SWEDISH", "NEUTRAL", "Swedish");
                        "pt-PT" = @("PTG", "PORTUGUESE", "PORTUGUESE", "Portuguese (Portugal)");
                        "zh-Hant" = @("CHT", "CHINESE", "CHINESE_TRADITIONAL", "Chinese (Traditional)")
                        "zh-TW" = @("CHT", "CHINESE", "CHINESE_TRADITIONAL", "Chinese (Traditional)")
                        }

# Store the content to be written to a buffer
$headerFileContent = ""
$rcFileContent = ""

# Iterate over all resx files in parent directory
Get-ChildItem $parentDirectory -Recurse -Filter *.resx | 
Foreach-Object {
    Write-Host "Processing $($_.FullName)"
    $xmlDocument = $null
    try {
        $xmlDocument = [xml](Get-Content $_.FullName -ErrorAction:Stop)
    }
    catch {
        Write-Host "Failed to load $($_.FullName)"
        exit 0
    }

    # Get language code from file name
    $lang = "en"
    $tokens = $_.Name -split "\."
    if ($tokens.Count -eq 3) {
        $lang = $tokens[1]
    } else {
        $d = $_.Directory.Name
        If ($d.Contains('-')) { # Looks like a language directory
            $lang = $d
        }
    }

    $langData = $languageHashTable[$lang]
    if ($null -eq $langData -and $lang.Contains('-')) {
        # Modern Localization comes in with language + country tuples;
        # we want to detect the language alone if we don't support the language-country
        # version.
        $lang = ($lang -split "-")[0]
        $langData = $languageHashTable[$lang]
    }
    if ($null -eq $langData) {
        Write-Warning "Unknown language $lang"
        Return
    }

    $newLinesForRCFile = ""
    $newLinesForHeaderFile = ""
    $count = $initResourceID

    try {        
        foreach ($entry in $xmlDocument.root.data) {
            $culture = [System.Globalization.CultureInfo]::GetCultureInfo('en-US')
            # Each resource is named as IDS_ResxResourceName, in uppercase. Escape occurrences of double quotes in the string
            $lineInRCFormat = "IDS_" + $entry.name.ToUpper($culture) + " L`"" + $entry.value.Replace("`"", "`"`"") + "`""
            $newLinesForRCFile = $newLinesForRCFile + "`r`n    " + $lineInRCFormat

            # Resource header file needs to be updated only for one language
            if (!$headerFileUpdated) {
                $lineInHeaderFormat = "#define IDS_" + $entry.name.ToUpper($culture) + " " + $count.ToString()
                $newLinesForHeaderFile = $newLinesForHeaderFile + "`r`n" + $lineInHeaderFormat
                $count++
            }
        }
    }
    catch {
        echo "Failed to read XML document."
        exit 0
    }

    if ($newLinesForRCFile -ne "") {
        # Add string table syntax
        $newLinesForRCFile = "`r`nSTRINGTABLE`r`nBEGIN" + $newLinesForRCFile + "`r`nEND"

        $langStart = "`r`n/////////////////////////////////////////////////////////////////////////////`r`n// " + $langData[3]  + " resources`r`n`r`n"
        $langStart += "#if !defined(AFX_RESOURCE_DLL) || defined(AFX_TARG_" + $langData[0] + ")`r`nLANGUAGE LANG_" + $langData[1] + ", SUBLANG_" + $langData[2] + "`r`n"

        $langEnd = "`r`n`r`n#endif    // " + $langData[3] + " resources`r`n/////////////////////////////////////////////////////////////////////////////`r`n"

        $newLinesForRCFile = $langStart + $newLinesForRCFile + $langEnd
    }

    # Initialize the rc file with an auto-generation warning and content from the base rc
    if (!$rcFileUpdated) {
        $rcFileContent = "// This file was auto-generated. Changes to this file may cause incorrect behavior and will be lost if the code is regenerated.`r`n"
        try {
            $rcFileContent += (Get-Content $parentDirectory\$baseRCFileName -Raw)
        }
        catch {
            echo "Failed to read base rc file."
            exit 0
        }
        $rcFileUpdated = $true
    }

    # Add in the new string table to the rc file
    $rcFileContent += $newLinesForRCFile

    # Resource header file needs to be set only once, with an auto-generation warning, content from the base resource header followed by #define for all the resources
    if (!$headerFileUpdated) {
        $headerFileContent = "// This file was auto-generated. Changes to this file may cause incorrect behavior and will be lost if the code is regenerated.`r`n"
        try {
            $headerFileContent += (Get-Content $parentDirectory\$baseHeaderFileName  -Raw)
        }
        catch {
            echo "Failed to read base header file."
            exit 0
        }
        $headerFileContent += $newLinesForHeaderFile
        $headerFileUpdated = $true
    }
}

# Write to header file if the content has changed or if the file doesnt exist
try {
    if (!(Test-Path -Path $generatedFilesFolder\$generatedHeaderFileName) -or (($headerFileContent + "`r`n") -ne (Get-Content $generatedFilesFolder\$generatedHeaderFileName -Raw))) {
        Set-Content -Path $generatedFilesFolder\$generatedHeaderFileName -Value $headerFileContent
    }
    else {
        # echo "Skipping write to generated header file"
    }
}
catch {
    echo "Failed to access generated header file."
    exit 0
}

# Write to rc file if the content has changed or if the file doesnt exist
try {
    if (!(Test-Path -Path $generatedFilesFolder\$generatedRCFileName) -or (($rcFileContent + "`r`n") -ne (Get-Content $generatedFilesFolder\$generatedRCFileName -Raw))) {
        Set-Content -Path $generatedFilesFolder\$generatedRCFileName -Value $rcFileContent -Encoding unicode
    }
    else {    
        # echo "Skipping write to generated rc file"
    }
}
catch {
    echo "Failed to access generated rc file."
    exit 0
}
