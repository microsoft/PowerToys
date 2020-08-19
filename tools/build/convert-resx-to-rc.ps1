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

# Temporary file created used for resgen
$tempFile = "temporaryResourceFile.txt"

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

# Iterate over all resx files in parent directory
Get-ChildItem $parentDirectory -Filter *.resx | 
Foreach-Object {
    # Use resgen to parse resx to txt. More details at https://docs.microsoft.com/en-us/dotnet/framework/tools/resgen-exe-resource-file-generator#converting-between-resource-file-types
    resgen $_.FullName $tempFile

    $newLinesForRCFile = ""
    $newLinesForHeaderFile = ""
    $count = 101
    foreach ($line in Get-Content $tempFile) {
        # Each line of the resgen text file is of the form ResourceName=ResourceValue with no spaces.
        $content = $line -split "=", 2

        # Each resource is named as IDS_ResxResourceName, in uppercase
        $lineInRCFormat = "IDS_" + $content[0].ToUpper() + " L`"" + $content[1] + "`""
        $newLinesForRCFile = $newLinesForRCFile + "`r`n`t" + $lineInRCFormat

        # Resource header file needs to be updated only for one language
        if (!$headerFileUpdated) {
            $lineInHeaderFormat = "#define IDS_" + $content[0].ToUpper() + " " + $count.ToString()
            $newLinesForHeaderFile = $newLinesForHeaderFile + "`r`n" + $lineInHeaderFormat
            $count++
        }
    }

    # Delete temporary text file used by resgen
    Remove-Item $tempFile

    # Add string table syntax
    $newLinesForRCFile = "`r`nSTRINGTABLE`r`nBEGIN" + $newLinesForRCFile + "`r`nEND"

    # Initialize the rc file with an auto-generation warning and content from the base rc
    if (!$rcFileUpdated) {
        Set-Content -Path $generatedFilesFolder\$generatedRCFileName -Value ("// This file was auto-generated. Changes to this file may cause incorrect behavior and will be lost if the code is regenerated.`r`n")
        Add-Content -Path $generatedFilesFolder\$generatedRCFileName -Value (Get-Content $parentDirectory\$baseRCFileName)
        $rcFileUpdated = $true
    }

    # Add in the new string table to the rc file
    Add-Content -Path $generatedFilesFolder\$generatedRCFileName -Value $newLinesForRCFile

    # Resource header file needs to be set only once, with an auto-generation warning, content from the base resource header followed by #define for all the resources
    if (!$headerFileUpdated) {
        Set-Content -Path $generatedFilesFolder\$generatedHeaderFileName -Value ("// This file was auto-generated. Changes to this file may cause incorrect behavior and will be lost if the code is regenerated.`r`n")
        Add-Content -Path $generatedFilesFolder\$generatedHeaderFileName -Value (Get-Content $parentDirectory\$baseHeaderFileName)
        Add-Content -Path $generatedFilesFolder\$generatedHeaderFileName -Value $newLinesForHeaderFile
        $headerFileUpdated = $true
    }
}
