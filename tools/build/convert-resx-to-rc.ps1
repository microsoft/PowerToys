$parentDirectory = $args[0]

$baseHeaderFileName = $args[1]
$generatedHeaderFileName = $args[2]
$baseRCFileName = $args[3]
$generatedRCFileName = $args[4]
$tempFile = "temporaryResourceFile.txt"

$headerFileUpdated = $false
$rcFileUpdated = $false
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
    # Convert resx to rc file
    # Use resgen to parse resx to txt
    resgen $_.FullName $tempFile

    $newLinesForRCFile = ""
    $newLinesForHeaderFile = ""
    $count = 101
    foreach ($line in Get-Content $tempFile) {
        $content = $line -split "=", 2
        $lineInRCFormat = "IDS_" + $content[0].ToUpper() + " L`"" + $content[1] + "`""
        $newLinesForRCFile = $newLinesForRCFile + "`r`n`t" + $lineInRCFormat
        if (!$headerFileUpdated) {
            $lineInHeaderFormat = "#define IDS_" + $content[0].ToUpper() + " " + $count.ToString()
            $newLinesForHeaderFile = $newLinesForHeaderFile + "`r`n" + $lineInHeaderFormat
            $count++
        }
    }

    # Delete temporary text file
    Remove-Item $tempFile

    $newLinesForRCFile = "`r`nSTRINGTABLE`r`nBEGIN" + $newLinesForRCFile + "`r`nEND"

    if (!$rcFileUpdated) {
        Set-Content -Path $generatedFilesFolder\$generatedRCFileName -Value ("// This file was auto-generated. Changes to this file may cause incorrect behavior and will be lost if the code is regenerated.`r`n")
        Add-Content -Path $generatedFilesFolder\$generatedRCFileName -Value (Get-Content $parentDirectory\$baseRCFileName)
        $rcFileUpdated = $true
    }

    Add-Content -Path $generatedFilesFolder\$generatedRCFileName -Value $newLinesForRCFile

    if (!$headerFileUpdated) {
        Set-Content -Path $generatedFilesFolder\$generatedHeaderFileName -Value ("// This file was auto-generated. Changes to this file may cause incorrect behavior and will be lost if the code is regenerated.`r`n")
        Add-Content -Path $generatedFilesFolder\$generatedHeaderFileName -Value (Get-Content $parentDirectory\$baseHeaderFileName)
        Add-Content -Path $generatedFilesFolder\$generatedHeaderFileName -Value $newLinesForHeaderFile
        $headerFileUpdated = $true
    }
}
